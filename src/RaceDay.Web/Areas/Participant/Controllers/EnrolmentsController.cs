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

    /// <summary>All events the participant has entered, with their enrolment status.</summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _api.GetMyEnrolmentsAsync();
        if (!result.Succeeded) TempData["Error"] = result.ErrorMessage;
        return View(result.Data ?? new List<EnrolmentDto>());
    }

    /// <summary>Withdraws from an event. The API only allows this for the participant's own enrolments without a result.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int id, string? eventName)
    {
        var result = await _api.WithdrawAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"You have withdrawn from {eventName ?? "the event"}."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

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
