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
public class DepartmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    public DepartmentsController(AppDbContext db) { _db = db; }

    // GET /api/departments
    [HttpGet]
    [SwaggerOperation(Summary = "قائمة جميع الأقسام")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll()
    {
        var deps = await _db.Departments.AsNoTracking().ToListAsync();
        return Ok(deps);
    }

    // POST /api/departments
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إنشاء قسم جديد (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        bool exists = await _db.Departments.AnyAsync(d => d.Name == req.Name);
        if (exists) return BadRequest(new { message = "Department name already exists" });
        var dep = new Department { Name = req.Name };
        _db.Departments.Add(dep);
        await _db.SaveChangesAsync();
        return Ok(dep);
    }
}