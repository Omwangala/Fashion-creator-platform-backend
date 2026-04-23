namespace backend.Models
{
    public class Post
    {
        public int Id { get; set; }

        public string MediaUrl { get; set; }

        public string Caption { get; set; }

        public string MediaType { get; set; } // image or video

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}