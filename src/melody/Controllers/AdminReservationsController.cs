using melody.Data;
using melody.Models.Domain;
using melody.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace melody.Controllers;

/// <summary>
/// Administrator-only oversight of all reservations: list every booking, edit a booking's
/// Room/Day/Time (re-running the same hard double-booking block and soft instrument-mismatch
/// warning as <see cref="TeacherReservationsController"/>), and cancel a booking outright.
/// </summary>
[Authorize(Roles = "Administrator")]
public class AdminReservationsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminReservationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var reservations = await _context.Reservations
            .Include(r => r.Teacher)
            .Include(r => r.Student)
            .Include(r => r.Room)
            .Select(r => new AdminReservationListItemViewModel
            {
                Id = r.Id,
                TeacherFullName = r.Teacher!.FullName,
                StudentFullName = r.Student!.FullName,
                RoomName = r.Room!.Name,
                DayOfWeek = r.DayOfWeek,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                HadInstrumentMismatchWarning = r.HadInstrumentMismatchWarning,
            })
            .ToListAsync();

        var ordered = reservations
            .OrderBy(r => r.TeacherFullName)
            .ThenBy(r => r.DayOfWeek)
            .ThenBy(r => r.StartTime)
            .ToList();

        return View(ordered);
    }

    public async Task<IActionResult> Edit(int id, int? roomId, DayOfWeek? dayOfWeek, int? startMinutes)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Teacher)
            .Include(r => r.Student)
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation is null)
        {
            return NotFound();
        }

        // "Check availability" resubmits the admin's in-progress Room/Day/Time selection as a
        // GET (mirroring TeacherReservationsController.Create) so the table below previews that
        // combination instead of the reservation's currently-persisted values.
        var effectiveRoomId = roomId ?? reservation.RoomId;
        var effectiveDayOfWeek = dayOfWeek ?? reservation.DayOfWeek;

        var model = new AdminReservationEditViewModel
        {
            Id = reservation.Id,
            TeacherFullName = reservation.Teacher!.FullName,
            StudentFullName = reservation.Student!.FullName,
            StudentLessonDuration = reservation.Student!.LessonDuration,
            RoomId = effectiveRoomId,
            DayOfWeek = effectiveDayOfWeek,
            StartMinutes = startMinutes ?? (int)reservation.StartTime.TotalMinutes,
            Rooms = await GetRoomSelectListAsync(),
            TimeSlots = GetTimeSlotSelectList(),
            ExistingReservationsForRoomAndDay =
                await GetExistingReservationsAsync(effectiveRoomId, effectiveDayOfWeek, reservation.Id),
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminReservationEditViewModel model)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Teacher)
            .Include(r => r.Student)
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == model.Id);

        if (reservation is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return await ReRenderEditAsync(model, reservation);
        }

        var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Id == model.RoomId);
        if (room is null)
        {
            ModelState.AddModelError(nameof(AdminReservationEditViewModel.RoomId), "Please select a valid room.");
            return await ReRenderEditAsync(model, reservation);
        }

        var student = reservation.Student!;
        var startTime = TimeSpan.FromMinutes(model.StartMinutes);
        var duration = student.LessonDuration;
        var endTime = startTime + duration;

        var existingReservations = await _context.Reservations
            .Where(r => r.RoomId == model.RoomId && r.DayOfWeek == model.DayOfWeek && r.Id != model.Id)
            .ToListAsync();

        var hasConflict = existingReservations
            .Any(r => Reservation.Overlaps(r.StartTime, r.EndTime, startTime, endTime));

        if (hasConflict)
        {
            ModelState.AddModelError(string.Empty, "This room is already booked at that time.");
            return await ReRenderEditAsync(model, reservation);
        }

        var hasInstrumentMismatch = Reservation.IsInstrumentMismatch(room, student);
        var confirmedForThisCombination = model.Confirmed
            && model.ConfirmedRoomId == model.RoomId
            && model.ConfirmedDayOfWeek == model.DayOfWeek
            && model.ConfirmedStartMinutes == model.StartMinutes;

        if (hasInstrumentMismatch && !confirmedForThisCombination)
        {
            model.ShowMismatchWarning = true;
            model.MismatchWarningMessage =
                $"{room.Name} is a specialist room for {room.SpecialistInstrument}, but {student.FullName} plays {student.Instrument}. You can still book this room if needed.";
            var reservationsForRender = existingReservations
                .Select(r => new StudentReservationSummary
                {
                    RoomName = room.Name,
                    DayOfWeek = r.DayOfWeek,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                })
                .OrderBy(r => r.StartTime)
                .ToList();
            return await ReRenderEditAsync(model, reservation, reservationsForRender);
        }

        reservation.RoomId = model.RoomId;
        reservation.DayOfWeek = model.DayOfWeek;
        reservation.StartTime = startTime;
        reservation.Duration = duration;
        reservation.HadInstrumentMismatchWarning = hasInstrumentMismatch;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique-index race (FR-010): another booking for the same room/day/time was saved
            // concurrently. Fall back to the same generic error message.
            ModelState.AddModelError(string.Empty, "This room is already booked at that time.");
            return await ReRenderEditAsync(model, reservation);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Teacher)
            .Include(r => r.Student)
            .Include(r => r.Room)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation is null)
        {
            return NotFound();
        }

        return View(BuildDeleteViewModel(reservation));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var reservation = await _context.Reservations.FirstOrDefaultAsync(r => r.Id == id);
        if (reservation is null)
        {
            return NotFound();
        }

        _context.Reservations.Remove(reservation);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ReRenderEditAsync(
        AdminReservationEditViewModel model,
        Reservation reservation,
        List<StudentReservationSummary>? existingReservationsForRoomAndDay = null)
    {
        model.TeacherFullName = reservation.Teacher!.FullName;
        model.StudentFullName = reservation.Student!.FullName;
        model.StudentLessonDuration = reservation.Student!.LessonDuration;
        model.Rooms = await GetRoomSelectListAsync();
        model.TimeSlots = GetTimeSlotSelectList();
        model.ExistingReservationsForRoomAndDay = existingReservationsForRoomAndDay
            ?? await GetExistingReservationsAsync(model.RoomId, model.DayOfWeek, reservation.Id);

        return View(model);
    }

    private async Task<IEnumerable<SelectListItem>> GetRoomSelectListAsync()
    {
        return await _context.Rooms
            .OrderBy(r => r.Name)
            .Select(r => new SelectListItem
            {
                Value = r.Id.ToString(),
                Text = r.Name,
            })
            .ToListAsync();
    }

    private static IEnumerable<SelectListItem> GetTimeSlotSelectList()
    {
        var slots = new List<SelectListItem>();
        for (var minutes = 8 * 60; minutes <= 18 * 60; minutes += 15)
        {
            var time = TimeSpan.FromMinutes(minutes);
            slots.Add(new SelectListItem
            {
                Value = minutes.ToString(),
                Text = time.ToString(@"hh\:mm"),
            });
        }

        return slots;
    }

    private async Task<List<StudentReservationSummary>> GetExistingReservationsAsync(
        int roomId, DayOfWeek dayOfWeek, int excludingReservationId)
    {
        // Ordering by TimeSpan in the database is rejected by the SQLite provider (used in the
        // test host), so the list is materialized first and ordered client-side instead.
        var reservations = await _context.Reservations
            .Where(r => r.RoomId == roomId && r.DayOfWeek == dayOfWeek && r.Id != excludingReservationId)
            .Select(r => new StudentReservationSummary
            {
                RoomName = r.Room!.Name,
                DayOfWeek = r.DayOfWeek,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
            })
            .ToListAsync();

        return reservations.OrderBy(r => r.StartTime).ToList();
    }

    private static AdminReservationDeleteViewModel BuildDeleteViewModel(Reservation reservation)
    {
        return new AdminReservationDeleteViewModel
        {
            Id = reservation.Id,
            TeacherFullName = reservation.Teacher!.FullName,
            StudentFullName = reservation.Student!.FullName,
            RoomName = reservation.Room!.Name,
            DayOfWeek = reservation.DayOfWeek,
            StartTime = reservation.StartTime,
            EndTime = reservation.EndTime,
        };
    }
}
