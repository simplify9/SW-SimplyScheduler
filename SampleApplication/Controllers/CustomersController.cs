using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SampleApplication.Data;

namespace SampleApplication.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Customer>>> GetAll() => await _db.Customers.AsNoTracking().ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Customer>> Get(int id)
    {
        var entity = await _db.Customers.FindAsync(id);
        return entity == null ? NotFound() : entity;
    }

    public record CustomerCreateDto(string Name, string Email);
    public record CustomerUpdateDto(string Name, string Email);

    [HttpPost]
    public async Task<ActionResult<Customer>> Create(CustomerCreateDto dto)
    {
        if (await _db.Customers.AnyAsync(c => c.Email == dto.Email))
            return Conflict("Email already exists");
        var entity = new Customer { Name = dto.Name, Email = dto.Email };
        _db.Customers.Add(entity);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CustomerUpdateDto dto)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity == null) return NotFound();
        if (entity.Email != dto.Email && await _db.Customers.AnyAsync(c => c.Email == dto.Email))
            return Conflict("Email already exists");
        entity.Name = dto.Name;
        entity.Email = dto.Email;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Customers.FindAsync(id);
        if (entity == null) return NotFound();
        _db.Customers.Remove(entity);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

