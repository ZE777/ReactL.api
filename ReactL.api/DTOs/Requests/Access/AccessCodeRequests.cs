using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.Requests.Access
{
    /// <summary>建立存取碼（碼由後端隨機產生）</summary>
    public class CreateAccessCodeRequest
    {
        [MaxLength(100)]
        public string? Label { get; set; }

        /// <summary>每日 token 上限；0 = 不限制；不傳則用系統預設值</summary>
        [Range(0, int.MaxValue, ErrorMessage = "每日上限不可為負數")]
        public int? DailyTokenLimit { get; set; }

        /// <summary>到期時間；null = 永不過期</summary>
        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>更新存取碼（標籤 / 每日上限 / 到期）</summary>
    public class UpdateAccessCodeRequest
    {
        [MaxLength(100)]
        public string? Label { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "每日上限不可為負數")]
        public int DailyTokenLimit { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }

    /// <summary>啟用 / 停用存取碼</summary>
    public class SetAccessCodeActiveRequest
    {
        public bool IsActive { get; set; }
    }
}
