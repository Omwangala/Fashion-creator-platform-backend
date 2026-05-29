public class PostCreationRequest
{
    public IFormFile File { get; set; }
    public string Caption { get; set; }
    public string MediaType { get; set; } = string.Empty;
}