using backend.DTOs;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;    // 👈 Needed for ClaimTypes

namespace backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("GeneralPolicy")]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly IImageService _imageService;
        private readonly ILogger<PostsController> _logger;

        private static readonly string[] AllowedMimeTypes =
            { "image/jpeg", "image/png", "image/webp", "video/mp4", "video/quicktime" };

        public PostsController(IPostService postService, IImageService imageService,
            ILogger<PostsController> logger)
        {
            _postService = postService;
            _imageService = imageService;
            _logger = logger;
        }

        [HttpPost]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> CreatePost([FromForm] PostCreationRequest request)
        {
            if (request.MediaFile == null || request.MediaFile.Length == 0)
                return BadRequest("No file provided.");

            if (request.MediaFile.Length > 52_428_800)
                return BadRequest("File size exceeds the 50MB limit.");

            if (!AllowedMimeTypes.Contains(request.MediaFile.ContentType.ToLower()))
                return BadRequest("File type not supported.");

            // ✅ Extract userId from claims HERE — before the try block
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized("Invalid user token.");

            var uploadResult = await _imageService.UploadImageAsync(request.MediaFile);

            try
            {
                var dto = new CreatePostDto
                {
                    MediaUrl = uploadResult.MediaUrl,
                    Caption = request.Caption,
                    MediaType = request.MediaType,
                    UserId = userId,        // ✅ Now in scope
                    PublicId = uploadResult.PublicId,
                    Status = UploadStatus.Uploading
                };

                var result = await _postService.CreatePostAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // ✅ No DeleteImageAsync — not implemented yet
                _logger.LogError(ex, "Post creation failed for user {UserId}", userId);
                return StatusCode(500, new { error = "An unexpected error occurred. Please try again." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPosts([FromQuery] DateTime? before, [FromQuery] int pageSize = 10)
        {
            var posts = await _postService.GetAllPostsAsync(before, pageSize);
            return Ok(posts);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPostById(int id)
        {
            var post = await _postService.GetPostByIdAsync(id);
            if (post == null)
                return NotFound(new { error = $"Post {id} not found." });

            return Ok(post);
        }
    }
}