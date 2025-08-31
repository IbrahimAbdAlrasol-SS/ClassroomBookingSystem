using System.ComponentModel.DataAnnotations;

namespace ClassroomBookingSystem.Api.Contracts;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^(Admin|Teacher|Staff)$", ErrorMessage = "Role must be Admin, Teacher, or Staff")] 
    public string Role { get; set; } = string.Empty;

    public int? DepartmentId { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ConfirmEmailRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare("NewPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// إضافة DTO لتغيير كلمة المرور للمستخدم المسجل دخول
public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare("NewPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}