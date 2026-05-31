using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace backend.IntegrationTests
{
    public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AuthIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _factory.InitializeDb();
        }

        [Fact]
        public async Task Register_And_Login_ReturnsCookie()
        {
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });

            var reg = new { Username = "integuser", Email = "integ@example.com", Password = "Password1!" };
            var regResp = await client.PostAsJsonAsync("/api/auth/register", reg);
            regResp.StatusCode.Should().Be(HttpStatusCode.OK);

            var login = new { Username = "integuser", Password = "Password1!" };
            var loginResp = await client.PostAsJsonAsync("/api/auth/login", login);
            loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

            var setCookie = loginResp.Headers.Contains("Set-Cookie") || client.DefaultRequestHeaders.Contains("Cookie");
            setCookie.Should().BeTrue();
        }
    }
}