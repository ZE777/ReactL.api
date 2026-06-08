using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.Common.Extensions;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.Users;
using ReactL.api.DTOs.Responses.Users;
using ReactL.api.Services.Users;

namespace ReactL.api.Controllers.Admin.Users
{
    /// <summary>使用者個人資料管理</summary>
    [ApiController]
    [Route("api/v1/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>取得目前登入使用者的個人資料</summary>
        /// <response code="200">個人資料</response>
        /// <response code="401">未登入</response>
        [HttpGet("me")]
        [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), 200)]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.GetUserId();
            var domain = await _userService.GetProfileAsync(userId);

            // 將業務 Domain 對應為 Response DTO 回傳前端
            var result = new UserProfileResponse
            {
                Id = domain.Id,
                Email = domain.Email,
                DisplayName = domain.DisplayName,
                Role = domain.Role,
                IsActive = domain.IsActive,
                LastLoginAt = domain.LastLoginAt,
                MustChangePassword = domain.MustChangePassword,
                CreatedAt = domain.CreatedAt
            };
            return Ok(ApiResponse<UserProfileResponse>.Ok(result));
        }

        /// <summary>更新個人資料（顯示名稱）</summary>
        /// <response code="200">更新後的個人資料</response>
        /// <response code="401">未登入</response>
        /// <response code="422">輸入格式錯誤</response>
        [HttpPatch("me")]
        [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), 200)]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = User.GetUserId();
            var domain = await _userService.UpdateProfileAsync(userId, request);

            // 將業務 Domain 對應為 Response DTO 回傳前端
            var result = new UserProfileResponse
            {
                Id = domain.Id,
                Email = domain.Email,
                DisplayName = domain.DisplayName,
                Role = domain.Role,
                IsActive = domain.IsActive,
                LastLoginAt = domain.LastLoginAt,
                MustChangePassword = domain.MustChangePassword,
                CreatedAt = domain.CreatedAt
            };
            return Ok(ApiResponse<UserProfileResponse>.Ok(result));
        }

        /// <summary>修改密碼</summary>
        /// <response code="200">密碼修改成功</response>
        /// <response code="401">未登入</response>
        /// <response code="422">目前密碼不正確</response>
        [HttpPost("me/change-password")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = User.GetUserId();
            await _userService.ChangePasswordAsync(userId, request);
            return Ok(ApiResponse<object>.Ok(null!, "密碼已更新"));
        }
    }
}
