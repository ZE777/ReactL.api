using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.DTOs.Requests.Ai;
using ReactL.api.Services.Ai;
using System.Text;
using System.Text.Json;

namespace ReactL.api.Controllers.Web.Chat
{
    /// <summary>前台公開 AI 聊天端點（無需登入，不寫入 DB）</summary>
    [ApiController]
    [Route("api/v1/public/chat")]
    [AllowAnonymous]
    public class PublicChatController : ControllerBase
    {
        private readonly IAiService _aiService;

        public PublicChatController(IAiService aiService)
        {
            _aiService = aiService;
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

            // AI 服務直接回傳 DTO（SSE 串流協議物件，不走 Domain）
            await foreach (var chunk in _aiService.PublicChatStreamAsync(request, cancellationToken))
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
    }
}
