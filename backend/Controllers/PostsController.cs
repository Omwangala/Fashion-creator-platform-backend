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
        public async Task<IActionResult> CreatePost([FromForm] PostCreationRequest request)
        {
            // 1. Get the username from the secure cookie
            var username = User.Identity?.Name;

            // 2. Find the user in the DB to get their ID
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return Unauthorized();

            var mediaUrl = await _imageService.UploadImageAsync(request.File);

            var dto = new CreatePostDto
            {
                MediaUrl = mediaUrl,
                Caption = request.Caption,
                MediaType = "image",
                UserId = user.Id // 🔒 Securely assigned on the server
            };

            var result = await _postService.CreatePostAsync(dto);
            return Ok(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetAllPosts()
        {
            var posts = await _postService.GetAllPostsAsync();
            return Ok(posts);
        }
    }
}