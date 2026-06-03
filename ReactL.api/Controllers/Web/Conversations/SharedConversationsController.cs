using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Responses.Conversations;
using ReactL.api.Services.Conversations;

namespace ReactL.api.Controllers.Web.Conversations
{
    /// <summary>前台公開對話端點（不需登入）</summary>
    [ApiController]
    [Route("api/v1/conversations")]
    [AllowAnonymous]
    public class SharedConversationsController : ControllerBase
    {
        private readonly IConversationService _service;

        public SharedConversationsController(IConversationService service)
        {
            _service = service;
        }

        /// <summary>依 ShareSlug 取得公開對話（不需登入）</summary>
        [HttpGet("share/{slug}")]
        [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), 200)]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var domain = await _service.GetBySlugAsync(slug);

            // 將對話 Domain 對應為詳情 Response DTO 回傳前台
            var result = new ConversationDetailResponse
            {
                Id = domain.Id,
                Title = domain.Title,
                ModelType = domain.ModelType,
                IsPinned = domain.IsPinned,
                IsPublic = domain.IsPublic,
                IsDeleted = domain.IsDeleted,
                ShareSlug = domain.ShareSlug,
                PersonaId = domain.PersonaId,
                PersonaName = domain.PersonaName,
                PersonaEmoji = domain.PersonaEmoji,
                Messages = domain.Messages.Select(m => new MessageResponse
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    TokensIn = m.TokensIn,
                    TokensOut = m.TokensOut,
                    CreatedAt = m.CreatedAt
                }).ToList(),
                CreatedAt = domain.CreatedAt,
                UpdatedAt = domain.UpdatedAt
            };
            return Ok(ApiResponse<ConversationDetailResponse>.Ok(result));
        }
    }
}