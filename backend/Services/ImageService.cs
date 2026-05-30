using backend.DTOs;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using System;
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
            // ? 1. Null/empty check
            if (file == null || file.Length == 0)
                throw new Exception("No file provided.");

            // ? 2. File size check FIRST (before anything else)
            if (file.Length > 52_428_800)
                throw new Exception("File size exceeds the 50MB limit.");

            // ? 3. MIME type check
            if (!AllowedContentTypes.Contains(file.ContentType.ToLower()))
                throw new Exception("File type not allowed. Only JPEG, PNG, WebP, and MP4 are accepted.");

            // ? 4. Extension check (double validation — MIME can be spoofed)
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!AllowedExtensions.Contains(extension))
                throw new Exception("File extension not allowed.");

            try
            {
                await using var stream = file.OpenReadStream();

                RawUploadResult result;

                if (file.ContentType.StartsWith("video/"))
                {
                    var videoParams = new VideoUploadParams
                    {
                        File = new FileDescription(file.FileName, stream)
                    };
                    result = await _cloudinary.UploadAsync(videoParams);
                }
                else
                {
                    var imageParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, stream)
                    };
                    result = await _cloudinary.UploadAsync(imageParams);
                }

                if (result.Error != null)
                    throw new Exception(result.Error.Message);

                var result = await _cloudinary.UploadAsync(uploadParams);

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
                // ✅ Preserves original stack trace
                throw new Exception("Image upload failed.", innerException: ex);
            }
        }
        public async Task DeleteImageAsync(string publicId)
        {
            var deleteParams = new DeletionParams(publicId);
            await _cloudinary.DestroyAsync(deleteParams);

            if (result.Result != "ok")
            {
                _logger.LogWarning(
                    "Cloudinary deletion may have failed for PublicId {PublicId}. Result: {Result}",
                    publicId, result.Result);
            }
        }
    }
}