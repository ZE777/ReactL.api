using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReactL.api.Common.Exceptions;
using ReactL.api.Common.Settings;
using ReactL.api.Data;
using ReactL.api.Domain.Auth;
using ReactL.api.DTOs.Requests.Auth;
using ReactL.api.Models.Auth;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ReactL.api.Services.Auth
{
    /// <summary>認證服務實作</summary>
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly JwtSettings _jwt;
        private readonly ILogger<AuthService> _logger;

        public AuthService(AppDbContext db, IOptions<JwtSettings> jwt, ILogger<AuthService> logger)
        {
            _db = db;
            _jwt = jwt.Value;
            _logger = logger;
        }

        /// <summary>註冊新帳號，若 Email 已存在則拋出 ConflictException</summary>
        public async Task<AuthResultDomain> RegisterAsync(RegisterRequest request)
        {
            // Email 不分大小寫比對，防止用 User@example.com 繞過 user@example.com 的唯一限制
            var exists = await _db.Users
                .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());

            if (exists)
            {
                _logger.LogWarning("註冊失敗：Email 已存在 {Email}", request.Email.ToLower());
                throw new ConflictException("此 Email 已被註冊");
            }

            var user = new User
            {
                Email = request.Email.ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                DisplayName = request.DisplayName,
                Role = "User"
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("新使用者註冊成功 UserId={UserId} Email={Email}", user.Id, user.Email);
            return BuildAuthResultDomain(user);
        }

        /// <summary>驗證帳號密碼，不區分「帳號不存在」與「密碼錯誤」防止帳號列舉</summary>
        public async Task<AuthResultDomain> LoginAsync(LoginRequest request)
        {
            // 故意不區分「帳號不存在」與「密碼錯誤」，統一回傳相同訊息，防止帳號列舉攻擊
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower());

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("登入失敗：帳號或密碼錯誤 Email={Email}", request.Email.ToLower());
                throw new UnauthorizedException("帳號或密碼錯誤");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("登入失敗：帳號已停用 UserId={UserId} Email={Email}", user.Id, user.Email);
                throw new ForbiddenException("帳號已停用，請聯繫管理員");
            }

            user.LastLoginAt = DateTime.Now;
            await _db.SaveChangesAsync();

            _logger.LogInformation("使用者登入成功 UserId={UserId} Email={Email}", user.Id, user.Email);
            return BuildAuthResultDomain(user);
        }

        /// <summary>將 User Entity 轉換為業務結果 Domain，並產生 JWT Token</summary>
        private AuthResultDomain BuildAuthResultDomain(User user)
        {
            var expiresAt = DateTime.Now.AddMinutes(_jwt.ExpirationMinutes);
            var token = GenerateJwtToken(user, expiresAt);

            return new AuthResultDomain
            {
                Token = token,
                ExpiresAt = expiresAt,
                User = new UserDomain
                {
                    Id = user.Id,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    Role = user.Role,
                    IsActive = user.IsActive,
                    LastLoginAt = user.LastLoginAt,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt
                }
            };
        }

        /// <summary>產生包含 userId、email、role 的 JWT Token</summary>
        private string GenerateJwtToken(User user, DateTime expiresAt)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Claim 設計：sub = userId（標準），放入 role 供 [Authorize(Roles="Admin")] 使用
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
