namespace backend.DTOs
{
    public class CreatePostDto
    {
        public string MediaUrl { get; set; }
        public string Caption { get; set; }
        public string MediaType { get; set; } // image or video
    }
}