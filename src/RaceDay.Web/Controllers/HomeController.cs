using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

/// <summary>The events home page: upcoming events with filters.</summary>
public class HomeController : Controller
{
    private readonly RaceDayApiClient _api;

    public HomeController(RaceDayApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] EventFilterViewModel filter)
    {
        var model = new EventsIndexViewModel { Filter = filter };
        if (!ModelState.IsValid)
        {
            model.ErrorMessage = "Please check the search filters.";
            return View(model);
        }

        var result = await _api.GetEventsAsync(new EventFilter
        {
            Search = filter.Search,
            EventType = filter.EventType,
            From = filter.From,
            To = filter.To
        });

        if (result.Succeeded) model.Events = result.Data ?? new List<EventDto>();
        else model.ErrorMessage = result.ErrorMessage;
        return View(model);
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
