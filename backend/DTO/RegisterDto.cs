using System.ComponentModel.DataAnnotations;

namespace backend.DTOs
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Username is required")]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 20 characters")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Please provide a valid fashion-industry email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        // This Regex enforces: 1 Uppercase, 1 Lowercase, 1 Number, 1 Special Char, min 8 chars
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "Password must be at least 8 characters long and include an uppercase letter, a number, and a special character.")]
        public string Password { get; set; } = string.Empty;
    }
}