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
    private readonly ILogger<DepartmentsController> _logger;
    
    public DepartmentsController(AppDbContext db, ILogger<DepartmentsController> logger) 
    { 
        _db = db; 
        _logger = logger;
    }

    /// <summary>
    /// Get all departments
    /// </summary>
    /// <returns>List of all departments with user count</returns>
    [HttpGet]
    [SwaggerOperation(Summary = "قائمة جميع الأقسام")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        try
        {
            var departments = await _db.Departments
                .Include(d => d.Users)
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.CreatedAt,
                    UserCount = d.Users.Count
                })
                .AsNoTracking()
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = departments,
                count = departments.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving departments");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get department by ID
    /// </summary>
    /// <param name="id">Department ID</param>
    /// <returns>Department details with users</returns>
    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "تفاصيل قسم محدد")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> GetById(int id)
    {
        try
        {
            var department = await _db.Departments
                .Include(d => d.Users)
                .Where(d => d.Id == id)
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.CreatedAt,
                    Users = d.Users.Select(u => new
                    {
                        u.Id,
                        u.Email,
                        u.FullName,
                        u.Role
                    })
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (department == null)
            {
                return NotFound(new { success = false, message = "Department not found" });
            }

            return Ok(new { success = true, data = department });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving department with ID {DepartmentId}", id);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a new department
    /// </summary>
    /// <param name="req">Department creation request</param>
    /// <returns>Created department</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إنشاء قسم جديد (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> Create([FromBody] CreateDepartmentRequest req)
    {
        try
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            
            // Check if department name already exists (case-insensitive)
            bool exists = await _db.Departments.AnyAsync(d => d.Name.ToLower() == req.Name.ToLower());
            if (exists) 
            {
                return BadRequest(new 
                { 
                    success = false,
                    message = "Department name already exists",
                    errors = new { Name = new[] { "A department with this name already exists" } }
                });
            }
            
            var department = new Department 
            { 
                Name = req.Name.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            
            _db.Departments.Add(department);
            await _db.SaveChangesAsync();
            
            var result = new
            {
                department.Id,
                department.Name,
                department.CreatedAt
            };
            
            return CreatedAtAction(
                nameof(GetById),
                new { id = department.Id },
                new { success = true, message = "Department created successfully", data = result }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating department");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }
}