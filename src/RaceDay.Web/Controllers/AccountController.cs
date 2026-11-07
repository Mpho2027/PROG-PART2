using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services;
using RaceDay.Web.ViewModels;

namespace RaceDay.Web.Controllers;

/// <summary>Registration, login and logout. Every check is done by the API; this controller keeps the session.</summary>
public class AccountController : Controller
{
    private readonly RaceDayApiClient _api;
    private readonly UserSession _session;

    public AccountController(RaceDayApiClient api, UserSession session)
    {
        _api = api;
        _session = session;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_session.User is { } user) return RedirectHome(user.Role);
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Start from a clean session so an old API session is never reused for a new login.
        _session.SignOut();
        var result = await _api.LoginAsync(model.Email, model.Password);
        if (!result.Succeeded || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Invalid email or password.");
            model.Password = string.Empty;
            return View(model);
        }

        _session.SignIn(result.Data.User);
        TempData["Success"] = $"Welcome back, {_session.User!.FirstName}.";
        return RedirectAfterLogin(result.Data.User.Role, model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (_session.User is { } user) return RedirectHome(user.Role);
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var registered = await _api.RegisterAsync(model.FullName, model.Email, model.Password, model.Role, model.PhoneNumber);
        if (!registered.Succeeded)
        {
            if (registered.StatusCode == System.Net.HttpStatusCode.Conflict)
                ModelState.AddModelError(nameof(model.Email), registered.ErrorMessage ?? "That email address is already registered.");
            else
                ModelState.AddApiErrors(registered);
            return View(model);
        }

        // Log the new user straight in.
        _session.SignOut();
        var login = await _api.LoginAsync(model.Email, model.Password);
        if (!login.Succeeded || login.Data is null)
        {
            TempData["Success"] = "Your account was created. Please log in.";
            return RedirectToAction(nameof(Login));
        }

        _session.SignIn(login.Data.User);
        TempData["Success"] = $"Welcome to RaceDay, {_session.User!.FirstName}. Your {model.Role.ToLowerInvariant()} account is ready.";
        return RedirectHome(login.Data.User.Role);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await _api.LogoutAsync();
        }
        catch (Exception ex) when (ex is ApiUnavailableException or ApiSessionExpiredException)
        {
            // The local session is cleared below either way.
        }

        _session.SignOut();
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [RequireRole]
    public async Task<IActionResult> Profile()
    {
        var profile = await _api.GetProfileAsync();
        if (!profile.Succeeded || profile.Data is null)
        {
            TempData["Error"] = profile.ErrorMessage;
            return RedirectToAction("Index", "Home");
        }
        _session.Update(profile.Data);
        return View(ProfileViewModel.From(profile.Data));
    }

    [HttpPost]
    [RequireRole]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(await WithReadOnlyFieldsAsync(model));

        var result = await _api.UpdateProfileAsync(model.FullName, model.PhoneNumber);
        if (!result.Succeeded || result.Data is null)
        {
            ModelState.AddApiErrors(result);
            return View(await WithReadOnlyFieldsAsync(model));
        }

        _session.Update(result.Data);
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Profile));
    }

    /// <summary>Sends a new profile picture to the API, which stores it in Azure Blob Storage.</summary>
    [HttpPost]
    [RequireRole]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ProfilePicture(IFormFile? picture)
    {
        if (picture is null || picture.Length == 0)
        {
            TempData["Error"] = "Please choose an image to upload.";
            return RedirectToAction(nameof(Profile));
        }

        var result = await _api.UploadProfilePictureAsync(picture);
        if (result.Succeeded && result.Data is not null)
        {
            _session.Update(result.Data);
            TempData["Success"] = "Your profile picture has been updated.";
        }
        else
        {
            TempData["Error"] = result.ErrorMessage;
        }
        return RedirectToAction(nameof(Profile));
    }

    /// <summary>Streams the logged-in user's profile picture from the API.</summary>
    [HttpGet]
    [RequireRole]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Photo()
    {
        var image = await _api.GetProfilePictureAsync();
        return image is null ? NotFound() : File(image.Content, image.ContentType);
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    /// <summary>Refills the fields the form does not post (email, role, picture) after a failed update.</summary>
    private async Task<ProfileViewModel> WithReadOnlyFieldsAsync(ProfileViewModel model)
    {
        var profile = await _api.GetProfileAsync();
        if (profile.Data is { } p)
        {
            model.Email = p.Email;
            model.Role = p.Role;
            model.MemberSince = p.CreatedAt;
            model.HasProfilePicture = p.HasProfilePicture;
        }
        return model;
    }

    private IActionResult RedirectAfterLogin(string role, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectHome(role);

    private IActionResult RedirectHome(string role) => role == Roles.Organiser
        ? RedirectToAction("Index", "Dashboard", new { area = "Organiser" })
        : RedirectToAction("Index", "Home");
}
