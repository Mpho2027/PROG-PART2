using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Phone number (optional)")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Choose a password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose how you will use RaceDay.")]
    [RegularExpression("^(Participant|Organiser)$", ErrorMessage = "Choose Participant or Organiser.")]
    [Display(Name = "I am joining as")]
    public string Role { get; set; } = Roles.Participant;
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    // Shown for information only; email and role cannot be changed.
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime MemberSince { get; set; }
    public bool HasProfilePicture { get; set; }

    public static ProfileViewModel From(UserProfileDto p) => new()
    {
        FullName = p.FullName,
        PhoneNumber = p.PhoneNumber,
        Email = p.Email,
        Role = p.Role,
        MemberSince = p.CreatedAt,
        HasProfilePicture = p.HasProfilePicture
    };
}
