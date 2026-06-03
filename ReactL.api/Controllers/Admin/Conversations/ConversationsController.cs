using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.Common.Extensions;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.Conversations;
using ReactL.api.DTOs.Responses.Conversations;
using ReactL.api.Services.Conversations;

namespace ReactL.api.Controllers.Admin.Conversations
{
    /// <summary>對話記錄管理（含訊息 CRUD）</summary>
    [ApiController]
    [Route("api/v1/conversations")]
    [Authorize]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationService _service;

        public ConversationsController(IConversationService service)
        {
            _service = service;
        }

        /// <summary>取得對話清單（釘選優先，再依更新時間排序）</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<ConversationListItem>>), 200)]
        public async Task<IActionResult> GetList()
        {
            var domains = await _service.GetListAsync(User.GetUserId());

            // 將業務 Domain 清單對應為 Response DTO 清單
            var result = domains.Select(d => new ConversationListItem
            {
                Id = d.Id,
                Title = d.Title,
                ModelType = d.ModelType,
                IsPinned = d.IsPinned,
                IsPublic = d.IsPublic,
                ShareSlug = d.ShareSlug,
                PersonaId = d.PersonaId,
                PersonaName = d.PersonaName,
                MessageCount = d.MessageCount,
                LastMessagePreview = d.LastMessagePreview,
                LastMessageRole = d.LastMessageRole,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            }).ToList();
            return Ok(ApiResponse<List<ConversationListItem>>.Ok(result));
        }

        /// <summary>取得對話詳情（含訊息列表）</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), 200)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var domain = await _service.GetByIdAsync(id, User.GetUserId());
            var result = ToDetailResponse(domain);
            return Ok(ApiResponse<ConversationDetailResponse>.Ok(result));
        }

        /// <summary>建立新對話</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), 200)]
        public async Task<IActionResult> Create([FromBody] CreateConversationRequest request)
        {
            var domain = await _service.CreateAsync(User.GetUserId(), request);
            var result = ToDetailResponse(domain);
            return Ok(ApiResponse<ConversationDetailResponse>.Ok(result));
        }

        /// <summary>更新對話（標題、釘選、公開分享）</summary>
        [HttpPatch("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), 200)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConversationRequest request)
        {
            var domain = await _service.UpdateAsync(id, User.GetUserId(), request);
            var result = ToDetailResponse(domain);
            return Ok(ApiResponse<ConversationDetailResponse>.Ok(result));
        }

        /// <summary>軟刪除對話</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id, User.GetUserId());
            return Ok(ApiResponse<object>.Ok(null!, "對話已刪除"));
        }

        /// <summary>新增訊息到對話</summary>
        [HttpPost("{id:guid}/messages")]
        [ProducesResponseType(typeof(ApiResponse<MessageResponse>), 200)]
        public async Task<IActionResult> AddMessage(Guid id, [FromBody] AddMessageRequest request)
        {
            var domain = await _service.AddMessageAsync(id, User.GetUserId(), request);

            // 將訊息 Domain 對應為 Response DTO
            var result = new MessageResponse
            {
                Id = domain.Id,
                Role = domain.Role,
                Content = domain.Content,
                TokensIn = domain.TokensIn,
                TokensOut = domain.TokensOut,
                CreatedAt = domain.CreatedAt
            };
            return Ok(ApiResponse<MessageResponse>.Ok(result));
        }

        /// <summary>刪除最後一筆 assistant 訊息（Regenerate 前呼叫，清除舊回應）</summary>
        [HttpDelete("{id:guid}/last-assistant-message")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> DeleteLastAssistantMessage(Guid id)
        {
            await _service.DeleteLastAssistantMessageAsync(id, User.GetUserId());
            return Ok(ApiResponse<object>.Ok(null!, "已刪除最後一筆 AI 回應"));
        }

        // ── 私有輔助方法 ──────────────────────────────────────────────────────

        /// <summary>將對話 Domain 轉換為詳情 Response DTO（含完整訊息列表）</summary>
        private static ConversationDetailResponse ToDetailResponse(Domain.Conversations.ConversationDomain d) =>
            new()
            {
                Id = d.Id,
                Title = d.Title,
                ModelType = d.ModelType,
                IsPinned = d.IsPinned,
                IsPublic = d.IsPublic,
                IsDeleted = d.IsDeleted,
                ShareSlug = d.ShareSlug,
                PersonaId = d.PersonaId,
                PersonaName = d.PersonaName,
                PersonaEmoji = d.PersonaEmoji,
                Messages = d.Messages.Select(m => new MessageResponse
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    TokensIn = m.TokensIn,
                    TokensOut = m.TokensOut,
                    CreatedAt = m.CreatedAt
                }).ToList(),
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            };
    }
}