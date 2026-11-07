using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Areas.Participant.Controllers;

/// <summary>The participant's personal race history.</summary>
[Area("Participant")]
[RequireRole(Roles.Participant)]
public class ResultsController : Controller
{
    private readonly RaceDayApiClient _api;

    public ResultsController(RaceDayApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _api.GetMyResultsAsync();
        if (!result.Succeeded) TempData["Error"] = result.ErrorMessage;
        return View(result.Data ?? new List<ResultDto>());
    }
}
