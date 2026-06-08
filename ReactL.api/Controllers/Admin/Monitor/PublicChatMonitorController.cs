using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ReactL.api.Common.Settings;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.PublicChat;
using ReactL.api.DTOs.Responses.PublicChat;
using ReactL.api.Services.PublicChat;

namespace ReactL.api.Controllers.Admin.Monitor
{
    /// <summary>前台公開聊天監控 API（僅管理員）</summary>
    [ApiController]
    [Route("api/v1/public-chat-monitor")]
    [Authorize(Roles = "Admin")]
    public class PublicChatMonitorController : ControllerBase
    {
        private readonly IPublicChatLogService _service;
        private readonly PublicChatSettings _settings;

        public PublicChatMonitorController(IPublicChatLogService service, IOptions<PublicChatSettings> settings)
        {
            _service = service;
            _settings = settings.Value;
        }

        /// <summary>取得聊天記錄保留天數（逾期自動清除）；0 = 永久保留</summary>
        [HttpGet("retention-days")]
        [ProducesResponseType(typeof(ApiResponse<int>), 200)]
        public IActionResult GetRetentionDays() => Ok(ApiResponse<int>.Ok(_settings.LogRetentionDays));

        /// <summary>取得前台聊天對話列表（以工作階段分組，含存取碼與用量彙總）</summary>
        [HttpGet("conversations")]
        [ProducesResponseType(typeof(ApiResponse<PagedResponse<PublicChatConversationSummary>>), 200)]
        public async Task<IActionResult> GetConversations([FromQuery] PublicChatConversationQueryParams query)
        {
            var result = await _service.GetConversationsAsync(query);
            return Ok(ApiResponse<PagedResponse<PublicChatConversationSummary>>.Ok(result));
        }

        /// <summary>取得指定對話的完整訊息記錄</summary>
        [HttpGet("messages")]
        [ProducesResponseType(typeof(ApiResponse<PagedResponse<PublicChatLogItem>>), 200)]
        public async Task<IActionResult> GetMessages([FromQuery] PublicChatMessageQueryParams query)
        {
            var result = await _service.GetMessagesAsync(query);
            return Ok(ApiResponse<PagedResponse<PublicChatLogItem>>.Ok(result));
        }
    }
}