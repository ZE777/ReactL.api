using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.Common.Extensions;
using ReactL.api.DTOs.BotBindings;
using ReactL.api.DTOs.Common;
using ReactL.api.Services.BotBindings;

namespace ReactL.api.Controllers.BotBindings
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
            var result = await _service.GetListAsync(User.GetUserId());
            return Ok(ApiResponse<List<BotBindingListItem>>.Ok(result));
        }

        /// <summary>取得 Bot 詳情</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<BotBindingDetailResponse>), 200)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id, User.GetUserId());
            return Ok(ApiResponse<BotBindingDetailResponse>.Ok(result));
        }

        /// <summary>新增 Bot 綁定（Token 由後端加密儲存）</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<BotBindingDetailResponse>), 200)]
        public async Task<IActionResult> Create([FromBody] CreateBotBindingRequest request)
        {
            var result = await _service.CreateAsync(User.GetUserId(), request);
            return Ok(ApiResponse<BotBindingDetailResponse>.Ok(result));
        }

        /// <summary>更新 Bot 設定（名稱、模型、Persona、啟用狀態）</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<BotBindingDetailResponse>), 200)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBotBindingRequest request)
        {
            var result = await _service.UpdateAsync(id, User.GetUserId(), request);
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
            var result = await _service.RotateTokenAsync(id, User.GetUserId(), request);
            return Ok(ApiResponse<BotBindingDetailResponse>.Ok(result));
        }
    }
}
