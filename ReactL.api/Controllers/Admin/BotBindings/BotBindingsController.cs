using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.Common.Extensions;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.BotBindings;
using ReactL.api.DTOs.Responses.BotBindings;
using ReactL.api.Services.BotBindings;

namespace ReactL.api.Controllers.Admin.BotBindings
{
    /// <summary>Bot 平台綁定管理（LINE / Discord）</summary>
    [ApiController]
    [Route("api/v1/bot-bindings")]
    [Authorize]
    public class BotBindingsController : ControllerBase
    {
        private readonly IBotBindingService _service;

        public BotBindingsController(IBotBindingService service)
        {
            _service = service;
        }

        /// <summary>取得 Bot 清單</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<BotBindingListItem>>), 200)]
        public async Task<IActionResult> GetList()
        {
            var domains = await _service.GetListAsync(User.GetUserId());

            // 將業務 Domain 清單對應為 Response DTO 清單
            var result = domains.Select(d => new BotBindingListItem
            {
                Id = d.Id,
                Platform = d.Platform,
                BotName = d.BotName,
                TokenLastFour = d.TokenLastFour,
                ModelType = d.ModelType,
                IsEnabled = d.IsEnabled,
                PersonaId = d.PersonaId,
                PersonaName = d.PersonaName,
                WebhookUrl = d.WebhookUrl,
                WebhookBaseUrl = d.WebhookBaseUrl,
                DiscordApplicationId = d.DiscordApplicationId,
                DiscordPublicKey = d.DiscordPublicKey,
                TrustedUserCount = d.TrustedUserCount,
                CredentialValid = d.CredentialValid,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            }).ToList();
            return Ok(ApiResponse<List<BotBindingListItem>>.Ok(result));
        }

        /// <summary>取得 Bot 詳情</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<BotBindingDetailResponse>), 200)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var domain = await _service.GetByIdAsync(id, User.GetUserId());
            var result = ToDetailResponse(domain);
            return Ok(ApiResponse<BotBindingDetailResponse>.Ok(result));
        }

        /// <summary>新增 Bot 綁定（Token 由後端加密儲存）</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<BotBindingDetailResponse>), 200)]
        public async Task<IActionResult> Create([FromBody] CreateBotBindingRequest request)
        {
            var domain = await _service.CreateAsync(User.GetUserId(), request);
            var result = ToDetailResponse(domain);
            return Ok(ApiResponse<BotBindingDetailResponse>.Ok(result));
        }

        /// <summary>更新 Bot 設定（名稱、模型、Persona、啟用狀態）</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<BotBindingDetailResponse>), 200)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBotBindingRequest request)
        {
            var domain = await _service.UpdateAsync(id, User.GetUserId(), request);
            var result = ToDetailResponse(domain);
            return Ok(ApiResponse<BotBindingDetailResponse>.Ok(result));
        }

        /// <summary>軟刪除 Bot</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id, User.GetUserId());
            return Ok(ApiResponse<object>.Ok(null!, "Bot 已刪除"));
        }

        /// <summary>更換 Bot Token（Token 重新 AES 加密後存回 DB）</summary>
        [HttpPost("{id:guid}/rotate-token")]
        [ProducesResponseType(typeof(ApiResponse<BotBindingDetailResponse>), 200)]
        public async Task<IActionResult> RotateToken(Guid id, [FromBody] RotateTokenRequest request)
        {
            var domain = await _service.RotateTokenAsync(id, User.GetUserId(), request);
            var result = ToDetailResponse(domain);
            return Ok(ApiResponse<BotBindingDetailResponse>.Ok(result));
        }

        /// <summary>查詢 LINE Bot 本月訊息用量（即時呼叫 LINE API，僅 LINE 平台有效）</summary>
        [HttpGet("{id:guid}/line-quota")]
        [ProducesResponseType(typeof(ApiResponse<LineQuotaResponse>), 200)]
        public async Task<IActionResult> GetLineQuota(Guid id)
        {
            var result = await _service.GetLineQuotaAsync(id, User.GetUserId());
            return Ok(ApiResponse<LineQuotaResponse>.Ok(result));
        }

        /// <summary>取得 Bot 的信任系統成員名單</summary>
        [HttpGet("{id:guid}/trusted-users")]
        [ProducesResponseType(typeof(ApiResponse<List<TrustedUserResponse>>), 200)]
        public async Task<IActionResult> GetTrustedUsers(Guid id)
        {
            var result = await _service.GetTrustedUsersAsync(id, User.GetUserId());
            return Ok(ApiResponse<List<TrustedUserResponse>>.Ok(result));
        }

        /// <summary>新增一位信任對象</summary>
        [HttpPost("{id:guid}/trusted-users")]
        [ProducesResponseType(typeof(ApiResponse<TrustedUserResponse>), 200)]
        public async Task<IActionResult> AddTrustedUser(Guid id, [FromBody] AddTrustedUserRequest request)
        {
            var result = await _service.AddTrustedUserAsync(id, User.GetUserId(), request);
            return Ok(ApiResponse<TrustedUserResponse>.Ok(result, "已加入信任名單"));
        }

        /// <summary>移除一位信任對象</summary>
        [HttpDelete("{id:guid}/trusted-users/{discordUserId}")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> RemoveTrustedUser(Guid id, string discordUserId)
        {
            await _service.RemoveTrustedUserAsync(id, User.GetUserId(), discordUserId);
            return Ok(ApiResponse<object>.Ok(null!, "已移出信任名單"));
        }

        // ── 私有輔助方法 ──────────────────────────────────────────────────────

        /// <summary>將 BotBinding Domain 轉換為詳情 Response DTO</summary>
        private static BotBindingDetailResponse ToDetailResponse(Domain.BotBindings.BotBindingDomain d) =>
            new()
            {
                Id = d.Id,
                Platform = d.Platform,
                BotName = d.BotName,
                TokenLastFour = d.TokenLastFour,
                ModelType = d.ModelType,
                IsEnabled = d.IsEnabled,
                PersonaId = d.PersonaId,
                PersonaName = d.PersonaName,
                WebhookUrl = d.WebhookUrl,
                WebhookBaseUrl = d.WebhookBaseUrl,
                DiscordApplicationId = d.DiscordApplicationId,
                DiscordPublicKey = d.DiscordPublicKey,
                TrustedUserCount = d.TrustedUserCount,
                CredentialValid = d.CredentialValid,
                CredentialError = d.CredentialError,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            };
    }
}