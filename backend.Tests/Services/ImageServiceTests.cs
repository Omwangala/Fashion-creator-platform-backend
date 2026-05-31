using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Http;
using Xunit;
using backend.Services;

namespace backend.Tests.Services
{
    public class ImageServiceTests
    {
        private readonly ImageService _sut;

        public ImageServiceTests()
        {
            // Real instance with dummy credentials — upload calls will fail
            // but validation runs before any upload attempt
            var cloudinary = new Cloudinary(new Account("testcloud", "testkey", "testsecret"));
            _sut = new ImageService(cloudinary);
        }

        private static IFormFile CreateFormFile(string fileName, string contentType, byte[] content)
        {
            var ms = new MemoryStream(content);
            return new FormFile(ms, 0, content.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        [Fact]
        public async Task UploadImageAsync_Throws_When_FileIsNull()
        {
            await Assert.ThrowsAsync<Exception>(() => _sut.UploadImageAsync(null!));
        }

        [Fact]
        public async Task UploadImageAsync_Throws_When_FileIsEmpty()
        {
            var file = CreateFormFile("photo.jpg", "image/jpeg", Array.Empty<byte>());
            await Assert.ThrowsAsync<Exception>(() => _sut.UploadImageAsync(file));
        }

        [Fact]
        public async Task UploadImageAsync_Throws_On_InvalidContentType()
        {
            var file = CreateFormFile("file.txt", "text/plain", new byte[] { 1, 2, 3 });
            await Assert.ThrowsAsync<Exception>(() => _sut.UploadImageAsync(file));
        }

        [Fact]
        public async Task UploadImageAsync_Throws_On_InvalidExtension()
        {
            var file = CreateFormFile("file.bmp", "image/jpeg", new byte[] { 1, 2, 3 });
            await Assert.ThrowsAsync<Exception>(() => _sut.UploadImageAsync(file));
        }

        [Fact]
        public async Task UploadImageAsync_Throws_When_FileTooLarge()
        {
            var largeContent = new byte[52_428_801];
            var file = CreateFormFile("photo.jpg", "image/jpeg", largeContent);
            await Assert.ThrowsAsync<Exception>(() => _sut.UploadImageAsync(file));
        }

        [Theory]
        [InlineData("video/mp4", ".mp4")]
        [InlineData("video/quicktime", ".mov")]
        public async Task UploadImageAsync_VideoFiles_PassValidation_FailOnCloudinary(
            string contentType, string extension)
        {
            // Validation passes — fails only on actual Cloudinary call with dummy creds
            var file = CreateFormFile($"video{extension}", contentType, new byte[] { 1, 2, 3 });
            var ex = await Assert.ThrowsAsync<Exception>(() => _sut.UploadImageAsync(file));
            Assert.Contains("Image upload failed", ex.Message);
        }
    }
}