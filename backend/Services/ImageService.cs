using backend.DTOs;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace backend.Services
{
    public class ImageService : IImageService
    {
        private readonly Cloudinary _cloudinary;

        private static readonly string[] AllowedContentTypes = new[]
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "video/mp4",
            "video/quicktime"
        };

        private static readonly string[] AllowedExtensions = new[]
        {
            ".jpg", ".jpeg", ".png", ".webp", ".mp4" , ".mov"
        };

        public ImageService(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        public async Task<UploadResultDto> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new Exception("No file provided.");

            if (file.Length > 52_428_800)
                throw new Exception("File size exceeds the 50MB limit.");

            if (!AllowedContentTypes.Contains(file.ContentType.ToLower()))
                throw new Exception("File type not allowed. Only JPEG, PNG, WebP, MP4, and MOV are accepted.");

            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!AllowedExtensions.Contains(extension))
                throw new Exception("File extension not allowed.");

            try
            {
                await using var stream = file.OpenReadStream();

                RawUploadResult result;  // 👈 Declare ONCE outside the if/else

                if (file.ContentType.StartsWith("video/"))
                {
                    var videoParams = new VideoUploadParams
                    {
                        File = new FileDescription(file.FileName, stream)
                    };
                    result = await _cloudinary.UploadAsync(videoParams);  // 👈 Assign inside branch
                }
                else
                {
                    var imageParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, stream)
                    };
                    result = await _cloudinary.UploadAsync(imageParams);  // 👈 Assign inside branch
                }

                if (result.Error != null)
                    throw new Exception(result.Error.Message);

                return new UploadResultDto
                {
                    MediaUrl = result.SecureUrl.ToString(),
                    PublicId = result.PublicId
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Image upload failed.", innerException: ex);
            }
        }
        
    }
}