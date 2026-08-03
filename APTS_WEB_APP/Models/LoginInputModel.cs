using System.ComponentModel.DataAnnotations;

namespace APTS_WEB_APP.Models
{
    public class LoginInputModel
    {
        [Required(ErrorMessage = "Please enter Login Id.")]
        [StringLength(50, ErrorMessage = "Login Id cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9_.@\- ]+$", ErrorMessage = "Login Id contains invalid characters.")]
        public string LoginId { get; set; }

        [Required(ErrorMessage = "Please enter Password.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Password cannot exceed 100 characters.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please select Location.")]
        [StringLength(50, ErrorMessage = "Location cannot exceed 50 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9_\- ]+$", ErrorMessage = "Location contains invalid characters.")]
        public string Locations { get; set; }
    }
}
