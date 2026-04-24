using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System;
using System.Threading.Tasks;

namespace backend.Services
{
    public class ImageService : IImageService
    {
        private readonly Cloudinary _cloudinary;

        public ImageService(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        public async Task<string> UploadImageAsync(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new Exception("Invalid file");

                await using var stream = file.OpenReadStream();

                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream)
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.Error != null)
                    throw new Exception(result.Error.Message);

                return result.SecureUrl.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception($"Image upload failed: {ex.Message}");
            }
        }
    }
}