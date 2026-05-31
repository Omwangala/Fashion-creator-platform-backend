using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Xunit;
using backend.Services;

namespace backend.Tests.Services
{
    public class TokenServiceTests
    {
        [Fact]
        public void CreateToken_IncludesUserClaims()
        {
            var inMemorySettings = new[]
            {
                new KeyValuePair<string,string>("Jwt:Key", "super-secret-key-that-is-long-enough"),
                new KeyValuePair<string,string>("Jwt:Issuer", "tests"),
                new KeyValuePair<string,string>("Jwt:Audience", "tests")
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
            var sut = new TokenService(config);

            var token = sut.CreateToken(42, "alice");

            Assert.False(string.IsNullOrWhiteSpace(token));
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            Assert.Contains(jwt.Claims, c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier && c.Value == "42");
            Assert.Contains(jwt.Claims, c => c.Type == System.Security.Claims.ClaimTypes.Name && c.Value == "alice");
        }
    }
}