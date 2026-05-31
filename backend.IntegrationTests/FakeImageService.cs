using System.Threading.Tasks;
using backend.DTOs;
using Microsoft.AspNetCore.Http;

namespace backend.IntegrationTests
{
    public class FakeImageService : backend.Services.IImageService
    {
        public Task<UploadResultDto> UploadImageAsync(IFormFile file)
        {
            var id = $"fake-{System.Guid.NewGuid():N}";
            return Task.FromResult(new UploadResultDto
            {
                MediaUrl = $"https://cdn.test/{id}.jpg",
                PublicId = id
            });
        }
    }
}