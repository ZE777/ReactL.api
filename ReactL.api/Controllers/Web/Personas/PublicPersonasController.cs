using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Responses.Personas;
using ReactL.api.Services.Personas;

namespace ReactL.api.Controllers.Web.Personas
{
    /// <summary>前台公開 Persona 端點（無需登入）</summary>
    [ApiController]
    [Route("api/v1/public/personas")]
    [AllowAnonymous]
    public class PublicPersonasController : ControllerBase
    {
        private readonly IPersonaService _personaService;

        public PublicPersonasController(IPersonaService personaService)
        {
            _personaService = personaService;
        }

        /// <summary>取得開放前台顯示的 Persona 清單（isBuiltin=true）</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<PersonaListItem>>), 200)]
        public async Task<IActionResult> GetPublicPersonas()
        {
            var domains = await _personaService.GetPublicPersonasAsync();

            // 將業務 Domain 清單對應為 Response DTO 清單（輕量版，不含完整 SystemPrompt）
            var result = domains.Select(d => new PersonaListItem
            {
                Id = d.Id,
                Name = d.Name,
                Emoji = d.Emoji,
                CurrentVersion = d.CurrentVersion,
                IsBuiltin = d.IsBuiltin,
                UserId = d.UserId,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            }).ToList();
            return Ok(ApiResponse<List<PersonaListItem>>.Ok(result));
        }
    }
}
