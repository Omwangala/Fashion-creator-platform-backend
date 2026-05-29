using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using backend.Data;
using backend.DTOs;
using backend.Models;
using Microsoft.EntityFrameworkCore;

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
                Status = dto.Status,
                Caption = dto.Caption,
                MediaType = dto.MediaType,
                UserId = dto.UserId,
                PublicId = dto.PublicId,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            
            var user = await _context.Users.FindAsync(dto.UserId);

            return new PostResponseDto
            {
                Id = post.Id,
                MediaUrl = post.MediaUrl,
                Caption = post.Caption,
                MediaType = post.MediaType,
                CreatedAt = post.CreatedAt,
                Username = user?.Username ?? "Unknown" 
            };
        }

        public async Task<List<PostResponseDto>> GetAllPostsAsync(DateTime? before, int pageSize)
        {
            var queryBase = _context.Posts.AsNoTracking();

            if (before.HasValue)
            {
                queryBase = queryBase.Where(p => p.CreatedAt < before.Value);
            }

            return await queryBase
                .OrderByDescending(p => p.CreatedAt)
                .Take(pageSize)
                .Include(p => p.User)
                .AsSplitQuery()
                .Select(p => new PostResponseDto
                {
                    Id = p.Id,
                    MediaUrl = p.MediaUrl,
                    Caption = p.Caption,
                    MediaType = p.MediaType,
                    CreatedAt = p.CreatedAt,
                    Username = p.User != null ? p.User.Username : "Unknown" 
                })
                .ToListAsync();
        }

        public async Task<PostResponseDto?> GetPostByIdAsync(int id) 
        {
            var post = await _context.Posts
                .AsNoTracking()
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
                Username = post.User?.Username ?? "Unknown" 
            };
        }
    }
}