namespace backend.DTOs
{
    public class CreatePostDto
    {
        public string MediaUrl { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty; // image or video

        // 🔗 The Link to the User
        public int UserId { get; set; }
    }
}