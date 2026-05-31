using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using backend.Controllers;
using backend.Services;
using backend.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace backend.Tests.Controllers
{
    public class PostsControllerTests
    {
        private static IFormFile CreateFormFile(string fileName, string contentType, byte[] content)
        {
            var ms = new MemoryStream(content);
            return new FormFile(ms, 0, content.Length, "mediaFile", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        [Fact]
        public async Task CreatePost_ReturnsBadRequest_When_NoFile()
        {
            var postServiceMock = new Mock<IPostService>();
            var imageServiceMock = new Mock<IImageService>();
            var logger = new Mock<ILogger<PostsController>>();

            var sut = new PostsController(postServiceMock.Object, imageServiceMock.Object, logger.Object);

            var req = new PostCreationRequest { MediaFile = null };

            var result = await sut.CreatePost(req);

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task CreatePost_ReturnsOk_OnSuccess()
        {
            var postServiceMock = new Mock<IPostService>();
            var imageServiceMock = new Mock<IImageService>();
            var logger = new Mock<ILogger<PostsController>>();

            var uploadDto = new UploadResultDto { MediaUrl = "https://cdn/x.jpg", PublicId = "pub" };
            imageServiceMock.Setup(s => s.UploadImageAsync(It.IsAny<IFormFile>()))
                .ReturnsAsync(uploadDto);

            postServiceMock.Setup(s => s.CreatePostAsync(It.IsAny<CreatePostDto>()))
                .ReturnsAsync(new backend.DTOs.PostResponseDto { Id = 5, MediaUrl = uploadDto.MediaUrl });

            var sut = new PostsController(postServiceMock.Object, imageServiceMock.Object, logger.Object);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "10")
            }, "mock"));

            sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            var file = CreateFormFile("img.jpg", "image/jpeg", Encoding.UTF8.GetBytes("data"));
            var req = new PostCreationRequest { MediaFile = file, Caption = "c", MediaType = "image" };

            var result = await sut.CreatePost(req);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }
    }
}