using melody.Data;
using melody.Models.Domain;
using melody.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace melody.Controllers;

/// <summary>
/// Administrator-only teacher roster management: list, create (with a real login account),
/// rename, and delete teachers (FR-003, FR-004).
/// </summary>
[Authorize(Roles = "Administrator")]
public class AdminTeachersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public AdminTeachersController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var teachers = await _context.Teachers
            .Include(t => t.ApplicationUser)
            .Include(t => t.Students)
            .OrderBy(t => t.FullName)
            .Select(t => new TeacherListItemViewModel
            {
                Id = t.Id,
                FullName = t.FullName,
                Email = t.ApplicationUser!.Email ?? string.Empty,
                StudentCount = t.Students.Count,
            })
            .ToListAsync();

        return View(teachers);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TeacherCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            DisplayName = model.FullName,
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _userManager.AddToRoleAsync(user, "Teacher");

        try
        {
            _context.Teachers.Add(new Teacher
            {
                ApplicationUserId = user.Id,
                FullName = model.FullName,
            });

            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Compensate: never leave an ApplicationUser/Teacher-role account behind without a Teacher profile.
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "Something went wrong while creating the teacher profile. Please try again.");
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var teacher = await _context.Teachers.FindAsync(id);
        if (teacher is null)
        {
            return NotFound();
        }

        return View(new TeacherEditViewModel { Id = teacher.Id, FullName = teacher.FullName });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TeacherEditViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var teacher = await _context.Teachers.Include(t => t.ApplicationUser).FirstOrDefaultAsync(t => t.Id == id);
        if (teacher is null)
        {
            return NotFound();
        }

        teacher.FullName = model.FullName;
        if (teacher.ApplicationUser is not null)
        {
            teacher.ApplicationUser.DisplayName = model.FullName;
        }

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var teacher = await _context.Teachers.Include(t => t.Students).FirstOrDefaultAsync(t => t.Id == id);
        if (teacher is null)
        {
            return NotFound();
        }

        return View(new TeacherDeleteViewModel
        {
            Id = teacher.Id,
            FullName = teacher.FullName,
            AssignedStudentNames = teacher.Students.Select(s => s.FullName).ToList(),
        });
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var teacher = await _context.Teachers
            .Include(t => t.ApplicationUser)
            .Include(t => t.Students)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (teacher is null)
        {
            return NotFound();
        }

        if (teacher.Students.Count > 0)
        {
            return View("Delete", new TeacherDeleteViewModel
            {
                Id = teacher.Id,
                FullName = teacher.FullName,
                AssignedStudentNames = teacher.Students.Select(s => s.FullName).ToList(),
            });
        }

        var applicationUser = teacher.ApplicationUser;

        _context.Teachers.Remove(teacher);
        await _context.SaveChangesAsync();

        if (applicationUser is not null)
        {
            await _userManager.DeleteAsync(applicationUser);
        }

        return RedirectToAction(nameof(Index));
    }
}
