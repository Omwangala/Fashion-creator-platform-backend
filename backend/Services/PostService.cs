using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace backend.Services
{
    public class PostService : IPostService
    {
        private readonly AppDbContext _context;

        public PostService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PostResponseDto> CreatePostAsync(CreatePostDto dto)
        {
            var post = new Post
            {
                MediaUrl = dto.MediaUrl,
                Caption = dto.Caption,
                MediaType = dto.MediaType,
                UserId = dto.UserId, // 🔗 Linking to the creator
                CreatedAt = DateTime.UtcNow
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            // Fetch again with Include to get the Username for the response
            return await GetPostByIdAsync(post.Id);
        }

        public async Task<List<PostResponseDto>> GetAllPostsAsync()
        {
            return await _context.Posts
                .Include(p => p.User) // 🔑 Still join the table
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PostResponseDto
                {
                    Id = p.Id,
                    MediaUrl = p.MediaUrl,
                    Caption = p.Caption,
                    MediaType = p.MediaType,
                    CreatedAt = p.CreatedAt,
                    // 🛡️ Safe check: if User is null, show "Vogue Guest" or "Legacy"
                    Username = p.User != null ? p.User.Username : "Vogue Creator"
                })
                .ToListAsync();
        }

        public async Task<PostResponseDto> GetPostByIdAsync(int id)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return null;

            return new PostResponseDto
            {
                Id = post.Id,
                MediaUrl = post.MediaUrl,
                Caption = post.Caption,
                MediaType = post.MediaType,
                CreatedAt = post.CreatedAt,
                Username = post.User.Username
            };
        }
    }
}