using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Areas.Organiser.Controllers;

/// <summary>Age or distance categories for one of the organiser's events.</summary>
public class CategoriesController : OrganiserControllerBase
{
    public CategoriesController(RaceDayApiClient api, UserSession session) : base(api, session) { }

    [HttpGet]
    public async Task<IActionResult> Index(int eventId)
    {
        var (ev, problem) = await LoadOwnEventAsync(eventId);
        if (problem is not null) return problem;

        var categories = await Api.GetCategoriesAsync(eventId);
        return View(new CategoriesPageViewModel { Event = ev!, Categories = categories.Data ?? new List<CategoryDto>() });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int eventId)
    {
        var (ev, problem) = await LoadOwnEventAsync(eventId);
        return problem ?? View(new CategoryFormViewModel { EventId = eventId, EventName = ev!.EventName });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int eventId, CategoryFormViewModel model)
    {
        var (ev, problem) = await LoadOwnEventAsync(eventId);
        if (problem is not null) return problem;

        model.EventId = eventId;
        model.EventName = ev!.EventName;
        if (!ModelState.IsValid) return View(model);

        var result = await Api.CreateCategoryAsync(eventId, model.ToRequest());
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result);
            return View(model);
        }

        TempData["Success"] = $"{model.CategoryName} has been added to {ev.EventName}.";
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, int eventId)
    {
        var (_, problem) = await LoadOwnEventAsync(eventId);
        if (problem is not null) return problem;

        var category = (await Api.GetCategoriesAsync(eventId)).Data?.FirstOrDefault(c => c.CategoryId == id);
        return category is null ? NotFound() : View(CategoryFormViewModel.From(category));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, int eventId, CategoryFormViewModel model)
    {
        var (ev, problem) = await LoadOwnEventAsync(eventId);
        if (problem is not null) return problem;

        model.CategoryId = id;
        model.EventId = eventId;
        model.EventName = ev!.EventName;
        if (!ModelState.IsValid) return View(model);

        var result = await Api.UpdateCategoryAsync(id, model.ToRequest());
        if (!result.Succeeded)
        {
            ModelState.AddApiErrors(result);
            return View(model);
        }

        TempData["Success"] = $"{model.CategoryName} has been updated.";
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int eventId, string? categoryName)
    {
        var result = await Api.DeleteCategoryAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"{categoryName ?? "The category"} has been removed."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Index), new { eventId });
    }
}
