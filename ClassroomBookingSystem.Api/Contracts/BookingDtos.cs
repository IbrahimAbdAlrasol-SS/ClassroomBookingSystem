using System.ComponentModel.DataAnnotations;

namespace ClassroomBookingSystem.Api.Contracts;

public class CreateBookingTeacherRequest
{
    [Required]
    public int RoomId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime StartsAt { get; set; }

    [Required]
    public DateTime EndsAt { get; set; }
}

public class CreateBookingAdminRequest
{
    [Required]
    public int RoomId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime StartsAt { get; set; }

    [Required]
    public DateTime EndsAt { get; set; }

    [Required]
    public int TeacherId { get; set; }
}

public class UpdateBookingRequest
{
    [Required]
    public int RoomId { get; set; }

    [MaxLength(200)]
    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime StartsAt { get; set; }

    [Required]
    public DateTime EndsAt { get; set; }

    // للأدمن فقط: يمكنه نقل الحجز إلى معلّم آخر
    public int? TeacherId { get; set; }
}