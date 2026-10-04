using System.ComponentModel.DataAnnotations;
using Procura.API.Shared.Enums;

namespace Procura.API.Shared.Authentication
{
    public class RegisterDto
    {
        [Required, StringLength(100, MinimumLength = 1)]
        public string FirstName { get; set; } = string.Empty;
        [Required, StringLength(100, MinimumLength = 1)]
        public string LastName { get; set; } = string.Empty;
        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;
        [Required, StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters.")]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
    }
}
