using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ReactL.api.Common.Extensions;
using ReactL.api.Common.Settings;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.Ai;
using ReactL.api.DTOs.Responses.Ai;
using ReactL.api.Services.Ai;
using System.Text;
using System.Text.Json;

namespace ReactL.api.Controllers.Admin.Ai
{
    /// <summary>AI 對話端點（SSE 串流）</summary>
    [ApiController]
    [Route("api/v1/ai")]
    [Authorize]
    public class AiController : ControllerBase
    {
        private readonly IAiService _aiService;
        private readonly IOptions<AiSettings> _aiOptions;

        public AiController(IAiService aiService, IOptions<AiSettings> aiOptions)
        {
            _aiService = aiService;
            _aiOptions = aiOptions;
        }

        /// <summary>
        /// 發起 AI 對話，以 SSE 串流回傳 AI 回應
        /// 前端使用 fetch + ReadableStream 或 EventSource 接收
        /// Content-Type: text/event-stream
        /// </summary>
        [HttpPost("chat")]
        public async Task Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
        {
            Response.ContentType = "text/event-stream; charset=utf-8";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            var userId = User.GetUserId();

            // AI 服務直接回傳 DTO（SSE 串流協議物件，不走 Domain）
            await foreach (var chunk in _aiService.ChatStreamAsync(request, userId, cancellationToken))
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

        /// <summary>取得可用的 AI 提供商及模型清單</summary>
        [HttpGet("providers")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<List<AiProviderDto>>), 200)]
        public IActionResult GetProviders()
        {
            var settings = _aiOptions.Value;
            var result = settings.Providers.Select(p => new AiProviderDto
            {
                Id = p.Id,
                DisplayName = p.DisplayName,
                IsConfigured = settings.ProviderKeys.ContainsKey(p.Id) && !string.IsNullOrEmpty(settings.ProviderKeys[p.Id]),
                Models = p.Models.Select(m => new AiModelDto
                {
                    Id = m.Id,
                    DisplayName = m.DisplayName
                }).ToList()
            }).ToList();

            return Ok(ApiResponse<List<AiProviderDto>>.Ok(result));
        }
    }
}