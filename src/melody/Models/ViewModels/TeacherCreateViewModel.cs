using System.ComponentModel.DataAnnotations;

namespace melody.Models.ViewModels;

/// <summary>
/// Inputs needed to create both the <see cref="Domain.ApplicationUser"/> login and the
/// <see cref="Domain.Teacher"/> profile in one form.
/// </summary>
public class TeacherCreateViewModel
{
    [Required]
    [MaxLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
