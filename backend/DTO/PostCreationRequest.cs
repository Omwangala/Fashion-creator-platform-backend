using System.ComponentModel.DataAnnotations;  // 👈 [Required], [MaxLength], [RegularExpression]
using Microsoft.AspNetCore.Http;

public class PostCreationRequest
{
    
        [Required]
        public IFormFile MediaFile { get; set; } = null!;

        [MaxLength(2200)]
        public string Caption { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(image|video)$")]
        public string MediaType { get; set; } = string.Empty;
    
}