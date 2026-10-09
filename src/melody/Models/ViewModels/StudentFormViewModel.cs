using System.ComponentModel.DataAnnotations;
using melody.Models.Domain;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace melody.Models.ViewModels;

/// <summary>
/// Single shape reused for both create (Id == 0) and edit — the fields are identical in both
/// cases. <see cref="Teachers"/> is populated by the controller for the dropdown and must never
/// be required/validated on POST.
/// </summary>
public class StudentFormViewModel
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Range(1, 8)]
    public int Class { get; set; }

    [Required]
    public Instrument Instrument { get; set; }

    [Required]
    [Display(Name = "Teacher")]
    public int TeacherId { get; set; }

    [ValidateNever]
    public IEnumerable<SelectListItem> Teachers { get; set; } = [];
}
