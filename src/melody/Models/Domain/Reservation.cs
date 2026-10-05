namespace melody.Models.Domain;

/// <summary>
/// A weekly-recurring reservation for a student's lesson, repeated for the whole school year
/// (FR-008). Double-booking the same room at the same DayOfWeek+StartTime is always hard-blocked
/// (FR-010, enforced in the application layer, not just here).
/// </summary>
public class Reservation
{
    public int Id { get; set; }

    public int RoomId { get; set; }
    public Room? Room { get; set; }

    public int StudentId { get; set; }
    public Student? Student { get; set; }

    public int TeacherId { get; set; }
    public Teacher? Teacher { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// Duration copied from Student.LessonDuration at creation time (derived from class, FR-009).
    /// </summary>
    public TimeSpan Duration { get; set; }

    public TimeSpan EndTime => StartTime + Duration;

    /// <summary>
    /// Set when the room is a specialist room that doesn't match the student's instrument.
    /// Recorded for audit/display; it never blocks the reservation (FR-011 soft enforcement).
    /// </summary>
    public bool HadInstrumentMismatchWarning { get; set; }
}
