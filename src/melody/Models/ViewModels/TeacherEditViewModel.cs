using System.ComponentModel.DataAnnotations;

namespace melody.Models.ViewModels;

/// <summary>
/// Edit-only inputs for a teacher — name change alone; credential changes are out of scope.
/// </summary>
public class TeacherEditViewModel
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;
}
