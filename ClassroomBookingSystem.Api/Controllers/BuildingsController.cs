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
public class BuildingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<BuildingsController> _logger;
    
    public BuildingsController(AppDbContext db, ILogger<BuildingsController> logger) 
    { 
        _db = db; 
        _logger = logger;
    }

    /// <summary>
    /// Get all buildings
    /// </summary>
    /// <returns>List of all buildings with room count</returns>
    [HttpGet]
    [SwaggerOperation(Summary = "قائمة جميع المباني")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        try
        {
            var buildings = await _db.Buildings
                .Include(b => b.Rooms)
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.CreatedAt,
                    RoomCount = b.Rooms.Count
                })
                .AsNoTracking()
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = buildings,
                count = buildings.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving buildings");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get building by ID
    /// </summary>
    /// <param name="id">Building ID</param>
    /// <returns>Building details with rooms</returns>
    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "تفاصيل مبنى محدد")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> GetById(int id)
    {
        try
        {
            var building = await _db.Buildings
                .Include(b => b.Rooms)
                .Where(b => b.Id == id)
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.CreatedAt,
                    Rooms = b.Rooms.Select(r => new
                    {
                        r.Id,
                        r.Name,
                        r.Capacity,
                        r.RoomType
                    })
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (building == null)
            {
                return NotFound(new { success = false, message = "Building not found" });
            }

            return Ok(new { success = true, data = building });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving building with ID {BuildingId}", id);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Create a new building
    /// </summary>
    /// <param name="req">Building creation request</param>
    /// <returns>Created building</returns>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إنشاء مبنى جديد (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> Create([FromBody] CreateBuildingRequest req)
    {
        try
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            
            // Check if building name already exists (case-insensitive)
            bool exists = await _db.Buildings.AnyAsync(b => b.Name.ToLower() == req.Name.ToLower());
            if (exists) 
            {
                return BadRequest(new 
                { 
                    success = false,
                    message = "Building name already exists",
                    errors = new { Name = new[] { "A building with this name already exists" } }
                });
            }
            
            var building = new Building 
            { 
                Name = req.Name.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            
            _db.Buildings.Add(building);
            await _db.SaveChangesAsync();
            
            var result = new
            {
                building.Id,
                building.Name,
                building.CreatedAt
            };
            
            return CreatedAtAction(
                nameof(GetById),
                new { id = building.Id },
                new { success = true, message = "Building created successfully", data = result }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating building");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete a building
    /// </summary>
    /// <param name="id">Building ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "حذف مبنى (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var building = await _db.Buildings
                .Include(b => b.Rooms)
                .FirstOrDefaultAsync(b => b.Id == id);
                
            if (building == null)
            {
                return NotFound(new { success = false, message = "Building not found" });
            }
            
            // Check if building has rooms
            if (building.Rooms.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Cannot delete building with existing rooms",
                    details = $"Building has {building.Rooms.Count} room(s)"
                });
            }
            
            _db.Buildings.Remove(building);
            await _db.SaveChangesAsync();
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting building with ID {BuildingId}", id);
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }
}