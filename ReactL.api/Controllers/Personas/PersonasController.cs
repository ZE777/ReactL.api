using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.Common.Extensions;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Personas;
using ReactL.api.Services.Personas;

namespace ReactL.api.Controllers.Personas
{
    /// <summary>AI 角色（Persona）管理 — CRUD + 版本控制 + AI 強化</summary>
    [ApiController]
    [Route("api/v1/personas")]
    [Authorize]
    public class PersonasController : ControllerBase
    {
        private readonly IPersonaService _personaService;

        public PersonasController(IPersonaService personaService)
        {
            _personaService = personaService;
        }

        /// <summary>取得可用的 Persona 清單（系統內建 + 本人自訂）</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<PersonaListItem>>), 200)]
        public async Task<IActionResult> GetList()
        {
            var result = await _personaService.GetListAsync(User.GetUserId());
            return Ok(ApiResponse<List<PersonaListItem>>.Ok(result));
        }

        /// <summary>取得單筆 Persona 詳情</summary>
        /// <response code="403">無權限</response>
        /// <response code="404">不存在</response>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PersonaDetailResponse>), 200)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _personaService.GetByIdAsync(id, User.GetUserId());
            return Ok(ApiResponse<PersonaDetailResponse>.Ok(result));
        }

        /// <summary>建立新 Persona</summary>
        /// <response code="200">建立成功，回傳詳情</response>
        /// <response code="422">輸入格式錯誤</response>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<PersonaDetailResponse>), 200)]
        public async Task<IActionResult> Create([FromBody] CreatePersonaRequest request)
        {
            var result = await _personaService.CreateAsync(User.GetUserId(), request);
            return Ok(ApiResponse<PersonaDetailResponse>.Ok(result));
        }

        /// <summary>更新 Persona（自動建立版本快照）</summary>
        /// <response code="403">無權限（系統內建或他人 Persona）</response>
        /// <response code="404">不存在</response>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PersonaDetailResponse>), 200)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePersonaRequest request)
        {
            var result = await _personaService.UpdateAsync(id, User.GetUserId(), request);
            return Ok(ApiResponse<PersonaDetailResponse>.Ok(result));
        }

        /// <summary>軟刪除 Persona</summary>
        /// <response code="200">刪除成功</response>
        /// <response code="403">無權限或為系統內建角色</response>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _personaService.DeleteAsync(id, User.GetUserId());
            return Ok(ApiResponse<object>.Ok(null!, "Persona 已刪除"));
        }

        // ── 版本管理 ─────────────────────────────────────────────────────────

        /// <summary>取得指定 Persona 的版本快照清單</summary>
        [HttpGet("{personaId:guid}/versions")]
        [ProducesResponseType(typeof(ApiResponse<List<PersonaVersionItem>>), 200)]
        public async Task<IActionResult> GetVersions(Guid personaId)
        {
            var result = await _personaService.GetVersionsAsync(personaId, User.GetUserId());
            return Ok(ApiResponse<List<PersonaVersionItem>>.Ok(result));
        }

        /// <summary>取得單一版本快照詳情</summary>
        [HttpGet("{personaId:guid}/versions/{versionId:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PersonaVersionDetailResponse>), 200)]
        public async Task<IActionResult> GetVersionDetail(Guid personaId, Guid versionId)
        {
            var result = await _personaService.GetVersionDetailAsync(personaId, versionId, User.GetUserId());
            return Ok(ApiResponse<PersonaVersionDetailResponse>.Ok(result));
        }

        /// <summary>回滾至指定版本（回滾本身也會產生新版本快照）</summary>
        [HttpPost("{personaId:guid}/versions/{versionId:guid}/rollback")]
        [ProducesResponseType(typeof(ApiResponse<PersonaDetailResponse>), 200)]
        public async Task<IActionResult> Rollback(Guid personaId, Guid versionId)
        {
            var result = await _personaService.RollbackAsync(personaId, versionId, User.GetUserId());
            return Ok(ApiResponse<PersonaDetailResponse>.Ok(result));
        }

        // ── AI 輔助 ──────────────────────────────────────────────────────────

        /// <summary>AI 強化 System Prompt（預覽用，不修改 DB）</summary>
        [HttpPost("enhance-prompt")]
        [ProducesResponseType(typeof(ApiResponse<EnhancePromptResponse>), 200)]
        public async Task<IActionResult> EnhancePrompt([FromBody] EnhancePromptRequest request)
        {
            var result = await _personaService.EnhancePromptAsync(request);
            return Ok(ApiResponse<EnhancePromptResponse>.Ok(result));
        }
    }
}
