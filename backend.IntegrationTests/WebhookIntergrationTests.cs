using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using backend.Data;
using backend.Models;
using Xunit;
using FluentAssertions;

namespace backend.IntegrationTests
{
    public class WebhookIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        public WebhookIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _factory.InitializeDb();
        }

        private static string ComputeSignature(string body, string timestamp, string secret)
        {
            var toSign = body + timestamp + secret;
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(toSign));
            return Convert.ToHexString(hash).ToLower();
        }

        [Fact]
        public async Task Webhook_ValidSignature_UpdatesPostStatus()
        {
            var client = _factory.CreateClient();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Posts.Add(new Post
                {
                    Id = 100,
                    PublicId = "pub-integ-1",
                    MediaUrl = "old",
                    Status = UploadStatus.Uploading,
                    UserId = 1,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var payload = new
            {
                PublicId = "pub-integ-1",
                SecureUrl = "https://cdn.new/integration.jpg",
                NotificationType = "upload",
                NotificationId = "notif-integ-1"
            };
            var body = JsonSerializer.Serialize(payload);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var secret = "test-cloud-secret";
            var signature = ComputeSignature(body, timestamp, secret);

            var req = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/cloudinary")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("X-Cld-Signature", signature);
            req.Headers.Add("X-Cld-Timestamp", timestamp);

            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().Be(HttpStatusCode.OK);

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var updated = await db.Posts.FindAsync(100);
                //updated.Status.Should().Be(UploadStatus.Ready);
                updated.Should().NotBeNull();
                updated!.Status.Should().Be(UploadStatus.Ready);
                //updated!.MediaUrl.Should().Be("https://cdn.new/integration.jpg");
                updated.MediaUrl.Should().Be("https://cdn.new/integration.jpg");
            }
        }
    }
}
