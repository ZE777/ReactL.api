using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.Users
{
    /// <summary>使用者個人資料回應</summary>
    public class UserProfileResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

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
        [Required(ErrorMessage = "目前密碼為必填")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "新密碼為必填")]
        [MinLength(8, ErrorMessage = "新密碼至少 8 個字元")]
        [MaxLength(100)]
        public string NewPassword { get; set; } = string.Empty;
    }
}
