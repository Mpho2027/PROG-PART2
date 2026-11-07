using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Areas.Organiser.Controllers;

/// <summary>Everyone enrolled in one of the organiser's events, with their category and status.</summary>
public class EnrolmentsController : OrganiserControllerBase
{
    public EnrolmentsController(RaceDayApiClient api, UserSession session) : base(api, session) { }

    [HttpGet]
    public async Task<IActionResult> Index(int eventId, int? categoryId, string? status)
    {
        var (ev, problem) = await LoadOwnEventAsync(eventId);
        if (problem is not null) return problem;

        var enrolments = await Api.GetEventEnrolmentsAsync(eventId);
        if (!enrolments.Succeeded) TempData["Error"] = enrolments.ErrorMessage;
        var categories = await Api.GetCategoriesAsync(eventId);

        var all = enrolments.Data ?? new List<EnrolmentDto>();
        var shown = all
            .Where(e => categoryId is null || e.CategoryId == categoryId)
            .Where(e => string.IsNullOrEmpty(status) || e.Status == status)
            .ToList();

        return View(new EventEnrolmentsViewModel
        {
            Event = ev!,
            Categories = categories.Data ?? new List<CategoryDto>(),
            AllEnrolments = all,
            Enrolments = shown,
            CategoryId = categoryId,
            Status = status
        });
    }

    /// <summary>Changes an enrolment's status (Pending, Confirmed or Cancelled) through the API.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, int eventId, string status, string? participantName, int? categoryId, string? filterStatus)
    {
        var result = await Api.UpdateEnrolmentStatusAsync(id, status);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"{participantName ?? "The participant"} is now {status.ToLowerInvariant()}."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index), new { eventId, categoryId, status = filterStatus });
    }
}
