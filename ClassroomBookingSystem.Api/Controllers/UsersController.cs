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
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) { _db = db; }

    // GET /api/users
    [HttpGet]
    [SwaggerOperation(Summary = "إرجاع جميع المستخدمين (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users.AsNoTracking().ToListAsync();
        return Ok(users);
    }

    // GET /api/users/{id}
    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "إرجاع مستخدم واحد بالمعرّف (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        return Ok(user);
    }

    // POST /api/users
    [HttpPost]
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

        bool exists = await _db.Users.AnyAsync(u => u.Email == req.Email);
        if (exists) return BadRequest(new { message = "Email already in use" });

        var user = new User
        {
            Email = req.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password!),
            FullName = req.FullName,
            Role = req.Role,
            DepartmentId = req.DepartmentId
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return Ok(user);
    }

    // PUT /api/users/{id}
    [HttpPut("{id:int}")]
    [SwaggerOperation(Summary = "تحديث بيانات مستخدم (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.FullName = req.FullName ?? user.FullName;
        user.Email = req.Email ?? user.Email;
        user.Role = req.Role ?? user.Role;
        user.DepartmentId = req.DepartmentId.HasValue ? req.DepartmentId : user.DepartmentId;

        await _db.SaveChangesAsync();
        return Ok(user);
    }

    // DELETE /api/users/{id}
    [HttpDelete("{id:int}")]
    [SwaggerOperation(Summary = "حذف مستخدم (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return NotFound();
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return Ok();
    }
}