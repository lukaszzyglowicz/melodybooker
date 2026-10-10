using melody.Data;
using melody.Models.Domain;
using melody.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace melody.Controllers;

[Authorize(Roles = "Teacher")]
public class TeacherController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TeacherController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.ApplicationUserId == userId);

        if (teacher is null)
        {
            return View(new List<TeacherStudentListItemViewModel>());
        }

        var students = await _context.Students
            .Where(s => s.TeacherId == teacher.Id)
            .OrderBy(s => s.FullName)
            .Select(s => new TeacherStudentListItemViewModel
            {
                Id = s.Id,
                FullName = s.FullName,
                Class = s.Class,
                Instrument = s.Instrument.ToString(),
            })
            .ToListAsync();

        return View(students);
    }
}
