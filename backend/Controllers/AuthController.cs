using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ITokenService _tokenService;

        public AuthController(ITokenService tokenService)
        {
            _tokenService = tokenService;
        }

        [HttpPost("login")]
        public IActionResult Login(string username, string password)
        {
            var validUser = _config["AdminConfig:Username"];
            var validPass = _config["AdminConfig:Password"];
            if (username == "admin" && password == "password")
            {
                var token = _tokenService.CreateToken(username);
                return Ok(new { token });
            }

            return Unauthorized();
        }
    }
}