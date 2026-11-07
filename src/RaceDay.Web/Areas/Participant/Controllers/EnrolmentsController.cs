using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Areas.Participant.Controllers;

/// <summary>Event enrolments for the logged-in Participant.</summary>
[Area("Participant")]
[RequireRole(Roles.Participant)]
public class EnrolmentsController : Controller
{
    private readonly RaceDayApiClient _api;

    public EnrolmentsController(RaceDayApiClient api) => _api = api;

    /// <summary>Enters the participant into the chosen category. The API records it straight away and assigns a bib number.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enrol(int eventId, int? categoryId)
    {
        if (categoryId is null)
        {
            TempData["Error"] = "Please choose a category to enter.";
            return RedirectToAction("Details", "Events", new { area = "", id = eventId });
        }

        var result = await _api.EnrolAsync(categoryId.Value);
        if (!result.Succeeded || result.Data is null)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction("Details", "Events", new { area = "", id = eventId });
        }

        TempData["Success"] = $"You're in! You are entered in {result.Data.CategoryName} for {result.Data.EventName}. Your bib number is {result.Data.BibNumber}.";
        return RedirectToAction("Details", "Events", new { area = "", id = eventId });
    }
}
