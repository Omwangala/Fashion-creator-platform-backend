using System;

namespace backend.DTOs
{
    public class PostResponseDto
    {
        public int Id { get; set; }
        public string MediaUrl { get; set; }
        public string Caption { get; set; }
        public string MediaType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}