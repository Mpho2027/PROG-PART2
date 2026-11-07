using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;

namespace RaceDay.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    /// <summary>Friendly page for error status codes such as 404 (see UseStatusCodePagesWithReExecute).</summary>
    [HttpGet, ActionName("StatusCode")]
    public IActionResult StatusCodePage(int code)
    {
        Response.StatusCode = code is >= 400 and <= 599 ? code : StatusCodes.Status404NotFound;
        return View("StatusCode", code);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
