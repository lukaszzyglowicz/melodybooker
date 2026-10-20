using melody.Data;
using melody.Models.Domain;
using melody.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace melody.Controllers;

/// <summary>
/// Lets a Teacher book a room for one of their own students (FR-008). Scoped to the logged-in
/// teacher's own students the same way <see cref="TeacherController"/> is scoped.
/// </summary>
[Authorize(Roles = "Teacher")]
public class TeacherReservationsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TeacherReservationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Create(int studentId, int? roomId, DayOfWeek? dayOfWeek)
    {
        var teacher = await GetCurrentTeacherAsync();
        var student = await GetOwnedStudentAsync(teacher, studentId);
        if (student is null)
        {
            return NotFound();
        }

        var model = new ReservationFormViewModel
        {
            StudentId = student.Id,
            StudentFullName = student.FullName,
            StudentLessonDuration = student.LessonDuration,
            RoomId = roomId ?? 0,
            DayOfWeek = dayOfWeek ?? DayOfWeek.Monday,
            Rooms = await GetRoomSelectListAsync(),
            TimeSlots = GetTimeSlotSelectList(),
            ExistingReservationsForRoomAndDay = roomId.HasValue && dayOfWeek.HasValue
                ? await GetExistingReservationsAsync(roomId.Value, dayOfWeek.Value)
                : [],
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReservationFormViewModel model)
    {
        var teacher = await GetCurrentTeacherAsync();
        var student = await GetOwnedStudentAsync(teacher, model.StudentId);
        if (student is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return await ReRenderCreateAsync(model, student);
        }

        var roomExists = await _context.Rooms.AnyAsync(r => r.Id == model.RoomId);
        if (!roomExists)
        {
            ModelState.AddModelError(nameof(ReservationFormViewModel.RoomId), "Please select a valid room.");
            return await ReRenderCreateAsync(model, student);
        }

        var startTime = TimeSpan.FromMinutes(model.StartMinutes);
        var duration = student.LessonDuration;
        var endTime = startTime + duration;

        var existingReservations = await _context.Reservations
            .Where(r => r.RoomId == model.RoomId && r.DayOfWeek == model.DayOfWeek)
            .ToListAsync();

        var hasConflict = existingReservations
            .Any(r => Reservation.Overlaps(r.StartTime, r.EndTime, startTime, endTime));

        if (hasConflict)
        {
            ModelState.AddModelError(string.Empty, "This room is already booked at that time.");
            return await ReRenderCreateAsync(model, student);
        }

        _context.Reservations.Add(new Reservation
        {
            RoomId = model.RoomId,
            StudentId = student.Id,
            TeacherId = teacher!.Id,
            DayOfWeek = model.DayOfWeek,
            StartTime = startTime,
            Duration = duration,
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique-index race (FR-010): another booking for the same room/day/time was saved
            // concurrently. Fall back to the same generic error message.
            ModelState.AddModelError(string.Empty, "This room is already booked at that time.");
            return await ReRenderCreateAsync(model, student);
        }

        return RedirectToAction("Index", "Teacher");
    }

    private async Task<IActionResult> ReRenderCreateAsync(ReservationFormViewModel model, Student student)
    {
        model.StudentFullName = student.FullName;
        model.StudentLessonDuration = student.LessonDuration;
        model.Rooms = await GetRoomSelectListAsync();
        model.TimeSlots = GetTimeSlotSelectList();
        model.ExistingReservationsForRoomAndDay = await GetExistingReservationsAsync(model.RoomId, model.DayOfWeek);

        return View(model);
    }

    private async Task<Teacher?> GetCurrentTeacherAsync()
    {
        var userId = _userManager.GetUserId(User);
        return await _context.Teachers.FirstOrDefaultAsync(t => t.ApplicationUserId == userId);
    }

    private async Task<Student?> GetOwnedStudentAsync(Teacher? teacher, int studentId)
    {
        if (teacher is null)
        {
            return null;
        }

        return await _context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId && s.TeacherId == teacher.Id);
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

    private async Task<List<StudentReservationSummary>> GetExistingReservationsAsync(int roomId, DayOfWeek dayOfWeek)
    {
        // Ordering by TimeSpan in the database is rejected by the SQLite provider (used in the
        // test host), so the list is materialized first and ordered client-side instead.
        var reservations = await _context.Reservations
            .Where(r => r.RoomId == roomId && r.DayOfWeek == dayOfWeek)
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
}
