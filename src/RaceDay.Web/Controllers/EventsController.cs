using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

/// <summary>Public event pages. Banner images are fetched from the API, which reads them from Azure Blob Storage.</summary>
public class EventsController : Controller
{
    private readonly RaceDayApiClient _api;
    private readonly UserSession _session;

    public EventsController(RaceDayApiClient api, UserSession session)
    {
        _api = api;
        _session = session;
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ev = await _api.GetEventAsync(id);
        if (ev.IsNotFound || ev.Data is null) return NotFound();

        var categories = await _api.GetCategoriesAsync(id);
        var model = new EventDetailsViewModel
        {
            Event = ev.Data,
            Categories = categories.Data ?? new List<CategoryDto>(),
            IsOwner = _session.User is { IsOrganiser: true } user && user.UserId == ev.Data.OrganiserId
        };

        if (_session.User?.IsParticipant == true)
        {
            var mine = await _api.GetMyEnrolmentsAsync();
            model.MyEnrolments = (mine.Data ?? new List<EnrolmentDto>())
                .Where(e => e.EventId == id)
                .ToDictionary(e => e.CategoryId);
        }

        if (ev.Data.IsPast)
        {
            var results = new Dictionary<int, List<ResultDto>>();
            foreach (var category in model.Categories)
            {
                var leaderboard = await _api.GetCategoryResultsAsync(category.CategoryId);
                if (leaderboard.Data is { Count: > 0 }) results[category.CategoryId] = leaderboard.Data;
            }
            model.Results = results;
        }

        return View(model);
    }

    /// <summary>Streams the event's banner image through the MVC app (the browser never talks to Azure).</summary>
    [HttpGet]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Banner(int id)
    {
        var image = await _api.GetBannerAsync(id);
        return image is null ? NotFound() : File(image.Content, image.ContentType);
    }
}
