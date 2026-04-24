using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

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
                MediaType = dto.MediaType
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            return new PostResponseDto
            {
                Id = post.Id,
                MediaUrl = post.MediaUrl,
                Caption = post.Caption,
                MediaType = post.MediaType,
                CreatedAt = post.CreatedAt
            };
        }

        public async Task<List<PostResponseDto>> GetAllPostsAsync()
        {
            return await _context.Posts
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PostResponseDto
                {
                    Id = p.Id,
                    MediaUrl = p.MediaUrl,
                    Caption = p.Caption,
                    MediaType = p.MediaType,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<PostResponseDto> GetPostByIdAsync(int id)
        {
            var post = await _context.Posts.FindAsync(id);

            if (post == null) return null;

            return new PostResponseDto
            {
                Id = post.Id,
                MediaUrl = post.MediaUrl,
                Caption = post.Caption,
                MediaType = post.MediaType,
                CreatedAt = post.CreatedAt
            };
        }
    }
}