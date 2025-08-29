using ClassroomBookingSystem.Api.Contracts;
using ClassroomBookingSystem.Core.Entities;
using ClassroomBookingSystem.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;

namespace ClassroomBookingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public BookingsController(AppDbContext db) { _db = db; }

    // GET /api/bookings
    [HttpGet]
    [SwaggerOperation(Summary = "قائمة جميع الحجوزات")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.Bookings.AsNoTracking().ToListAsync();
        return Ok(list);
    }

    // GET /api/bookings/mine
    [HttpGet("mine")]
    [Authorize(Roles = "Teacher")]
    [SwaggerOperation(Summary = "حجوزاتي (للمعلم الحالي)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
        var teacherId = int.Parse(userIdClaim);
        var list = await _db.Bookings.Where(b => b.TeacherId == teacherId).AsNoTracking().ToListAsync();
        return Ok(list);
    }

    // GET /api/bookings/{id}
    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "إرجاع حجز واحد بالمعرّف")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(int id)
    {
        var item = await _db.Bookings.FindAsync(id);
        if (item == null) return NotFound();
        return Ok(item);
    }

    // POST /api/bookings
    [HttpPost]
    [Authorize(Roles = "Teacher,Admin")]
    [SwaggerOperation(Summary = "إنشاء حجز جديد (المعلم لنفسه، أو أدمن)" )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
        var actingUserId = int.Parse(userIdClaim);

        var room = await _db.Rooms.FindAsync(req.RoomId);
        if (room == null) return BadRequest(new { message = "RoomId not found" });
        if (!room.IsActive) return BadRequest(new { message = "Room is inactive" });

        int teacherId;
        if (role == "Teacher")
        {
            teacherId = actingUserId; // تجاهل TeacherId القادم من الطلب
        }
        else // Admin
        {
            var teacher = await _db.Users.FindAsync(req.TeacherId);
            if (teacher == null || teacher.Role != "Teacher")
                return BadRequest(new { message = "TeacherId must belong to a Teacher" });
            teacherId = teacher.Id;
        }

        if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length > 200)
            return BadRequest(new { message = "Title is required and must be <= 200" });

        if (req.StartsAt < DateTime.UtcNow)
            return BadRequest(new { message = "StartsAt must be in the future" });

        if (req.EndsAt <= req.StartsAt)
            return BadRequest(new { message = "EndsAt must be > StartsAt" });

        bool overlap = await _db.Bookings.AnyAsync(b => b.RoomId == req.RoomId && b.Status != BookingStatus.Cancelled && req.StartsAt < b.EndsAt && req.EndsAt > b.StartsAt);
        if (overlap) return BadRequest(new { message = "Room already booked in this period." });

        var booking = new Booking
        {
            RoomId = req.RoomId,
            TeacherId = teacherId,
            Title = req.Title,
            StartsAt = req.StartsAt,
            EndsAt = req.EndsAt,
            Status = BookingStatus.Pending
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();
        return Ok(booking);
    }

    // PUT /api/bookings/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    [SwaggerOperation(Summary = "تحديث حجز قائم مع فحص التداخل")] 
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBookingRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
        var actingUserId = int.Parse(userIdClaim);

        var booking = await _db.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        // المعلّم لا يستطيع تعديل حجوزات غيره
        if (role == "Teacher" && booking.TeacherId != actingUserId)
            return Forbid();

        var room = await _db.Rooms.FindAsync(req.RoomId);
        if (room == null) return BadRequest(new { message = "RoomId not found" });
        if (!room.IsActive) return BadRequest(new { message = "Room is inactive" });

        if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length > 200)
            return BadRequest(new { message = "Title is required and must be <= 200" });

        if (req.StartsAt < DateTime.UtcNow)
            return BadRequest(new { message = "StartsAt must be in the future" });

        if (req.EndsAt <= req.StartsAt)
            return BadRequest(new { message = "EndsAt must be > StartsAt" });

        bool overlap = await _db.Bookings.AnyAsync(b => b.Id != id && b.RoomId == req.RoomId && b.Status != BookingStatus.Cancelled && req.StartsAt < b.EndsAt && req.EndsAt > b.StartsAt);
        if (overlap) return BadRequest(new { message = "Room already booked in this period." });

        booking.RoomId = req.RoomId;
        booking.Title = req.Title;
        booking.StartsAt = req.StartsAt;
        booking.EndsAt = req.EndsAt;

        if (role == "Admin")
        {
            // يسمح للأدمن بتغيير المعلّم إن أراد، مع التحقق أنه Teacher
            if (req.TeacherId != booking.TeacherId)
            {
                var teacher = await _db.Users.FindAsync(req.TeacherId);
                if (teacher == null || teacher.Role != "Teacher")
                    return BadRequest(new { message = "TeacherId must belong to a Teacher" });
                booking.TeacherId = teacher.Id;
            }
        }
        else
        {
            // المعلّم: تثبيت TeacherId إلى صاحب التوكن
            booking.TeacherId = actingUserId;
        }

        await _db.SaveChangesAsync();
        return Ok(booking);
    }

    // DELETE /api/bookings/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    [SwaggerOperation(Summary = "إلغاء حجز (تغيير الحالة إلى Cancelled)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
        var actingUserId = int.Parse(userIdClaim);

        var booking = await _db.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        if (role == "Teacher" && booking.TeacherId != actingUserId)
            return Forbid();

        // سياسة: نغيّر الحالة إلى Cancelled بدلاً من الحذف الفعلي.
        booking.Status = BookingStatus.Cancelled;
        await _db.SaveChangesAsync();
        return Ok();
    }
}