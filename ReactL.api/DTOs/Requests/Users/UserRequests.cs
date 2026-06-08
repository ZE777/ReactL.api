using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.Requests.Users
{
    /// <summary>更新個人資料請求</summary>
    public class UpdateProfileRequest
    {
        [Required(ErrorMessage = "顯示名稱為必填")]
        [MaxLength(100, ErrorMessage = "顯示名稱最多 100 字元")]
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>修改密碼請求</summary>
    public class ChangePasswordRequest
    {
        /// <summary>
        /// 目前密碼。一般改密為必填並會驗證；
        /// 首次登入強制改密（MustChangePassword=true）時可留空，免驗原密碼（剛以原密碼登入，再要一次屬多餘）。
        /// </summary>
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "新密碼為必填")]
        [MinLength(8, ErrorMessage = "新密碼至少 8 個字元")]
        [MaxLength(100)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
