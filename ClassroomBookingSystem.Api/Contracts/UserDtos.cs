using System.ComponentModel.DataAnnotations;

namespace ClassroomBookingSystem.Api.Contracts;

public class CreateUserRequest
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
    [RegularExpression("^(Admin|Teacher|Staff)$")] 
    public string Role { get; set; } = string.Empty;

    public int? DepartmentId { get; set; }
}

public class UpdateUserRequest
{
    [EmailAddress]
    public string? Email { get; set; }

    [MaxLength(200)]
    public string? FullName { get; set; }

    [RegularExpression("^(Admin|Teacher|Staff)$")] 
    public string? Role { get; set; }

    public int? DepartmentId { get; set; }
}