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
    private readonly ILogger<BookingsController> _logger;
    
    public BookingsController(AppDbContext db, ILogger<BookingsController> logger) 
    { 
        _db = db; 
        _logger = logger;
    }

    /// <summary>
    /// Get all bookings (Admin only) or user's own bookings (Teacher)
    /// </summary>
    /// <returns>List of bookings with related data</returns>
    [HttpGet]
    [SwaggerOperation(Summary = "قائمة جميع الحجوزات")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> GetAll([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var actingUserId = int.Parse(userIdClaim);

            var query = _db.Bookings
                .Include(b => b.Room)
                    .ThenInclude(r => r.Building)
                .Include(b => b.Teacher)
                    .ThenInclude(t => t.Department)
                .AsQueryable();

            // Teachers can only see their own bookings
            if (role == "Teacher")
            {
                query = query.Where(b => b.TeacherId == actingUserId);
            }

            if (from.HasValue && to.HasValue)
            {
                if (from.Value >= to.Value)
                {
                    return BadRequest(new { success = false, message = "Invalid time range" });
                }
                query = query.Where(b => from.Value < b.EndsAt && to.Value > b.StartsAt);
            }

            var bookings = await query
                .Select(b => new
                {
                    b.Id,
                    b.Title,
                    b.StartsAt,
                    b.EndsAt,
                    b.Status,
                    b.CreatedAt,
                    Room = new
                    {
                        b.Room.Id,
                        b.Room.Code,
                        b.Room.Capacity,
                        Building = (b.Room.Building == null) ? null : new
                        {
                            b.Room.Building.Id,
                            b.Room.Building.Name
                        }
                    },
                    Teacher = new
                    {
                        b.Teacher.Id,
                        b.Teacher.FullName,
                        b.Teacher.Email,
                        Department = (b.Teacher.Department == null) ? null : new
                        {
                            b.Teacher.Department.Id,
                            b.Teacher.Department.Name
                        }
                    }
                })
                .AsNoTracking()
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = bookings,
                count = bookings.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bookings");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get current teacher's bookings
    /// </summary>
    /// <returns>List of current teacher's bookings</returns>
    [HttpGet("mine")]
    [Authorize(Roles = "Teacher")]
    [SwaggerOperation(Summary = "حجوزاتي (للمعلم الحالي)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> GetMine()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var teacherId = int.Parse(userIdClaim);
            
            var bookings = await _db.Bookings
                .Include(b => b.Room)
                    .ThenInclude(r => r.Building)
                .Where(b => b.TeacherId == teacherId)
                .Select(b => new
                {
                    b.Id,
                    b.Title,
                    b.StartsAt,
                    b.EndsAt,
                    b.Status,
                    b.CreatedAt,
                    Room = new
                    {
                        b.Room.Id,
                        b.Room.Code,
                        b.Room.Capacity,
                        Building = (b.Room.Building == null) ? null : new
                        {
                            b.Room.Building.Id,
                            b.Room.Building.Name
                        }
                    }
                })
                .AsNoTracking()
                .ToListAsync();
                
            return Ok(new
            {
                success = true,
                data = bookings,
                count = bookings.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving teacher's bookings");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get booking by ID
    /// </summary>
    /// <param name="id">Booking ID</param>
    /// <returns>Booking details with related data</returns>
    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "إرجاع حجز واحد بالمعرّف")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<object>> GetById(int id)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var actingUserId = int.Parse(userIdClaim);

            var booking = await _db.Bookings
                .Include(b => b.Room)
                    .ThenInclude(r => r.Building)
                .Include(b => b.Teacher)
                    .ThenInclude(t => t.Department)
                .Where(b => b.Id == id)
                .Select(b => new
                {
                    b.Id,
                    b.Title,
                    b.StartsAt,
                    b.EndsAt,
                    b.Status,
                    b.CreatedAt,
                    Room = new
                    {
                        b.Room.Id,
                        b.Room.Code,
                        b.Room.Capacity,
                        Building = (b.Room.Building == null) ? null : new
                        {
                            b.Room.Building.Id,
                            b.Room.Building.Name
                        }
                    },
                    Teacher = new
                    {
                        b.Teacher.Id,
                        b.Teacher.FullName,
                        b.Teacher.Email,
                        Department = (b.Teacher.Department == null) ? null : new
                        {
                            b.Teacher.Department.Id,
                            b.Teacher.Department.Name
                        }
                    }
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (booking == null)
            {
                return NotFound(new { success = false, message = "Booking not found" });
            }

            // Teachers can only view their own bookings
            if (role == "Teacher" && booking.Teacher.Id != actingUserId)
            {
                return Forbid();
            }

            return Ok(new { success = true, data = booking });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving booking with ID {BookingId}", id);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a new booking (Teacher)
    /// </summary>
    /// <param name="request">Booking creation request</param>
    /// <returns>Created booking details</returns>
    [HttpPost]
    [Authorize(Roles = "Teacher")]
    [SwaggerOperation(Summary = "إنشاء حجز جديد (للمعلم)")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<object>> Create([FromBody] CreateBookingTeacherRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var actingUserId = int.Parse(userIdClaim);

            // التحقق من وجود الغرفة
            var room = await _db.Rooms
                .Include(r => r.Building)
                .FirstOrDefaultAsync(r => r.Id == request.RoomId);
            if (room == null)
            {
                return BadRequest(new { success = false, message = "Room not found" });
            }
            if (!room.IsActive)
            {
                return BadRequest(new { success = false, message = "Room is not active" });
            }

            // التحقق من طول العنوان
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            {
                return BadRequest(new { success = false, message = "Title is required and must be 200 characters or less" });
            }

            // التحقق من أن الحجز ليس في الماضي
            if (request.StartsAt <= DateTime.UtcNow)
            {
                return BadRequest(new { success = false, message = "Booking cannot be in the past" });
            }

            // التحقق من أن وقت الانتهاء بعد وقت البداية
            if (request.EndsAt <= request.StartsAt)
            {
                return BadRequest(new { success = false, message = "End time must be after start time" });
            }

            // التحقق من عدم وجود تداخل في الأوقات
            bool overlap = await _db.Bookings.AnyAsync(b => 
                b.RoomId == request.RoomId && 
                b.Status != BookingStatus.Cancelled && 
                request.StartsAt < b.EndsAt && 
                request.EndsAt > b.StartsAt);
            if (overlap)
            {
                return BadRequest(new { success = false, message = "Time slot is already booked" });
            }

            var booking = new Booking
            {
                RoomId = request.RoomId,
                TeacherId = actingUserId,
                Title = request.Title,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                Status = BookingStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();

            // إرجاع البيانات مع التفاصيل
            var teacherDetails = await _db.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Id == actingUserId);

            var createdBooking = new
            {
                booking.Id,
                booking.Title,
                booking.StartsAt,
                booking.EndsAt,
                booking.Status,
                booking.CreatedAt,
                Room = new
                {
                    room.Id,
                    room.Code,
                    room.Capacity,
                    Building = (room.Building == null) ? null : new
                    {
                        room.Building.Id,
                        room.Building.Name
                    }
                },
                Teacher = (teacherDetails == null) ? null : new
                {
                    teacherDetails.Id,
                    teacherDetails.FullName,
                    teacherDetails.Email,
                    Department = (teacherDetails.Department == null) ? null : new
                    {
                        teacherDetails.Department.Id,
                        teacherDetails.Department.Name
                    }
                }
            };

            _logger.LogInformation("Booking created with ID {BookingId} by teacher {UserId}", booking.Id, actingUserId);
            return CreatedAtAction(nameof(GetById), new { id = booking.Id }, new { success = true, data = createdBooking });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking (teacher)");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a new booking (Admin)
    /// </summary>
    /// <param name="request">Booking creation request</param>
    /// <returns>Created booking details</returns>
    [HttpPost("admin")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إنشاء حجز جديد (للأدمن)")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<object>> CreateAdmin([FromBody] CreateBookingAdminRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var actingUserId = int.Parse(userIdClaim);

            // التحقق من وجود الغرفة
            var room = await _db.Rooms
                .Include(r => r.Building)
                .FirstOrDefaultAsync(r => r.Id == request.RoomId);
            if (room == null)
            {
                return BadRequest(new { success = false, message = "Room not found" });
            }
            if (!room.IsActive)
            {
                return BadRequest(new { success = false, message = "Room is not active" });
            }

            // TeacherId مطلوب للأدمن
            var teacher = await _db.Users
                .Include(u => u.Department)
                .FirstOrDefaultAsync(u => u.Id == request.TeacherId && u.Role == "Teacher");
            if (teacher == null)
            {
                return BadRequest(new { success = false, message = "Teacher not found" });
            }

            // التحقق من طول العنوان
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            {
                return BadRequest(new { success = false, message = "Title is required and must be 200 characters or less" });
            }

            // التحقق من أن الحجز ليس في الماضي
            if (request.StartsAt <= DateTime.UtcNow)
            {
                return BadRequest(new { success = false, message = "Booking cannot be in the past" });
            }

            // التحقق من أن وقت الانتهاء بعد وقت البداية
            if (request.EndsAt <= request.StartsAt)
            {
                return BadRequest(new { success = false, message = "End time must be after start time" });
            }

            // التحقق من عدم وجود تداخل في الأوقات
            bool overlap = await _db.Bookings.AnyAsync(b => 
                b.RoomId == request.RoomId && 
                b.Status != BookingStatus.Cancelled && 
                request.StartsAt < b.EndsAt && 
                request.EndsAt > b.StartsAt);
            if (overlap)
            {
                return BadRequest(new { success = false, message = "Time slot is already booked" });
            }

            var booking = new Booking
            {
                RoomId = request.RoomId,
                TeacherId = teacher.Id,
                Title = request.Title,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                Status = BookingStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();

            var createdBooking = new
            {
                booking.Id,
                booking.Title,
                booking.StartsAt,
                booking.EndsAt,
                booking.Status,
                booking.CreatedAt,
                Room = new
                {
                    room.Id,
                    room.Code,
                    room.Capacity,
                    Building = (room.Building == null) ? null : new
                    {
                        room.Building.Id,
                        room.Building.Name
                    }
                },
                Teacher = new
                {
                    teacher.Id,
                    teacher.FullName,
                    teacher.Email,
                    Department = (teacher.Department == null) ? null : new
                    {
                        teacher.Department.Id,
                        teacher.Department.Name
                    }
                }
            };

            _logger.LogInformation("Booking created with ID {BookingId} by admin {UserId}", booking.Id, actingUserId);
            return CreatedAtAction(nameof(GetById), new { id = booking.Id }, new { success = true, data = createdBooking });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking (admin)");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Update an existing booking
    /// </summary>
    /// <param name="id">Booking ID</param>
    /// <param name="request">Booking update request</param>
    /// <returns>Updated booking details</returns>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    [SwaggerOperation(Summary = "تحديث حجز موجود")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<object>> Update(int id, [FromBody] UpdateBookingRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var actingUserId = int.Parse(userIdClaim);

            var booking = await _db.Bookings
                .Include(b => b.Room)
                    .ThenInclude(r => r.Building)
                .Include(b => b.Teacher)
                    .ThenInclude(t => t.Department)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound(new { success = false, message = "Booking not found" });
            }

            // التحقق من الصلاحيات
            if (role == "Teacher" && booking.TeacherId != actingUserId)
            {
                return Forbid();
            }

            // التحقق من حالة الحجز
            if (booking.Status == BookingStatus.Cancelled)
            {
                return BadRequest(new { success = false, message = "Cannot update cancelled booking" });
            }

            // التحقق من وجود الغرفة
            var room = await _db.Rooms
                .Include(r => r.Building)
                .FirstOrDefaultAsync(r => r.Id == request.RoomId);
            if (room == null)
            {
                return BadRequest(new { success = false, message = "Room not found" });
            }
            if (!room.IsActive)
            {
                return BadRequest(new { success = false, message = "Room is not active" });
            }

            // التحقق من طول العنوان
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            {
                return BadRequest(new { success = false, message = "Title is required and must be 200 characters or less" });
            }

            // التحقق من أن الحجز ليس في الماضي
            if (request.StartsAt <= DateTime.UtcNow)
            {
                return BadRequest(new { success = false, message = "Booking cannot be in the past" });
            }

            // التحقق من أن وقت الانتهاء بعد وقت البداية
            if (request.EndsAt <= request.StartsAt)
            {
                return BadRequest(new { success = false, message = "End time must be after start time" });
            }

            // التحقق من عدم وجود تداخل في الأوقات (باستثناء الحجز الحالي)
            var hasOverlap = await _db.Bookings.AnyAsync(b =>
                b.Id != id &&
                b.RoomId == request.RoomId &&
                b.Status != BookingStatus.Cancelled &&
                request.StartsAt < b.EndsAt &&
                request.EndsAt > b.StartsAt);

            if (hasOverlap)
            {
                return BadRequest(new { success = false, message = "Time slot is already booked" });
            }

            // تحديث TeacherId إذا كان المستخدم أدمن
            if (role == "Admin" && request.TeacherId.HasValue)
            {
                var teacher = await _db.Users
                    .Include(u => u.Department)
                    .FirstOrDefaultAsync(u => u.Id == request.TeacherId.Value && u.Role == "Teacher");
                if (teacher == null)
                {
                    return BadRequest(new { success = false, message = "Teacher not found" });
                }
                booking.TeacherId = request.TeacherId.Value;
                booking.Teacher = teacher;
            }

            booking.RoomId = request.RoomId;
            booking.Room = room;
            booking.Title = request.Title;
            booking.StartsAt = request.StartsAt;
            booking.EndsAt = request.EndsAt;

            await _db.SaveChangesAsync();

            var updatedBooking = new
            {
                booking.Id,
                booking.Title,
                booking.StartsAt,
                booking.EndsAt,
                booking.Status,
                booking.CreatedAt,
                Room = new
                {
                    booking.Room.Id,
                    booking.Room.Code,
                    booking.Room.Capacity,
                    Building = new
                    {
                        booking.Room.Building.Id,
                        booking.Room.Building.Name
                    }
                },
                Teacher = new
                {
                    booking.Teacher.Id,
                    booking.Teacher.FullName,
                    booking.Teacher.Email,
                    Department = new
                    {
                        booking.Teacher.Department.Id,
                        booking.Teacher.Department.Name
                    }
                }
            };

            _logger.LogInformation("Booking {BookingId} updated by user {UserId}", id, actingUserId);
            return Ok(new { success = true, data = updatedBooking });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating booking with ID {BookingId}", id);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Cancel a booking (change status to Cancelled)
    /// </summary>
    /// <param name="id">Booking ID</param>
    /// <returns>Cancellation confirmation</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Teacher,Admin")]
    [SwaggerOperation(Summary = "إلغاء حجز (تغيير الحالة إلى Cancelled)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> Delete(int id)
    {
        try
        {
            var booking = await _db.Bookings
                .Include(b => b.Room)
                    .ThenInclude(r => r.Building)
                .Include(b => b.Teacher)
                    .ThenInclude(t => t.Department)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound(new { success = false, message = "Booking not found" });
            }

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var actingUserId = int.Parse(userIdClaim);

            // التحقق من الصلاحيات
            if (role == "Teacher" && booking.TeacherId != actingUserId)
            {
                return Forbid();
            }

            // التحقق من حالة الحجز
            if (booking.Status == BookingStatus.Cancelled)
            {
                return BadRequest(new { success = false, message = "Booking is already cancelled" });
            }

            booking.Status = BookingStatus.Cancelled;
            await _db.SaveChangesAsync();

            var cancelledBooking = new
            {
                booking.Id,
                booking.Title,
                booking.StartsAt,
                booking.EndsAt,
                booking.Status,
                booking.CreatedAt,
                Room = new
                {
                    booking.Room.Id,
                    booking.Room.Code,
                    booking.Room.Capacity,
                    Building = new
                    {
                        booking.Room.Building.Id,
                        booking.Room.Building.Name
                    }
                },
                Teacher = new
                {
                    booking.Teacher.Id,
                    booking.Teacher.FullName,
                    booking.Teacher.Email,
                    Department = new
                    {
                        booking.Teacher.Department.Id,
                        booking.Teacher.Department.Name
                    }
                }
            };

            _logger.LogInformation("Booking {BookingId} cancelled by user {UserId}", id, actingUserId);
            return Ok(new { success = true, message = "Booking cancelled successfully", data = cancelledBooking });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling booking with ID {BookingId}", id);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get a teacher schedule within an optional time range
    /// </summary>
    /// <param name="teacherId">Teacher ID (Admin can view any; Teacher only their own)</param>
    /// <param name="from">Start time (optional)</param>
    /// <param name="to">End time (optional)</param>
    /// <returns>List of bookings for the teacher</returns>
    [HttpGet("teacher/{teacherId:int}/schedule")]
    [SwaggerOperation(Summary = "جدول المدرس خلال فترة")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<object>> GetTeacherSchedule(int teacherId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var actingUserId = int.Parse(userIdClaim);

            // Teachers can only view their own schedule
            if (role == "Teacher" && actingUserId != teacherId)
            {
                return Forbid();
            }

            // Validate teacher exists and is Teacher role
            var teacher = await _db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == teacherId && u.Role == "Teacher");
            if (teacher == null)
            {
                return NotFound(new { success = false, message = "Teacher not found" });
            }

            if (from.HasValue && to.HasValue && from.Value >= to.Value)
            {
                return BadRequest(new { success = false, message = "Invalid time range" });
            }

            var query = _db.Bookings
                .Include(b => b.Room)
                    .ThenInclude(r => r.Building)
                .Where(b => b.TeacherId == teacherId && b.Status != BookingStatus.Cancelled)
                .AsQueryable();

            if (from.HasValue && to.HasValue)
            {
                query = query.Where(b => from.Value < b.EndsAt && to.Value > b.StartsAt);
            }

            var bookings = await query
                .Select(b => new
                {
                    b.Id,
                    b.Title,
                    b.StartsAt,
                    b.EndsAt,
                    b.Status,
                    b.CreatedAt,
                    Room = new
                    {
                        b.Room.Id,
                        b.Room.Code,
                        b.Room.Capacity,
                        Building = b.Room.Building == null ? null : new
                        {
                            b.Room.Building.Id,
                            b.Room.Building.Name
                        }
                    }
                })
                .AsNoTracking()
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = bookings,
                count = bookings.Count,
                teacher = new
                {
                    teacher.Id,
                    teacher.FullName,
                    teacher.Email,
                    Department = teacher.Department == null ? null : new { teacher.Department.Id, teacher.Department.Name }
                },
                timeRange = new { from, to }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedule for teacher {TeacherId}", teacherId);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }
}