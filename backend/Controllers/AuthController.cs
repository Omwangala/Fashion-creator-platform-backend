using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.Tasks;
using backend.Data;
using backend.Models;
using backend.Services;
using backend.DTOs; // <--- This points to your new separate DTO files

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;

        // Constructor now correctly receives BOTH services
        public AuthController(AppDbContext context, ITokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromBody] RegisterDto registerDto)
        {
            // Normalization: Convert to lowercase to prevent duplicate accounts with different casing
            var normalizedEmail = registerDto.Email.ToLower().Trim();
            var normalizedUsername = registerDto.Username.ToLower().Trim();

            if (await _context.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername))
                return BadRequest(new { message = "Identity already archived." });

            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail))
                return BadRequest(new { message = "Email already in use." });

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

            var user = new User
            {
                Username = registerDto.Username, // Keep original casing for display if you like
                Email = normalizedEmail,         // Store normalized for lookups
                PasswordHash = passwordHash
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Access credentials created." });
        }
        [EnableRateLimiting("LoginPolicy")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            // Normalize input for lookup
            var user = await _context.Users.FirstOrDefaultAsync(u =>
                u.Username.ToLower() == loginDto.Username.ToLower().Trim());

            // 🔒 PRODUCTION TIP: Generic Error Message
            // We check the password, but if it fails, we don't tell them WHICH part failed.
            if (user == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
            {
                // Don't say "User not found" or "Wrong Password"
                return Unauthorized(new { message = "Invalid credentials provided." });
            }

            var token = _tokenService.CreateToken(user.Username);
            AppendAuthCookie(token);

            return Ok(new { message = "Access Granted." });
        }

        // Helper method to keep the main logic clean
        private void AppendAuthCookie(string token)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // Ensure this is true for HTTPS
                SameSite = SameSiteMode.Strict,
                Expires = System.DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("vault_session", token, cookieOptions);
        }
    }
}