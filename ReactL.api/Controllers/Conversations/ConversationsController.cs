using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.Common.Extensions;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Conversations;
using ReactL.api.Services.Conversations;

namespace ReactL.api.Controllers.Conversations
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
            var result = await _service.GetListAsync(User.GetUserId());
            return Ok(ApiResponse<List<ConversationListItem>>.Ok(result));
        }

        /// <summary>取得對話詳情（含訊息列表）</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), 200)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id, User.GetUserId());
            return Ok(ApiResponse<ConversationDetailResponse>.Ok(result));
        }

        /// <summary>依 ShareSlug 取得公開對話（不需登入）</summary>
        [HttpGet("share/{slug}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), 200)]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var result = await _service.GetBySlugAsync(slug);
            return Ok(ApiResponse<ConversationDetailResponse>.Ok(result));
        }

        /// <summary>建立新對話</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), 200)]
        public async Task<IActionResult> Create([FromBody] CreateConversationRequest request)
        {
            var result = await _service.CreateAsync(User.GetUserId(), request);
            return Ok(ApiResponse<ConversationDetailResponse>.Ok(result));
        }

        /// <summary>更新對話（標題、釘選、公開分享）</summary>
        [HttpPatch("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<ConversationDetailResponse>), 200)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConversationRequest request)
        {
            var result = await _service.UpdateAsync(id, User.GetUserId(), request);
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
            var result = await _service.AddMessageAsync(id, User.GetUserId(), request);
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
    }
}
