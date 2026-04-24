namespace backend.Services
{
    public interface IImageService
    {
        Task<string> UploadImageAsync(IFormFile file);
    }
}