using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace melody.Models.ViewModels;

/// <summary>
/// Backs the admin's reservation edit form, the same way <see cref="ReservationFormViewModel"/>
/// backs the teacher's create form, minus the Student-selection concern (the student is fixed,
/// loaded by route id) and with a <see cref="Id"/> instead of a StudentId driving the
/// confirmation pin.
/// </summary>
public class AdminReservationEditViewModel
{
    /// <summary>
    /// Hidden field; drives the DB lookup of the reservation being edited and the "confirmed for
    /// this combination" pin check.
    /// </summary>
    [Required]
    public int Id { get; set; }

    [ValidateNever]
    [Display(Name = "Teacher")]
    public string TeacherFullName { get; set; } = string.Empty;

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
    /// Existing reservations for the currently-selected Room+Day, excluding the reservation being
    /// edited, shown so the admin can see what's already booked before submitting.
    /// </summary>
    [ValidateNever]
    public List<StudentReservationSummary> ExistingReservationsForRoomAndDay { get; set; } = [];

    /// <summary>
    /// Posted back from the hidden fields rendered on a warned form, pinning the admin's
    /// confirmation to the exact Room/Day/Time combination they were warned about. No
    /// ConfirmedStudentId equivalent is needed: the student is fixed by the route id, never
    /// posted back from the form.
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
    /// Controller-set only, never bound from the form: true when the just-submitted Room
    /// combination is an instrument mismatch that hasn't yet been confirmed.
    /// </summary>
    [ValidateNever]
    public bool ShowMismatchWarning { get; set; }

    [ValidateNever]
    public string? MismatchWarningMessage { get; set; }
}
