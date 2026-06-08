using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ReactL.api.Common.Constants;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.Ai;
using ReactL.api.DTOs.Responses.PublicChat;
using ReactL.api.Services.Ai;
using ReactL.api.Services.PublicChat;
using System.Text;
using System.Text.Json;

namespace ReactL.api.Controllers.Web.Chat
{
    /// <summary>前台公開 AI 聊天端點（無需登入；訊息寫入 PublicChatLogs 供後台 Admin 監控）</summary>
    [ApiController]
    [Route("api/v1/public/chat")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicPerIp)]
    public class PublicChatController : ControllerBase
    {
        private readonly IAiService _aiService;
        private readonly IPublicChatLogService _publicChatLogs;

        public PublicChatController(IAiService aiService, IPublicChatLogService publicChatLogs)
        {
            _aiService = aiService;
            _publicChatLogs = publicChatLogs;
        }

        /// <summary>
        /// 前台公開 AI 聊天，以 SSE 串流回傳 AI 回應
        /// 前端負責在 client side 維護對話歷史並每次完整傳入
        /// 不寫入 DB，適合未登入訪客使用
        /// </summary>
        [HttpPost("stream")]
        public async Task Stream([FromBody] PublicChatRequest request, CancellationToken cancellationToken)
        {
            Response.ContentType = "text/event-stream; charset=utf-8";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            // 存取碼由標頭帶入（前台 ?code= → localStorage → X-Access-Code）
            var accessCode = Request.Headers["X-Access-Code"].FirstOrDefault();
            // 對話工作階段 Id（前台 localStorage 產生），供後台監控分組同一訪客的連續對話
            var sessionId = Request.Headers["X-Chat-Session"].FirstOrDefault();

            // AI 服務直接回傳 DTO（SSE 串流協議物件，不走 Domain）
            await foreach (var chunk in _aiService.PublicChatStreamAsync(request, accessCode, sessionId, cancellationToken))
            {
                var json = JsonSerializer.Serialize(chunk, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                var line = $"data: {json}\n\n";
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(line), cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }

        /// <summary>
        /// 取回前台訪客自己的歷史對話（指定角色）。
        /// 一人一碼：以 X-Access-Code 識別（跨裝置可見）；無碼時退回 X-Chat-Session（同瀏覽器）。
        /// </summary>
        [HttpGet("history")]
        [ProducesResponseType(typeof(ApiResponse<List<PublicChatHistoryItem>>), 200)]
        public async Task<IActionResult> History([FromQuery] Guid? personaId, CancellationToken cancellationToken)
        {
            var accessCode = Request.Headers["X-Access-Code"].FirstOrDefault();
            var sessionId = Request.Headers["X-Chat-Session"].FirstOrDefault();
            var items = await _publicChatLogs.GetHistoryAsync(accessCode, sessionId, personaId, cancellationToken);
            return Ok(ApiResponse<List<PublicChatHistoryItem>>.Ok(items));
        }
    }
}
