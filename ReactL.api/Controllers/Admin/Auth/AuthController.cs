using Microsoft.AspNetCore.Mvc;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.Auth;
using ReactL.api.DTOs.Responses.Auth;
using ReactL.api.Services.Auth;

namespace ReactL.api.Controllers.Admin.Auth
{
    /// <summary>認證相關端點：註冊、登入</summary>
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>註冊新帳號</summary>
        /// <response code="200">註冊成功，回傳 JWT Token</response>
        /// <response code="409">Email 已被使用</response>
        /// <response code="422">輸入格式錯誤</response>
        [HttpPost("register")]
        [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 200)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var domain = await _authService.RegisterAsync(request);

            // 將業務 Domain 對應為 Response DTO 回傳前端
            var result = new AuthResponse
            {
                Token = domain.Token,
                ExpiresAt = domain.ExpiresAt,
                User = new UserInfo
                {
                    Id = domain.User.Id,
                    Email = domain.User.Email,
                    DisplayName = domain.User.DisplayName,
                    Role = domain.User.Role
                }
            };
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }

        /// <summary>帳號密碼登入</summary>
        /// <response code="200">登入成功，回傳 JWT Token</response>
        /// <response code="401">帳號或密碼錯誤</response>
        /// <response code="403">帳號已停用</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<AuthResponse>), 200)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var domain = await _authService.LoginAsync(request);

            // 將業務 Domain 對應為 Response DTO 回傳前端
            var result = new AuthResponse
            {
                Token = domain.Token,
                ExpiresAt = domain.ExpiresAt,
                User = new UserInfo
                {
                    Id = domain.User.Id,
                    Email = domain.User.Email,
                    DisplayName = domain.User.DisplayName,
                    Role = domain.User.Role
                }
            };
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }
    }
}
