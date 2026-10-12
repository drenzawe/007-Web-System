using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using backend.Services;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/[controller]")] // This creates the URL path: http://localhost:xxxx/api/users
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/users
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        // Fetch all users directly from your SQLite database file!
        var users = await _context.LegacyUsers.ToListAsync();
        return Ok(users);
    }
}
