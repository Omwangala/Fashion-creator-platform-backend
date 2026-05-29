using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using backend.DTOs;

namespace backend.Services
{
    public interface IPostService
    {
        Task<PostResponseDto> CreatePostAsync(CreatePostDto dto);
        Task<List<PostResponseDto>> GetAllPostsAsync(DateTime? before, int pageSize);
        Task<PostResponseDto> GetPostByIdAsync(int id);
    }
}