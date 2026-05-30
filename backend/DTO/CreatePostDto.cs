using backend.Models; // 👈 Correct namespace
using System.ComponentModel.DataAnnotations;

namespace backend.DTOs
{
   

    
        public class CreatePostDto
        {
            [Required(ErrorMessage = "Media URL is required")]
            [Url(ErrorMessage = "MediaUrl must be a valid URL")]
            public string MediaUrl { get; set; } = string.Empty;

            [MaxLength(2200, ErrorMessage = "Caption cannot exceed 2200 characters")]
            public string Caption { get; set; } = string.Empty;

            [Required(ErrorMessage = "MediaType is required")]
            [RegularExpression("^(image|video)$", ErrorMessage = "MediaType must be 'image' or 'video'")]
            public string MediaType { get; set; } = string.Empty;

            [Required(ErrorMessage = "PublicId is required")]
            [MaxLength(200, ErrorMessage = "PublicId cannot exceed 200 characters")]
            public string PublicId { get; set; } = string.Empty;

            // ✅ Enum — no annotation needed, invalid values are rejected by model binding automatically
            public UploadStatus Status { get; set; } = UploadStatus.Pending;

            [Required(ErrorMessage = "UserId is required")]
            [Range(1, int.MaxValue, ErrorMessage = "UserId must be a positive integer")]
            public int UserId { get; set; }
        }
    
}