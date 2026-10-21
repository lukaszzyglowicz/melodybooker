using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace melody.Models.ViewModels;

/// <summary>
/// Backs the teacher's room-booking form. The student is fixed (chosen via the entry link, not
/// re-selectable); Room/Day/Time are the teacher's choices. Controller-populated
/// dropdown/summary collections must never be posted-back-validated.
/// </summary>
public class ReservationFormViewModel
{
    [Required]
    public int StudentId { get; set; }

    [ValidateNever]
    [Display(Name = "Student")]
    public string StudentFullName { get; set; } = string.Empty;

    [ValidateNever]
    [Display(Name = "Lesson duration")]
    public TimeSpan StudentLessonDuration { get; set; }

    [Required]
    [Display(Name = "Room")]
    public int RoomId { get; set; }

    [Required]
    [EnumDataType(typeof(DayOfWeek))]
    [Display(Name = "Day")]
    public DayOfWeek DayOfWeek { get; set; }

    /// <summary>
    /// Minutes since midnight, bound from the time-slot select.
    /// </summary>
    [Required]
    [Range(0, 1439)]
    [Display(Name = "Start time")]
    public int StartMinutes { get; set; }

    [ValidateNever]
    public IEnumerable<SelectListItem> Rooms { get; set; } = [];

    [ValidateNever]
    public IEnumerable<SelectListItem> TimeSlots { get; set; } = [];

    /// <summary>
    /// Existing reservations for the currently-selected Room+Day, shown so the teacher can see
    /// what's already booked before submitting.
    /// </summary>
    [ValidateNever]
    public List<StudentReservationSummary> ExistingReservationsForRoomAndDay { get; set; } = [];

    /// <summary>
    /// Posted back from the hidden fields rendered on a warned form, pinning the teacher's
    /// confirmation to the exact Room/Day/Time combination they were warned about.
    /// </summary>
    [ValidateNever]
    public bool Confirmed { get; set; }

    [ValidateNever]
    public int? ConfirmedRoomId { get; set; }

    [ValidateNever]
    public DayOfWeek? ConfirmedDayOfWeek { get; set; }

    [ValidateNever]
    public int? ConfirmedStartMinutes { get; set; }

    /// <summary>
    /// Controller-set only, never bound from the form: true when the just-submitted Room/Student
    /// combination is an instrument mismatch that hasn't yet been confirmed.
    /// </summary>
    [ValidateNever]
    public bool ShowMismatchWarning { get; set; }

    [ValidateNever]
    public string? MismatchWarningMessage { get; set; }
}
