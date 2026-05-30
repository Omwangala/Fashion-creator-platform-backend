using backend.DTOs;          // 👈 CreatePostDto, PostResponseDto
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.Services
{
    public interface IPostService
    {
        Task<object> GetAllPostsAsync(DateTime? before, int pageSize);
        Task<PostResponseDto> CreatePostAsync(CreatePostDto dto);
        Task<PostResponseDto?> GetPostByIdAsync(int id);
    }
}