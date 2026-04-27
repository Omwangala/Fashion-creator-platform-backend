namespace backend.DTOs
{
    public class PostResponseDto
    {
        public int Id { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // The "Vogue" touch: show the creator's name
        public string Username { get; set; } = string.Empty;
    }
}