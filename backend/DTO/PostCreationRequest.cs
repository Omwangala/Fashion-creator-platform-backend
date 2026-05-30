public class PostCreationRequest
{
    public class PostCreationRequest
    {
        [Required]
        public IFormFile File { get; set; } = null!;

        [MaxLength(2200)]
        public string Caption { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(image|video)$")]
        public string MediaType { get; set; } = string.Empty;
    };
}