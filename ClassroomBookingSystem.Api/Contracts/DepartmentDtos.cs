using System.ComponentModel.DataAnnotations;

namespace ClassroomBookingSystem.Api.Contracts;

public class CreateDepartmentRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
}