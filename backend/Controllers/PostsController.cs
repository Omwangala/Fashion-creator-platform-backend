using backend.DTOs;
using backend.Data;
using backend.Services;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

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
        private readonly AppDbContext _context;

        public PostsController(IPostService postService, IImageService imageService, AppDbContext context)
        {
            _postService = postService;
            _imageService = imageService;
            _context = context;
        }

        [HttpPost]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> CreatePost([FromForm] PostCreationRequest request)
        {
            // ✅ 1. File validation FIRST before anything else
            if (request.File == null || request.File.Length == 0)
                return BadRequest("No file provided.");

            if (request.File.Length > 52_428_800)
                return BadRequest("File size exceeds the 50MB limit.");

            // ✅ 2. Auth check
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized("Invalid user token.");

            try
            {
                // ✅ 3. Upload with full error handling
                var uploadResult = await _imageService.UploadImageAsync(request.File);

                var dto = new CreatePostDto
                {
                    MediaUrl = uploadResult.MediaUrl,
                    Caption = request.Caption,
                    MediaType = request.MediaType,
                    UserId = userId,
                    PublicId = uploadResult.PublicId,
                    Status = UploadStatus.Uploading
                };

                var result = await _postService.CreatePostAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // ✅ 4. Never expose raw exception to client
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPosts([FromQuery] DateTime? before, [FromQuery] int pageSize = 10)
        {
            if (pageSize > 30) pageSize = 30;
            if (pageSize < 1) pageSize = 10;

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