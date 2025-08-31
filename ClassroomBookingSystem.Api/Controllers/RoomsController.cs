using ClassroomBookingSystem.Api.Contracts;
using ClassroomBookingSystem.Core.Entities;
using ClassroomBookingSystem.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace ClassroomBookingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RoomsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<RoomsController> _logger;
    
    public RoomsController(AppDbContext db, ILogger<RoomsController> logger) 
    { 
        _db = db; 
        _logger = logger;
    }

    /// <summary>
    /// Get all rooms
    /// </summary>
    /// <returns>List of all rooms with building information</returns>
    [HttpGet]
    [SwaggerOperation(Summary = "قائمة جميع القاعات")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        try
        {
            var rooms = await _db.Rooms
                .Include(r => r.Building)
                .Select(r => new
                {
                    r.Id,
                    r.Code,
                    r.Capacity,
                    r.RoomType,
                    r.IsActive,
                    r.CreatedAt,
                    Building = new
                    {
                        r.Building.Id,
                        r.Building.Name
                    }
                })
                .AsNoTracking()
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = rooms,
                count = rooms.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving rooms");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get room by ID
    /// </summary>
    /// <param name="id">Room ID</param>
    /// <returns>Room details with building information</returns>
    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "تفاصيل قاعة محددة")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> GetById(int id)
    {
        try
        {
            var room = await _db.Rooms
                .Include(r => r.Building)
                .Where(r => r.Id == id)
                .Select(r => new
                {
                    r.Id,
                    r.Code,
                    r.Capacity,
                    r.RoomType,
                    r.IsActive,
                    r.CreatedAt,
                    Building = new
                    {
                        r.Building.Id,
                        r.Building.Name
                    }
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (room == null)
            {
                return NotFound(new { success = false, message = "Room not found" });
            }

            return Ok(new { success = true, data = room });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving room with ID {RoomId}", id);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a new room
    /// </summary>
    /// <param name="req">Room creation request</param>
    /// <returns>Created room</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إضافة قاعة جديدة (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> Create([FromBody] CreateRoomRequest req)
    {
        try
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            // Validate building exists
            var building = await _db.Buildings.FindAsync(req.BuildingId);
            if (building == null) 
            {
                return BadRequest(new 
                { 
                    success = false,
                    message = "Building not found",
                    errors = new { BuildingId = new[] { "The specified building does not exist" } }
                });
            }

            // Check for duplicate room code in the same building
            bool duplicate = await _db.Rooms.AnyAsync(r => r.BuildingId == req.BuildingId && r.Code.ToLower() == req.Code.ToLower());
            if (duplicate) 
            {
                return BadRequest(new 
                { 
                    success = false,
                    message = "Room code already exists in building",
                    errors = new { Code = new[] { "A room with this code already exists in the specified building" } }
                });
            }

            var room = new Room 
            { 
                BuildingId = req.BuildingId, 
                Code = req.Code.Trim(), 
                Capacity = req.Capacity,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            
            _db.Rooms.Add(room);
            await _db.SaveChangesAsync();
            
            // Load building info for response
            await _db.Entry(room).Reference(r => r.Building).LoadAsync();
            
            var result = new
            {
                room.Id,
                room.Code,
                room.Capacity,
                room.RoomType,
                room.IsActive,
                room.CreatedAt,
                Building = new
                {
                    room.Building.Id,
                    room.Building.Name
                }
            };
            
            return CreatedAtAction(
                nameof(GetById),
                new { id = room.Id },
                new { success = true, message = "Room created successfully", data = result }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a room
    /// </summary>
    /// <param name="id">Room ID</param>
    /// <returns>Success or error message</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "حذف قاعة (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var room = await _db.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound(new { success = false, message = "Room not found" });
            }

            // Check if room has existing bookings
            var hasBookings = await _db.Bookings.AnyAsync(b => b.RoomId == id);
            if (hasBookings)
            {
                return BadRequest(new 
                { 
                    success = false, 
                    message = "Cannot delete room with existing bookings",
                    errors = new { Room = new[] { "This room has existing bookings and cannot be deleted" } }
                });
            }

            _db.Rooms.Remove(room);
            await _db.SaveChangesAsync();
            
            _logger.LogInformation("Room {RoomId} deleted successfully", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting room with ID {RoomId}", id);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Activate or deactivate a room
    /// </summary>
    /// <param name="id">Room ID</param>
    /// <param name="request">Activation request</param>
    /// <returns>Updated room status</returns>
    [HttpPatch("{id:int}/active")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "تفعيل/إلغاء تفعيل قاعة (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> SetActive(int id, [FromBody] SetRoomActiveRequest request)
    {
        try
        {
            var room = await _db.Rooms.Include(r => r.Building).FirstOrDefaultAsync(r => r.Id == id);
            if (room == null)
            {
                return NotFound(new { success = false, message = "Room not found" });
            }

            room.IsActive = request.IsActive;
             await _db.SaveChangesAsync();
             
             var result = new
             {
                 room.Id,
                 room.Code,
                 room.Capacity,
                 room.RoomType,
                 room.IsActive,
                 room.CreatedAt,
                 Building = new
                 {
                     room.Building.Id,
                     room.Building.Name
                 }
             };
             
             _logger.LogInformation("Room {RoomId} activation status changed to {IsActive}", id, request.IsActive);
            return Ok(new { success = true, message = "Room status updated successfully", data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating room activation status for ID {RoomId}", id);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get available rooms for a specific time period
    /// </summary>
    /// <param name="from">Start time</param>
    /// <param name="to">End time</param>
    /// <returns>List of available rooms</returns>
    [HttpGet("available")]
    [SwaggerOperation(Summary = "قائمة القاعات المتاحة خلال فترة محددة")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> Available([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        try
        {
            if (from >= to) 
            {
                return BadRequest(new 
                { 
                    success = false, 
                    message = "Invalid time range",
                    errors = new { From = new[] { "Start time must be before end time" } }
                });
            }

            if (from < DateTime.UtcNow)
            {
                return BadRequest(new 
                { 
                    success = false, 
                    message = "Invalid time range",
                    errors = new { From = new[] { "Start time cannot be in the past" } }
                });
            }

            // Get room IDs that are booked during the requested time period
            var overlappingRoomIds = await _db.Bookings
                .Where(b => b.Status != BookingStatus.Cancelled && from < b.EndsAt && to > b.StartsAt)
                .Select(b => b.RoomId)
                .Distinct()
                .ToListAsync();

            // Get available rooms (active and not booked)
            var available = await _db.Rooms
                .Include(r => r.Building)
                .Where(r => r.IsActive && !overlappingRoomIds.Contains(r.Id))
                .Select(r => new
                {
                    r.Id,
                    r.Code,
                    r.Capacity,
                    r.RoomType,
                    r.IsActive,
                    Building = new
                    {
                        r.Building.Id,
                        r.Building.Name
                    }
                })
                .AsNoTracking()
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = available,
                count = available.Count,
                timeRange = new
                {
                    from = from,
                    to = to
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available rooms for time range {From} to {To}", from, to);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }
}