using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using backend.Controllers;
using backend.Data;
using backend.Services;
using backend.Models;
using backend.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace backend.Tests.Controllers
{
    public class AuthControllerTests
    {
        private static AppDbContext CreateInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task SignUp_ReturnsBadRequest_When_UsernameExists()
        {
            var ctx = CreateInMemoryContext(nameof(SignUp_ReturnsBadRequest_When_UsernameExists));
            ctx.Users.Add(new User { Username = "taken", Email = "x@y.com", PasswordHash = "h" });
            await ctx.SaveChangesAsync();

            var tokenMock = new Mock<ITokenService>();
            var sut = new AuthController(ctx, tokenMock.Object);

            var dto = new RegisterDto { Username = "taken", Email = "other@x.com", Password = "pw" };

            var result = await sut.SignUp(dto);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Login_ReturnsUnauthorized_When_InvalidCredentials()
        {
            var ctx = CreateInMemoryContext(nameof(Login_ReturnsUnauthorized_When_InvalidCredentials));
            ctx.Users.Add(new User { Id = 1, Username = "user", Email = "u@e", PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct") });
            await ctx.SaveChangesAsync();

            var tokenMock = new Mock<ITokenService>();
            var sut = new AuthController(ctx, tokenMock.Object);

            var dto = new LoginDto { Username = "user", Password = "wrong" };

            var result = await sut.Login(dto);

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Login_AppendsCookie_On_Success()
        {
            var ctx = CreateInMemoryContext(nameof(Login_AppendsCookie_On_Success));
            ctx.Users.Add(new User { Id = 7, Username = "user2", Email = "u2@e", PasswordHash = BCrypt.Net.BCrypt.HashPassword("correct") });
            await ctx.SaveChangesAsync();

            var tokenMock = new Mock<ITokenService>();
            tokenMock.Setup(t => t.CreateToken(7, "user2")).Returns("tok");

            var sut = new AuthController(ctx, tokenMock.Object);
            sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var dto = new LoginDto { Username = "user2", Password = "correct" };

            var result = await sut.Login(dto);

            Assert.IsType<OkObjectResult>(result);
            var setCookie = sut.Response.Headers["Set-Cookie"].FirstOrDefault();
            Assert.False(string.IsNullOrEmpty(setCookie));
        }
    }
}