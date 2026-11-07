using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;

namespace RaceDay.Web.Areas.Organiser.Controllers;

/// <summary>Base for every Organiser page: only logged-in Organisers get in.</summary>
[Area("Organiser")]
[RequireRole(Roles.Organiser)]
public abstract class OrganiserControllerBase : Controller
{
    protected OrganiserControllerBase(RaceDayApiClient api, UserSession session)
    {
        Api = api;
        CurrentUser = session;
    }

    protected RaceDayApiClient Api { get; }
    protected UserSession CurrentUser { get; }

    /// <summary>
    /// Loads an event for one of the organiser pages. Returns the event, or the page to show instead
    /// (not found, or access denied when the event belongs to another organiser).
    /// </summary>
    protected async Task<(EventDto? Event, IActionResult? Problem)> LoadOwnEventAsync(int eventId)
    {
        var result = await Api.GetEventAsync(eventId);
        if (result.IsNotFound || result.Data is null) return (null, NotFound());
        if (result.Data.OrganiserId != CurrentUser.User?.UserId)
            return (null, RedirectToAction("AccessDenied", "Account", new { area = "" }));
        return (result.Data, null);
    }
}
