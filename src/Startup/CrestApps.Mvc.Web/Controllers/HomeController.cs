using Microsoft.AspNetCore.Mvc;

namespace CrestApps.Mvc.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}
