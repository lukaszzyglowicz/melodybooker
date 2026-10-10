using System.ComponentModel.DataAnnotations;

namespace melody.Models.ViewModels;

/// <summary>
/// A single row in the viewing teacher's own student list.
/// </summary>
public class TeacherStudentListItemViewModel
{
    public int Id { get; set; }

    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Class")]
    public int Class { get; set; }

    public string Instrument { get; set; } = string.Empty;
}
