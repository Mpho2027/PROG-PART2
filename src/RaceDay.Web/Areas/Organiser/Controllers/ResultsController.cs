using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Areas.Organiser.Controllers;

/// <summary>Capture and publish finish times and positions after an event.</summary>
public class ResultsController : OrganiserControllerBase
{
    public ResultsController(RaceDayApiClient api, UserSession session) : base(api, session) { }

    [HttpGet]
    public async Task<IActionResult> Index(int eventId)
    {
        var (ev, problem) = await LoadOwnEventAsync(eventId);
        if (problem is not null) return problem;

        var enrolments = await Api.GetEventEnrolmentsAsync(eventId);
        if (!enrolments.Succeeded) TempData["Error"] = enrolments.ErrorMessage;

        var rows = (enrolments.Data ?? new List<EnrolmentDto>())
            .Where(e => e.Status != EnrolmentStatuses.Cancelled)
            .OrderBy(e => e.CategoryName)
            .ThenBy(e => e.Position ?? int.MaxValue)
            .ThenBy(e => e.ParticipantName)
            .Select(e => new ResultRowViewModel
            {
                EnrolmentId = e.EnrolmentId,
                ParticipantName = e.ParticipantName,
                BibNumber = e.BibNumber,
                CategoryName = e.CategoryName,
                Status = e.Status,
                FinishTime = e.FinishTime,
                Position = e.Position,
                SavedFinishTime = e.FinishTime,
                SavedPosition = e.Position
            })
            .ToList();

        return View(new ResultsEntryViewModel { Event = ev!, Rows = rows });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(int eventId, ResultsEntryViewModel model)
    {
        var (ev, problem) = await LoadOwnEventAsync(eventId);
        if (problem is not null) return problem;
        model.Event = ev!;

        var saved = 0;
        for (var i = 0; i < model.Rows.Count; i++)
        {
            var row = model.Rows[i];
            var prefix = $"{nameof(model.Rows)}[{i}]";
            if (row.IsEmpty || !row.HasChanged) continue;
            if (!ModelState.IsValid && (ModelState[$"{prefix}.FinishTime"]?.Errors.Count > 0 || ModelState[$"{prefix}.Position"]?.Errors.Count > 0))
                continue;

            if (string.IsNullOrWhiteSpace(row.FinishTime) || row.Position is null)
            {
                ModelState.AddModelError(string.IsNullOrWhiteSpace(row.FinishTime) ? $"{prefix}.FinishTime" : $"{prefix}.Position",
                    "Enter both a finish time and a position.");
                continue;
            }

            var result = await Api.CaptureResultAsync(row.EnrolmentId, row.FinishTime.Trim(), row.Position.Value);
            if (result.Succeeded)
            {
                saved++;
                row.SavedFinishTime = row.FinishTime.Trim();
                row.SavedPosition = row.Position;
            }
            else
            {
                var field = result.FieldErrors.Keys.Any(k => k.Equals("FinishTime", StringComparison.OrdinalIgnoreCase)) ? "FinishTime" : "Position";
                ModelState.AddModelError($"{prefix}.{field}", result.ErrorMessage ?? "This result could not be saved.");
            }
        }

        var published = saved == 0 ? "" : $"{saved} result{(saved == 1 ? "" : "s")} saved and published. ";
        if (ModelState.ErrorCount > 0)
        {
            if (saved > 0) TempData["Success"] = published + "Participants can now see them on My Results.";
            ModelState.AddModelError(string.Empty, "Some rows were not saved. Check the highlighted results below.");
            return View(model);
        }

        TempData[saved > 0 ? "Success" : "Info"] = saved > 0
            ? published + "Participants can now see them on My Results."
            : "There were no new or changed results to save.";
        return RedirectToAction(nameof(Index), new { eventId });
    }
}
