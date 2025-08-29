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
    public BuildingsController(AppDbContext db) { _db = db; }

    // GET /api/buildings
    [HttpGet]
    [SwaggerOperation(Summary = "قائمة جميع المباني")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll()
    {
        var list = await _db.Buildings.AsNoTracking().ToListAsync();
        return Ok(list);
    }

    // POST /api/buildings
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "إنشاء مبنى جديد (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateBuildingRequest req)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        bool exists = await _db.Buildings.AnyAsync(d => d.Name == req.Name);
        if (exists) return BadRequest(new { message = "Building name already exists" });
        var building = new Building { Name = req.Name };
        _db.Buildings.Add(building);
        await _db.SaveChangesAsync();
        return Ok(building);
    }

    // DELETE /api/buildings/{id}
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [SwaggerOperation(Summary = "حذف مبنى (أدمن فقط)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var building = await _db.Buildings.FindAsync(id);
        if (building == null) return NotFound();
        _db.Buildings.Remove(building);
        await _db.SaveChangesAsync();
        return Ok();
    }
}