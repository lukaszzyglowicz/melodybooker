using System.ComponentModel.DataAnnotations;

namespace melody.Models.ViewModels;

/// <summary>
/// A single row in the student roster list, including assigned teacher and instrument.
/// </summary>
public class StudentListItemViewModel
{
    public int Id { get; set; }

    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Class")]
    public int Class { get; set; }

    public string Instrument { get; set; } = string.Empty;

    [Display(Name = "Teacher")]
    public string TeacherFullName { get; set; } = string.Empty;
}
