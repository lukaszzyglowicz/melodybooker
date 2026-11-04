namespace melody.Models.ViewModels;

/// <summary>
/// Flattens a Reservation plus its related Teacher/Student/Room names into the row shape the
/// admin reservations Index view renders.
/// </summary>
public class AdminReservationListItemViewModel
{
    public int Id { get; set; }

    public string TeacherFullName { get; set; } = string.Empty;

    public string StudentFullName { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// Optional visual indicator: true when this reservation was saved despite an instrument
    /// mismatch warning (FR-011).
    /// </summary>
    public bool HadInstrumentMismatchWarning { get; set; }
}
