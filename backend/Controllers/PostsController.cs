using backend.DTOs;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly IImageService _imageService;

        public PostsController(IPostService postService, IImageService imageService)
        {
            _postService = postService;
            _imageService = imageService;
        }

        [Authorize] // 🔐 Only admin can upload
        [HttpPost]
        public async Task<IActionResult> CreatePost([FromForm] PostCreationRequest request)
        {
            var mediaUrl = await _imageService.UploadImageAsync(request.File);

            var dto = new CreatePostDto
            {
                MediaUrl = mediaUrl,
                Caption = request.Caption,
                MediaType = "image"
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