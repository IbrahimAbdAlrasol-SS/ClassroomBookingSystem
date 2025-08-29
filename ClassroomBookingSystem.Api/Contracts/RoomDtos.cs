using System.ComponentModel.DataAnnotations;

namespace ClassroomBookingSystem.Api.Contracts;

public class CreateRoomRequest
{
    [Required]
    public int BuildingId { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Capacity { get; set; }
}

public class AvailableRoomsQuery
{
    [Required]
    public DateTime From { get; set; }

    [Required]
    public DateTime To { get; set; }
}