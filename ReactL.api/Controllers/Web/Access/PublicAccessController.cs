using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ReactL.api.Common.Constants;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Responses.Access;
using ReactL.api.Services.Access;

namespace ReactL.api.Controllers.Web.Access
{
    /// <summary>前台公開存取碼狀態查詢（無需登入）</summary>
    [ApiController]
    [Route("api/v1/public/access")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicPerIp)]
    public class PublicAccessController : ControllerBase
    {
        private readonly IAccessCodeService _service;

        public PublicAccessController(IAccessCodeService service)
        {
            _service = service;
        }

        /// <summary>
        /// 查詢本站是否需要存取碼，以及（若有帶 X-Access-Code 標頭）該碼是否有效與今日剩餘額度。
        /// 前台 gate 用此決定是否要求輸入碼，並顯示剩餘用量。
        /// </summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(ApiResponse<PublicAccessStatusResponse>), 200)]
        public async Task<IActionResult> Status(CancellationToken cancellationToken)
        {
            var code = Request.Headers["X-Access-Code"].FirstOrDefault();
            var status = await _service.GetStatusAsync(code, cancellationToken);

            return Ok(ApiResponse<PublicAccessStatusResponse>.Ok(new PublicAccessStatusResponse
            {
                RequireAccessCode = status.RequireAccessCode,
                Valid = status.Valid,
                Label = status.Label,
                DailyTokenLimit = status.DailyTokenLimit,
                UsedToday = status.UsedToday,
                Remaining = status.Remaining,
            }));
        }
    }
}
