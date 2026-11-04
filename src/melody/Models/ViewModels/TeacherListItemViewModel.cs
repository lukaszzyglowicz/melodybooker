using System.ComponentModel.DataAnnotations;

namespace melody.Models.ViewModels;

/// <summary>
/// A single row in the teacher roster list, including the number of students currently
/// assigned (used to warn before navigating to delete).
/// </summary>
public class TeacherListItemViewModel
{
    public int Id { get; set; }

    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Display(Name = "Students")]
    public int StudentCount { get; set; }
}
