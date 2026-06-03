using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.Common.Extensions;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.Monitor;
using ReactL.api.DTOs.Responses.Monitor;
using ReactL.api.Services.Monitor;

namespace ReactL.api.Controllers.Admin.Monitor
{
    /// <summary>監控與統計 API</summary>
    [ApiController]
    [Route("api/v1/monitor")]
    [Authorize]
    public class MonitorController : ControllerBase
    {
        private readonly IMonitorService _service;

        public MonitorController(IMonitorService service)
        {
            _service = service;
        }

        /// <summary>取得外部平台訊息列表（Monitor 頁，支援平台/時間/使用者篩選）</summary>
        [HttpGet("messages")]
        [ProducesResponseType(typeof(ApiResponse<PagedResponse<ExternalMessageListItem>>), 200)]
        public async Task<IActionResult> GetMessages([FromQuery] MonitorQueryParams query)
        {
            // Monitor 服務直接回傳 DTO（統計資料是計算後的彙總，不走 Domain）
            var result = await _service.GetExternalMessagesAsync(User.GetUserId(), query);
            return Ok(ApiResponse<PagedResponse<ExternalMessageListItem>>.Ok(result));
        }

        /// <summary>取得 Token 用量統計總覽（含圖表資料）</summary>
        [HttpGet("stats/tokens")]
        [ProducesResponseType(typeof(ApiResponse<StatsSummary>), 200)]
        public async Task<IActionResult> GetTokenStats([FromQuery] StatsQueryParams query)
        {
            // Monitor 服務直接回傳 DTO（統計資料是計算後的彙總，不走 Domain）
            var result = await _service.GetTokenStatsAsync(User.GetUserId(), query);
            return Ok(ApiResponse<StatsSummary>.Ok(result));
        }
    }
}
