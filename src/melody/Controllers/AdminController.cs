using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace melody.Controllers;

[Authorize(Roles = "Administrator")]
public class AdminController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
