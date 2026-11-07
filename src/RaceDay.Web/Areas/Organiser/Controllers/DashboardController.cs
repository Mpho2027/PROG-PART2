using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Areas.Organiser.Controllers;

/// <summary>Summary of the organiser's events, enrolments and upcoming dates.</summary>
public class DashboardController : OrganiserControllerBase
{
    public DashboardController(RaceDayApiClient api, UserSession session) : base(api, session) { }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyEventsAsync();
        if (!result.Succeeded) TempData["Error"] = result.ErrorMessage;
        return View(result.Data ?? new List<OrganiserEventSummaryDto>());
    }
}
