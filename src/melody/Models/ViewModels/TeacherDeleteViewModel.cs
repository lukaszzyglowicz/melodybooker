namespace melody.Models.ViewModels;

/// <summary>
/// Backs the teacher delete confirmation page — either a plain confirm prompt, or, when the
/// teacher still has assigned students, a blocking message listing those students instead.
/// </summary>
public class TeacherDeleteViewModel
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// True when this teacher still has assigned students and deletion must be blocked.
    /// </summary>
    public bool IsBlocked => AssignedStudentNames.Count > 0;

    public List<string> AssignedStudentNames { get; set; } = [];
}
