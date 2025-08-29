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
    public RoomsController(AppDbContext db) { _db = db; }

    // GET /api/rooms
    [HttpGet]
    [SwaggerOperation(Summary = "قائمة جميع القاعات")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.Rooms.AsNoTracking().Include(r=>r.Building).ToListAsync();
        return Ok(list);
    }

    // POST /api/rooms
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إضافة قاعة جديدة (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateRoomRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var building = await _db.Buildings.FindAsync(req.BuildingId);
        if (building == null) return BadRequest(new { message = "BuildingId not found" });

        bool duplicate = await _db.Rooms.AnyAsync(r => r.BuildingId == req.BuildingId && r.Code == req.Code);
        if (duplicate) return BadRequest(new { message = "Room code already exists in building" });

        if (req.Capacity <= 0) return BadRequest(new { message = "Capacity must be > 0" });

        var room = new Room { BuildingId = req.BuildingId, Code = req.Code, Capacity = req.Capacity };
        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
        return Ok(room);
    }

    // DELETE /api/rooms/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "حذف قاعة (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room == null) return NotFound();
        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync();
        return Ok();
    }

    // PATCH /api/rooms/{id}/active
    [HttpPatch("{id:int}/active")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "تفعيل/تعطيل قاعة (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActive(int id, [FromQuery] bool active)
    {
        var room = await _db.Rooms.FindAsync(id);
        if (room == null) return NotFound();
        room.IsActive = active;
        await _db.SaveChangesAsync();
        return Ok(room);
    }

    // GET /api/rooms/available?from=...&to=...
    [HttpGet("available")]
    [SwaggerOperation(Summary = "قائمة القاعات المتاحة خلال فترة محددة")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Available([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (from >= to) return BadRequest(new { message = "from must be earlier than to" });

        // rooms that don't have overlapping bookings
        var overlappingRoomIds = await _db.Bookings
            .Where(b => b.Status != BookingStatus.Cancelled && from < b.EndsAt && to > b.StartsAt)
            .Select(b => b.RoomId)
            .Distinct()
            .ToListAsync();

        var available = await _db.Rooms
            .Where(r => r.IsActive && !overlappingRoomIds.Contains(r.Id))
            .ToListAsync();

        return Ok(available);
    }
}