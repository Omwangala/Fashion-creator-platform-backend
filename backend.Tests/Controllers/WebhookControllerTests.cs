using backend.Controllers;
using backend.Data;
using backend.Hubs;
using backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.Controllers
{
    public class WebhookControllerTests
    {
        private static AppDbContext CreateInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new AppDbContext(options);
        }

        private static string ComputeSignature(string body, string timestamp, string secret)
        {
            var toSign = body + timestamp + secret;
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(toSign));
            return Convert.ToHexString(hash).ToLower();
        }

        [Fact]
        public async Task HandleCloudinaryWebhook_ReturnsUnauthorized_On_InvalidSignature()
        {
            var ctx = CreateInMemoryContext(nameof(HandleCloudinaryWebhook_ReturnsUnauthorized_On_InvalidSignature));
            var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var logger = new Mock<ILogger<WebhookController>>();
            var hubMock = new Mock<IHubContext<UploadHub>>();

            var sut = new WebhookController(ctx, config, logger.Object, hubMock.Object);

            var body = JsonSerializer.Serialize(new { PublicId = "p", NotificationType = "upload", NotificationId = "id1" });
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(body));
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Body = ms;
            httpContext.Request.Headers["X-Cld-Signature"] = "bad";
            httpContext.Request.Headers["X-Cld-Timestamp"] = "123";

            sut.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = await sut.HandleCloudinaryWebhook();

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task HandleCloudinaryWebhook_UpdatesPost_And_Notifies_On_ValidWebhook()
        {
            var ctx = CreateInMemoryContext(nameof(HandleCloudinaryWebhook_UpdatesPost_And_Notifies_On_ValidWebhook));
            // seed a post
            var post = new Post { Id = 2, PublicId = "pub-1", Status = UploadStatus.Uploading, MediaUrl = "old", UserId = 99, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow };
            ctx.Posts.Add(post);
            await ctx.SaveChangesAsync();

            var secret = "my-secret";
            var config = new ConfigurationBuilder().AddInMemoryCollection(new[]
            {
                new KeyValuePair<string,string>("CloudinarySettings:ApiSecret", secret)
            }).Build();

            var logger = new Mock<ILogger<WebhookController>>();

            var clientsMock = new Mock<IClientProxy>();
            clientsMock
                .Setup(c => c.SendCoreAsync("UploadComplete", It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var clientsAllMock = new Mock<IHubClients>();
            var groupProxy = clientsMock.Object;

            var hubContextMock = new Mock<IHubContext<UploadHub>>();
            var clientsObjMock = new Mock<IHubClients>();
            hubContextMock.Setup(h => h.Clients).Returns(clientsObjMock.Object);
            clientsObjMock.Setup(c => c.Group(It.IsAny<string>())).Returns(groupProxy);

            var sut = new WebhookController(ctx, config, logger.Object, hubContextMock.Object);

            var payload = new
            {
                PublicId = "pub-1",
                SecureUrl = "https://cdn/new.jpg",
                NotificationType = "upload",
                NotificationId = "notif-123"
            };
            var body = JsonSerializer.Serialize(payload);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = ComputeSignature(body, timestamp, secret);

            var ms = new MemoryStream(Encoding.UTF8.GetBytes(body));
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Body = ms;
            httpContext.Request.Headers["X-Cld-Signature"] = signature;
            httpContext.Request.Headers["X-Cld-Timestamp"] = timestamp;

            sut.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = await sut.HandleCloudinaryWebhook();

            Assert.IsType<OkObjectResult>(result);

            var updated = await ctx.Posts.FindAsync(2);
            Assert.Equal(UploadStatus.Ready, updated.Status);
            Assert.Equal("https://cdn/new.jpg", updated.MediaUrl);
        }
    }
}