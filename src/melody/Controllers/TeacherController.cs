using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace melody.Controllers;

[Authorize(Roles = "Teacher")]
public class TeacherController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
