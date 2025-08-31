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
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) { _db = db; }

    // GET /api/users
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إرجاع جميع المستخدمين (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .Include(u => u.Department)
            .AsNoTracking()
            .Select(u => new {
                u.Id,
                u.Email,
                u.FullName,
                u.Role,
                u.DepartmentId,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                u.EmailConfirmed,
                u.CreatedAt
            })
            .ToListAsync();
        return Ok(users);
    }

    // GET /api/users/{id}
    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إرجاع مستخدم واحد بالمعرّف (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _db.Users
            .Include(u => u.Department)
            .AsNoTracking()
            .Select(u => new {
                u.Id,
                u.Email,
                u.FullName,
                u.Role,
                u.DepartmentId,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                u.EmailConfirmed,
                u.CreatedAt
            })
            .FirstOrDefaultAsync(u => u.Id == id);
        
        if (user == null) return NotFound(new { message = "User not found" });
        return Ok(user);
    }

    // GET /api/users/me
    [HttpGet("me")]
    [SwaggerOperation(Summary = "تفاصيل حسابي (المستخدم الحالي)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
        var currentUserId = int.Parse(userIdClaim);

        var me = await _db.Users
            .Include(u => u.Department)
            .AsNoTracking()
            .Where(u => u.Id == currentUserId)
            .Select(u => new {
                u.Id,
                u.Email,
                u.FullName,
                u.Role,
                u.DepartmentId,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                u.EmailConfirmed,
                u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (me == null) return NotFound(new { message = "User not found" });
        return Ok(me);
    }

    // POST /api/users
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إنشاء مستخدم جديد (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (req.Role == "Teacher" && !req.DepartmentId.HasValue)
            return BadRequest(new { message = "DepartmentId is required for Teacher" });

        // Validate department exists if provided
        if (req.DepartmentId.HasValue)
        {
            var departmentExists = await _db.Departments.AnyAsync(d => d.Id == req.DepartmentId.Value);
            if (!departmentExists)
                return BadRequest(new { message = "Department not found" });
        }

        bool exists = await _db.Users.AnyAsync(u => u.Email == req.Email);
        if (exists) return BadRequest(new { message = "Email already in use" });

        var user = new User
        {
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password!),
            FullName = req.FullName,
            Role = req.Role,
            DepartmentId = req.DepartmentId,
            EmailConfirmed = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        
        // Load department for response
        await _db.Entry(user).Reference(u => u.Department).LoadAsync();
        
        var response = new {
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.DepartmentId,
            DepartmentName = user.Department?.Name,
            user.EmailConfirmed,
            user.CreatedAt
        };
        
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, response);
    }

    // PUT /api/users/{id}
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "تحديث بيانات مستخدم (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "User not found" });

        // Check if email already exists for another user
        if (!string.IsNullOrEmpty(req.Email) && req.Email != user.Email)
        {
            if (await _db.Users.AnyAsync(u => u.Email == req.Email && u.Id != id))
                return BadRequest(new { message = "Email already exists" });
        }

        // Validate department exists if provided
        if (req.DepartmentId.HasValue)
        {
            var departmentExists = await _db.Departments.AnyAsync(d => d.Id == req.DepartmentId.Value);
            if (!departmentExists)
                return BadRequest(new { message = "Department not found" });
        }

        user.FullName = req.FullName ?? user.FullName;
        user.Email = req.Email ?? user.Email;
        user.Role = req.Role ?? user.Role;
        user.DepartmentId = req.DepartmentId.HasValue ? req.DepartmentId : user.DepartmentId;

        try
        {
            await _db.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _db.Users.AnyAsync(e => e.Id == id))
                return NotFound();
            throw;
        }
    }

    // DELETE /api/users/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "حذف مستخدم (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound(new { message = "User not found" });

        // Prevent admin from deleting themselves
        var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (user.Id == currentUserId)
            return BadRequest(new { message = "Cannot delete your own account" });

        // Check if user has bookings
        var hasBookings = await _db.Bookings.AnyAsync(b => b.UserId == id);
        if (hasBookings)
            return BadRequest(new { message = "Cannot delete user with existing bookings" });

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/users/department/{departmentId}
    [HttpGet("department/{departmentId:int}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إرجاع المستخدمين حسب القسم (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetByDepartment(int departmentId)
    {
        var departmentExists = await _db.Departments.AnyAsync(d => d.Id == departmentId);
        if (!departmentExists) return NotFound(new { message = "Department not found" });

        var users = await _db.Users
            .Include(u => u.Department)
            .Where(u => u.DepartmentId == departmentId)
            .AsNoTracking()
            .Select(u => new {
                u.Id,
                u.Email,
                u.FullName,
                u.Role,
                u.DepartmentId,
                DepartmentName = u.Department!.Name,
                u.EmailConfirmed,
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    // GET /api/users/role/{role}
    [HttpGet("role/{role}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إرجاع المستخدمين حسب الدور (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetByRole(string role)
    {
        var validRoles = new[] { "Admin", "Teacher", "Staff" };
        if (!validRoles.Contains(role))
            return BadRequest(new { message = "Invalid role. Valid roles: Admin, Teacher, Staff" });

        var users = await _db.Users
            .Include(u => u.Department)
            .Where(u => u.Role == role)
            .AsNoTracking()
            .Select(u => new {
                u.Id,
                u.Email,
                u.FullName,
                u.Role,
                u.DepartmentId,
                DepartmentName = u.Department != null ? u.Department.Name : null,
                u.EmailConfirmed,
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }
}