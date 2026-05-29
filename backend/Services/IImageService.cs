using backend.DTOs;
using Microsoft.AspNetCore.Http;

namespace backend.Services
{
    public interface IImageService
    {
        Task<UploadResultDto> UploadImageAsync(IFormFile file);
    }
}
