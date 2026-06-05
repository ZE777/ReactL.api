using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.Common.Extensions;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.AiKeys;
using ReactL.api.DTOs.Responses.AiKeys;
using ReactL.api.Services.Ai;

namespace ReactL.api.Controllers.Admin.AiKeys
{
    /// <summary>使用者 AI 金鑰管理（混合 BYOK：自帶 Key 覆蓋系統預設）</summary>
    [ApiController]
    [Route("api/v1/users/me/ai-keys")]
    [Authorize]
    public class AiKeysController : ControllerBase
    {
        private readonly IAiKeyService _service;

        public AiKeysController(IAiKeyService service)
        {
            _service = service;
        }

        /// <summary>取得目前使用者的 AI 金鑰清單（不含系統預設）</summary>
        /// <response code="200">金鑰清單（僅後 4 碼）</response>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<AiKeyResponse>>), 200)]
        public async Task<IActionResult> GetMyKeys()
        {
            var result = await _service.GetMyKeysAsync(User.GetUserId());
            return Ok(ApiResponse<List<AiKeyResponse>>.Ok(result));
        }

        /// <summary>新增或更新某供應商的 AI 金鑰（儲存前會驗證金鑰有效性）</summary>
        /// <response code="200">儲存後的金鑰（僅後 4 碼）</response>
        /// <response code="422">供應商未知或金鑰無效</response>
        [HttpPut]
        [ProducesResponseType(typeof(ApiResponse<AiKeyResponse>), 200)]
        public async Task<IActionResult> Upsert([FromBody] UpsertAiKeyRequest request)
        {
            var result = await _service.UpsertAsync(User.GetUserId(), request);
            return Ok(ApiResponse<AiKeyResponse>.Ok(result, "AI 金鑰已儲存"));
        }

        /// <summary>刪除某供應商的 AI 金鑰</summary>
        /// <response code="200">刪除成功</response>
        /// <response code="404">該供應商尚無金鑰</response>
        [HttpDelete("{providerId}")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> Delete(string providerId)
        {
            await _service.DeleteAsync(User.GetUserId(), providerId);
            return Ok(ApiResponse<object>.Ok(null!, "AI 金鑰已刪除"));
        }
    }
}
