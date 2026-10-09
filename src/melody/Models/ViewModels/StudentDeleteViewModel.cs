namespace melody.Models.ViewModels;

/// <summary>
/// Backs the student delete confirmation page — either a plain confirm prompt, or, when the
/// student still has active reservations, a blocking message listing those reservations instead.
/// </summary>
public class StudentDeleteViewModel
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// True when this student still has active reservations and deletion must be blocked.
    /// </summary>
    public bool IsBlocked => Reservations.Count > 0;

    public List<StudentReservationSummary> Reservations { get; set; } = [];
}

/// <summary>
/// Room/day/time summary of a single reservation, used to explain why a student delete is blocked.
/// </summary>
public class StudentReservationSummary
{
    public string RoomName { get; set; } = string.Empty;

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }
}
