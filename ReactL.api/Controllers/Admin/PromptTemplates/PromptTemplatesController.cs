using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.Common.Extensions;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.PromptTemplates;
using ReactL.api.DTOs.Responses.PromptTemplates;
using ReactL.api.Services.PromptTemplates;

namespace ReactL.api.Controllers.Admin.PromptTemplates
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
            var domains = await _service.GetListAsync(User.GetUserId(), query);

            // 將業務 Domain 清單對應為 Response DTO 清單
            var result = domains.Select(d => new PromptTemplateListItem
            {
                Id = d.Id,
                Title = d.Title,
                Content = d.Content,
                Category = d.Category,
                Tags = d.Tags,
                UsageCount = d.UsageCount,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            }).ToList();
            return Ok(ApiResponse<List<PromptTemplateListItem>>.Ok(result));
        }

        /// <summary>取得模板詳情</summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PromptTemplateDetailResponse>), 200)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var domain = await _service.GetByIdAsync(id, User.GetUserId());

            // 將業務 Domain 對應為詳情 Response DTO
            var result = new PromptTemplateDetailResponse
            {
                Id = domain.Id,
                Title = domain.Title,
                Content = domain.Content,
                Category = domain.Category,
                Tags = domain.Tags,
                UsageCount = domain.UsageCount,
                CreatedAt = domain.CreatedAt,
                UpdatedAt = domain.UpdatedAt
            };
            return Ok(ApiResponse<PromptTemplateDetailResponse>.Ok(result));
        }

        /// <summary>建立模板</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<PromptTemplateDetailResponse>), 200)]
        public async Task<IActionResult> Create([FromBody] CreatePromptTemplateRequest request)
        {
            var domain = await _service.CreateAsync(User.GetUserId(), request);
            var result = new PromptTemplateDetailResponse
            {
                Id = domain.Id,
                Title = domain.Title,
                Content = domain.Content,
                Category = domain.Category,
                Tags = domain.Tags,
                UsageCount = domain.UsageCount,
                CreatedAt = domain.CreatedAt,
                UpdatedAt = domain.UpdatedAt
            };
            return Ok(ApiResponse<PromptTemplateDetailResponse>.Ok(result));
        }

        /// <summary>更新模板</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PromptTemplateDetailResponse>), 200)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePromptTemplateRequest request)
        {
            var domain = await _service.UpdateAsync(id, User.GetUserId(), request);
            var result = new PromptTemplateDetailResponse
            {
                Id = domain.Id,
                Title = domain.Title,
                Content = domain.Content,
                Category = domain.Category,
                Tags = domain.Tags,
                UsageCount = domain.UsageCount,
                CreatedAt = domain.CreatedAt,
                UpdatedAt = domain.UpdatedAt
            };
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
