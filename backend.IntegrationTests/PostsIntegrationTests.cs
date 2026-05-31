using System.IO;
using System.Net.Http.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;

namespace backend.IntegrationTests
{
    public class PostsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public PostsIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _factory.InitializeDb();
        }

        private async Task<System.Net.Http.HttpClient> CreateAuthenticatedClient()
        {
            var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false
            });

            // Register
            var reg = new { Username = "postuser2", Email = "post2@example.com", Password = "Password1!" };
            await client.PostAsJsonAsync("/api/auth/register", reg);

            // Login
            var login = new { Username = "postuser2", Password = "Password1!" };
            var loginResp = await client.PostAsJsonAsync("/api/auth/login", login);
            loginResp.StatusCode.Should().Be(HttpStatusCode.OK);

            // ✅ Extract cookie and add manually to bypass Secure cookie restriction
            if (loginResp.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (var cookie in cookies)
                {
                    // Extract just name=value before the first semicolon
                    var cookieValue = cookie.Split(';')[0];
                    client.DefaultRequestHeaders.Add("Cookie", cookieValue);
                }
            }

            return client;
        }

        [Fact]
        public async Task CreatePost_UploadsFile_ReturnsOk()
        {
            var client = await CreateAuthenticatedClient();

            using var ms = new MemoryStream(Encoding.UTF8.GetBytes("fakejpeg"));
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(ms.ToArray());
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(fileContent, "MediaFile", "file.jpg");
            content.Add(new StringContent("hello caption"), "Caption");
            content.Add(new StringContent("image"), "MediaType");

            var resp = await client.PostAsync("/api/posts", content);
            resp.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}