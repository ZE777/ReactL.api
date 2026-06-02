using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.Common.Extensions;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.PromptTemplates;
using ReactL.api.Services.PromptTemplates;

namespace ReactL.api.Controllers.PromptTemplates
{
    /// <summary>Prompt 模板管理</summary>
    [ApiController]
    [Route("api/v1/prompt-templates")]
    [Authorize]
    public class PromptTemplatesController : ControllerBase
    {
        private readonly IPromptTemplateService _service;

        public PromptTemplatesController(IPromptTemplateService service)
        {
            _service = service;
        }

        /// <summary>取得模板清單（支援分類篩選與關鍵字搜尋）</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<PromptTemplateListItem>>), 200)]
        public async Task<IActionResult> GetList([FromQuery] PromptTemplateQueryParams query)
        {
            var result = await _service.GetListAsync(User.GetUserId(), query);
            return Ok(ApiResponse<List<PromptTemplateListItem>>.Ok(result));
        }

        /// <summary>取得模板詳情</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PromptTemplateDetailResponse>), 200)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id, User.GetUserId());
            return Ok(ApiResponse<PromptTemplateDetailResponse>.Ok(result));
        }

        /// <summary>建立模板</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<PromptTemplateDetailResponse>), 200)]
        public async Task<IActionResult> Create([FromBody] CreatePromptTemplateRequest request)
        {
            var result = await _service.CreateAsync(User.GetUserId(), request);
            return Ok(ApiResponse<PromptTemplateDetailResponse>.Ok(result));
        }

        /// <summary>更新模板</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PromptTemplateDetailResponse>), 200)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePromptTemplateRequest request)
        {
            var result = await _service.UpdateAsync(id, User.GetUserId(), request);
            return Ok(ApiResponse<PromptTemplateDetailResponse>.Ok(result));
        }

        /// <summary>軟刪除模板</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _service.DeleteAsync(id, User.GetUserId());
            return Ok(ApiResponse<object>.Ok(null!, "模板已刪除"));
        }

        /// <summary>記錄套用次數 +1</summary>
        [HttpPost("{id:guid}/use")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> Use(Guid id)
        {
            await _service.IncrementUsageAsync(id, User.GetUserId());
            return Ok(ApiResponse<object>.Ok(null!));
        }
    }
}
