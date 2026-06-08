using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReactL.api.DTOs.Common;
using ReactL.api.DTOs.Requests.Access;
using ReactL.api.DTOs.Responses.Access;
using ReactL.api.Services.Access;

namespace ReactL.api.Controllers.Admin.Access
{
    /// <summary>存取碼（邀請碼）管理，僅限 Admin 角色</summary>
    [ApiController]
    [Route("api/v1/access-codes")]
    [Authorize(Roles = "Admin")]
    public class AccessCodesController : ControllerBase
    {
        private readonly IAccessCodeService _service;

        public AccessCodesController(IAccessCodeService service)
        {
            _service = service;
        }

        /// <summary>存取碼清單（含今日用量）</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<AccessCodeResponse>>), 200)]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var items = await _service.ListAsync(cancellationToken);
            return Ok(ApiResponse<List<AccessCodeResponse>>.Ok(items.Select(Map).ToList()));
        }

        /// <summary>建立存取碼（碼由後端隨機產生）</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<AccessCodeResponse>), 200)]
        public async Task<IActionResult> Create([FromBody] CreateAccessCodeRequest request, CancellationToken cancellationToken)
        {
            var item = await _service.CreateAsync(request.Label, request.DailyTokenLimit, request.ExpiresAt, cancellationToken);
            return Ok(ApiResponse<AccessCodeResponse>.Ok(Map(item), "存取碼已建立"));
        }

        /// <summary>更新存取碼（標籤 / 每日上限 / 到期）</summary>
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<AccessCodeResponse>), 200)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAccessCodeRequest request, CancellationToken cancellationToken)
        {
            var item = await _service.UpdateAsync(id, request.Label, request.DailyTokenLimit, request.ExpiresAt, cancellationToken);
            return Ok(ApiResponse<AccessCodeResponse>.Ok(Map(item), "已更新"));
        }

        /// <summary>啟用 / 停用存取碼</summary>
        [HttpPatch("{id:guid}/active")]
        [ProducesResponseType(typeof(ApiResponse<AccessCodeResponse>), 200)]
        public async Task<IActionResult> SetActive(Guid id, [FromBody] SetAccessCodeActiveRequest request, CancellationToken cancellationToken)
        {
            var item = await _service.SetActiveAsync(id, request.IsActive, cancellationToken);
            return Ok(ApiResponse<AccessCodeResponse>.Ok(Map(item), request.IsActive ? "已啟用" : "已停用"));
        }

        /// <summary>刪除存取碼（連帶刪除其用量記錄）</summary>
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<object>), 200)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await _service.DeleteAsync(id, cancellationToken);
            return Ok(ApiResponse<object>.Ok(null!, "存取碼已刪除"));
        }

        private static AccessCodeResponse Map(AccessCodeListItem i) => new()
        {
            Id = i.Id,
            Code = i.Code,
            Label = i.Label,
            DailyTokenLimit = i.DailyTokenLimit,
            ExpiresAt = i.ExpiresAt,
            IsActive = i.IsActive,
            CreatedAt = i.CreatedAt,
            UsedTokensToday = i.UsedTokensToday,
            RequestsToday = i.RequestsToday,
        };
    }
}
