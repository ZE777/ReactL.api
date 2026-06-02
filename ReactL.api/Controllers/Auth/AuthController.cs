using Microsoft.AspNetCore.Mvc;
using ReactL.api.DTOs.Auth;
using ReactL.api.DTOs.Common;
using ReactL.api.Services.Auth;

namespace ReactL.api.Controllers.Auth
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
            var result = await _authService.RegisterAsync(request);
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
            var result = await _authService.LoginAsync(request);
            return Ok(ApiResponse<AuthResponse>.Ok(result));
        }
    }
}
