using backend.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.Services
{
	public interface IPostService
	{
		Task<PostResponseDto> CreatePostAsync(CreatePostDto dto);
		Task<List<PostResponseDto>> GetAllPostsAsync();
		Task<PostResponseDto> GetPostByIdAsync(int id);
	}
}