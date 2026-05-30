using backend.DTOs;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace backend.Services
{
    public interface IImageService
    {
        Task<UploadResultDto> UploadImageAsync(IFormFile file);
        
    }
}
