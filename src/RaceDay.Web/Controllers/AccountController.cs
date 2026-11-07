using Microsoft.AspNetCore.Mvc;
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
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private IActionResult RedirectAfterLogin(string role, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectHome(role);

    private IActionResult RedirectHome(string role) => role == Roles.Organiser
        ? RedirectToAction("Index", "Dashboard", new { area = "Organiser" })
        : RedirectToAction("Index", "Home");
}
