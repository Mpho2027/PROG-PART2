using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Areas.Organiser.Controllers;

/// <summary>Create, edit and delete events. Banner images are passed to the API, which stores them in Azure Blob Storage.</summary>
public class EventsController : OrganiserControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    public EventsController(RaceDayApiClient api, UserSession session) : base(api, session) { }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await Api.GetMyEventsAsync();
        if (!result.Succeeded) TempData["Error"] = result.ErrorMessage;
        return View(result.Data ?? new List<OrganiserEventSummaryDto>());
    }

    [HttpGet]
    public IActionResult Create() => View(new EventFormViewModel { EventDate = DateTime.Today.AddDays(30), EventType = EventType.Run });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var created = await Api.CreateEventAsync(model.ToRequest());
        if (!created.Succeeded || created.Data is null)
        {
            ModelState.AddApiErrors(created);
            return View(model);
        }

        var eventId = created.Data.EventId;
        if (!await TryUploadBannerAsync(eventId, model.BannerImage, saved: "Event created"))
            return RedirectToAction(nameof(Edit), new { id = eventId });

        TempData["Success"] = $"{created.Data.EventName} has been created. Now add the categories participants can enter.";
        return RedirectToAction("Index", "Categories", new { eventId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var (ev, problem) = await LoadOwnEventAsync(id);
        return problem ?? View(EventFormViewModel.From(ev!));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        var (ev, problem) = await LoadOwnEventAsync(id);
        if (problem is not null) return problem;

        model.EventId = id;
        model.HasBanner = ev!.HasBanner;
        if (!ModelState.IsValid) return View(model);

        var updated = await Api.UpdateEventAsync(id, model.ToRequest());
        if (!updated.Succeeded)
        {
            ModelState.AddApiErrors(updated);
            return View(model);
        }

        if (!await TryUploadBannerAsync(id, model.BannerImage, saved: "Event details saved"))
            return RedirectToAction(nameof(Edit), new { id });

        TempData["Success"] = $"{model.EventName} has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var (ev, problem) = await LoadOwnEventAsync(id);
        if (problem is not null) return problem;

        var categories = await Api.GetCategoriesAsync(id);
        ViewBag.Entries = categories.Data?.Sum(c => c.EnrolledCount) ?? 0;
        return View(ev);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, string? eventName)
    {
        var result = await Api.DeleteEventAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"{eventName ?? "The event"} has been deleted."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Uploads the banner if one was chosen. Returns false (with an error message) when the upload failed.</summary>
    private async Task<bool> TryUploadBannerAsync(int eventId, IFormFile? banner, string saved)
    {
        if (banner is null || banner.Length == 0) return true;

        var upload = await Api.UploadBannerAsync(eventId, banner);
        if (upload.Succeeded) return true;

        TempData["Error"] = $"{saved}, but the banner image was not uploaded: {upload.ErrorMessage}";
        return false;
    }
}
