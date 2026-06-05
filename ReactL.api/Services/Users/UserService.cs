using Microsoft.EntityFrameworkCore;
using ReactL.api.Common.Exceptions;
using ReactL.api.Data;
using ReactL.api.Domain.Auth;
using ReactL.api.DTOs.Requests.Users;

namespace ReactL.api.Services.Users
{
    /// <summary>使用者服務實作</summary>
    public class UserService : IUserService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<UserService> _logger;

        public UserService(AppDbContext db, ILogger<UserService> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>取得使用者業務物件（不含密碼雜湊等安全欄位）</summary>
        public async Task<UserDomain> GetProfileAsync(Guid userId)
        {
            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new UserDomain
                {
                    Id = u.Id,
                    Email = u.Email,
                    DisplayName = u.DisplayName,
                    Role = u.Role,
                    IsActive = u.IsActive,
                    LastLoginAt = u.LastLoginAt,
                    CreatedAt = u.CreatedAt,
                    UpdatedAt = u.UpdatedAt
                })
                .FirstOrDefaultAsync();

            return user ?? throw new NotFoundException("User", userId);
        }

        /// <summary>更新使用者顯示名稱，回傳更新後的業務物件</summary>
        public async Task<UserDomain> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            var user = await _db.Users.FindAsync(userId)
                ?? throw new NotFoundException("User", userId);

            user.DisplayName = request.DisplayName.Trim();
            await _db.SaveChangesAsync();

            return new UserDomain
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Role = user.Role,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        /// <summary>
        /// 修改密碼
        /// 先驗證目前密碼，確認是本人操作
        /// </summary>
        public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            var user = await _db.Users.FindAsync(userId)
                ?? throw new NotFoundException("User", userId);

            // 先驗證目前密碼，確認是本人操作
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword.Trim(), user.PasswordHash))
            {
                _logger.LogWarning("密碼修改失敗：目前密碼不正確 UserId={UserId}", userId);
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    ["currentPassword"] = ["目前密碼不正確"]
                });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword.Trim());
            await _db.SaveChangesAsync();

            _logger.LogInformation("使用者密碼已修改 UserId={UserId}", userId);
        }
    }
}