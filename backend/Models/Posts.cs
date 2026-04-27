namespace backend.Models
{
    public class Post
    {
        public int Id { get; set; }

        public string MediaUrl { get; set; } = string.Empty;

        public string Caption { get; set; } = string.Empty;

        public string MediaType { get; set; } = string.Empty; // image or video

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🔗 The Relationship
        public int ? UserId { get; set; } // Foreign Key
        public User ? User { get; set; } = null!; // Navigation Property
    }
}