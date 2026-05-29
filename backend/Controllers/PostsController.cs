using backend.DTOs;
using backend.Data;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
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

        [Authorize]
        [HttpPost]
        [RequestSizeLimit(52_428_800)]
        public async Task<IActionResult> CreatePost([FromForm] PostCreationRequest request)
        {


            // 1. Get the username from the secure cookie
            var username = User.Identity?.Name;

            // 2. Find the user in the DB to get their ID
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out int userId)) return Unauthorized(); 
            var mediaUrl = await _imageService.UploadImageAsync(request.File);

            var dto = new CreatePostDto
            {
                MediaUrl = string.Empty,
                Caption = request.Caption,
                MediaType = request.MediaType,
                UserId = userId, // ✅ FIXED: Changed from user.Id to parsed userId variable
                PublicId = publicId,
                Status = UploadStatus.Uploading
            };


            if (request.File.Length > 52_428_800)
            {
                return BadRequest("File size exceeds the maximum allowed limit of 50MB.");
            }

            var result = await _postService.CreatePostAsync(dto);
            return Ok(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetAllPosts([FromQuery] DateTime? before, [FromQuery] int pageSize = 10)
        {
            // 🛡️ Guard rail: Limit maximum payload requests to prevent scraping attacks
            if (pageSize > 30) pageSize = 30;
            if (pageSize < 1) pageSize = 10;

            var posts = await _postService.GetAllPostsAsync(before, pageSize);
            return Ok(posts);
        }
    }
}