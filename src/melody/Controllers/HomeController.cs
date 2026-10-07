using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using melody.Models;

namespace melody.Controllers;

[Authorize]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.IsInRole("Administrator"))
        {
            return RedirectToAction("Index", "Admin");
        }

        if (User.IsInRole("Teacher"))
        {
            return RedirectToAction("Index", "Teacher");
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
