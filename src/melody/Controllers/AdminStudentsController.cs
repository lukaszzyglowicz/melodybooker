using melody.Data;
using melody.Models.Domain;
using melody.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace melody.Controllers;

/// <summary>
/// Administrator-only student roster management: list, create, edit (including reassigning the
/// teacher), and delete students (FR-005, FR-006).
/// </summary>
[Authorize(Roles = "Administrator")]
public class AdminStudentsController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdminStudentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var students = await _context.Students
            .Include(s => s.Teacher)
            .OrderBy(s => s.FullName)
            .Select(s => new StudentListItemViewModel
            {
                Id = s.Id,
                FullName = s.FullName,
                Class = s.Class,
                Instrument = s.Instrument.ToString(),
                TeacherFullName = s.Teacher!.FullName,
            })
            .ToListAsync();

        return View(students);
    }

    public async Task<IActionResult> Create()
    {
        return View(new StudentFormViewModel { Teachers = await GetTeacherSelectListAsync() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StudentFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Teachers = await GetTeacherSelectListAsync();
            return View(model);
        }

        _context.Students.Add(new Student
        {
            FullName = model.FullName,
            Class = model.Class,
            Instrument = model.Instrument,
            TeacherId = model.TeacherId,
        });

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var student = await _context.Students.FindAsync(id);
        if (student is null)
        {
            return NotFound();
        }

        return View(new StudentFormViewModel
        {
            Id = student.Id,
            FullName = student.FullName,
            Class = student.Class,
            Instrument = student.Instrument,
            TeacherId = student.TeacherId,
            Teachers = await GetTeacherSelectListAsync(),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StudentFormViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.Teachers = await GetTeacherSelectListAsync();
            return View(model);
        }

        var student = await _context.Students.FindAsync(id);
        if (student is null)
        {
            return NotFound();
        }

        student.FullName = model.FullName;
        student.Class = model.Class;
        student.Instrument = model.Instrument;
        student.TeacherId = model.TeacherId;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var student = await _context.Students
            .Include(s => s.Reservations)
            .ThenInclude(r => r.Room)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student is null)
        {
            return NotFound();
        }

        return View(BuildDeleteViewModel(student));
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var student = await _context.Students
            .Include(s => s.Reservations)
            .ThenInclude(r => r.Room)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student is null)
        {
            return NotFound();
        }

        if (student.Reservations.Count > 0)
        {
            return View("Delete", BuildDeleteViewModel(student));
        }

        _context.Students.Remove(student);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    private async Task<IEnumerable<SelectListItem>> GetTeacherSelectListAsync()
    {
        return await _context.Teachers
            .OrderBy(t => t.FullName)
            .Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = t.FullName,
            })
            .ToListAsync();
    }

    private static StudentDeleteViewModel BuildDeleteViewModel(Student student)
    {
        return new StudentDeleteViewModel
        {
            Id = student.Id,
            FullName = student.FullName,
            Reservations = student.Reservations.Select(r => new StudentReservationSummary
            {
                RoomName = r.Room?.Name ?? string.Empty,
                DayOfWeek = r.DayOfWeek,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
            }).ToList(),
        };
    }
}
