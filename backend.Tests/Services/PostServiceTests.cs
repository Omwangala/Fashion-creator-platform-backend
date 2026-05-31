using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Services;
using backend.DTOs;
using backend.Models;
using Xunit;

namespace backend.Tests.Services
{
    public class PostServiceTests
    {
        private static AppDbContext CreateInMemoryContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new AppDbContext(options);
        }

        [Fact]
        public async Task CreatePostAsync_AddsPostAndReturnsResponseDto()
        {
            var ctx = CreateInMemoryContext(nameof(CreatePostAsync_AddsPostAndReturnsResponseDto));
            ctx.Users.Add(new User { Id = 1, Username = "bob", Email = "bob@example.com" });
            await ctx.SaveChangesAsync();

            var sut = new PostService(ctx);

            var dto = new CreatePostDto
            {
                MediaUrl = "https://cdn.example/test.jpg",
                Caption = "hello",
                MediaType = "image",
                PublicId = "pub1",
                Status = UploadStatus.Uploading,
                UserId = 1
            };

            var result = await sut.CreatePostAsync(dto);

            Assert.NotNull(result);
            Assert.Equal(dto.MediaUrl, result.MediaUrl);
            Assert.Equal("bob", result.Username);
            Assert.True(result.Id > 0);

            var stored = ctx.Posts.FirstOrDefault(p => p.Id == result.Id);
            Assert.NotNull(stored);
            Assert.Equal(dto.PublicId, stored.PublicId);
        }
    }
}