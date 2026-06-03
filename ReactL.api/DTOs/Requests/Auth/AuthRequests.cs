using System.ComponentModel.DataAnnotations;

namespace ReactL.api.DTOs.Requests.Auth
{
    /// <summary>註冊請求</summary>
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Email 為必填")]
        [EmailAddress(ErrorMessage = "Email 格式不正確")]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "密碼為必填")]
        [MinLength(8, ErrorMessage = "密碼至少 8 個字元")]
        [MaxLength(100)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "顯示名稱為必填")]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>登入請求</summary>
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email 為必填")]
        [EmailAddress(ErrorMessage = "Email 格式不正確")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "密碼為必填")]
        public string Password { get; set; } = string.Empty;
    }
}
