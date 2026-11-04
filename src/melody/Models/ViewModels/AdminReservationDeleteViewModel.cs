namespace melody.Models.ViewModels;

/// <summary>
/// Backs the admin cancel-confirmation page, showing enough context (who/what/when) for the
/// admin to confirm they're cancelling the right reservation.
/// </summary>
public class AdminReservationDeleteViewModel
{
    public int Id { get; set; }

    public string TeacherFullName { get; set; } = string.Empty;

    public string StudentFullName { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }
}
