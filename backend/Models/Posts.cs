namespace backend.Models
{
    public class Post
    {
        public int Id { get; set; }

        public string MediaUrl { get; set; } = string.Empty;

        public UploadStatus Status { get; set; } = UploadStatus.Pending;

        public string Caption { get; set; } = string.Empty;

        public string MediaType { get; set; } = string.Empty; // image or video

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string PublicId { get; set; } = string.Empty;

        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

        // 🔗 The Relationship
        public int? UserId { get; set; } // Foreign Key
        public User? User { get; set; } = null!; // Navigation Property
    }

    public class ProcessedWebhookEvent
    {
        public string Id { get; set; } = string.Empty; // Stores Cloudinary's internal notification_id
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
}