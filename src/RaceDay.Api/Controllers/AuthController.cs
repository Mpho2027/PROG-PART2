using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout. Login starts a server-side session.</summary>
[Route("api/auth")]
public class AuthController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public AuthController(RaceDayDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>Creates a new user account as an Organiser or Participant.</summary>
    /// <remarks>
    /// Role must be "Organiser" or "Participant". The Admin role cannot be chosen at registration.
    /// The password is hashed (PBKDF2 with a random salt) before it is stored.
    /// </remarks>
    /// <response code="201">Account created. Returns the new user (without password).</response>
    /// <response code="400">Validation failed, e.g. an invalid email, a short password or a role that is not allowed.</response>
    /// <response code="409">The email address is already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (!AppRoles.Registerable.TryGetValue(request.Role.Trim(), out var roleId))
        {
            ModelState.AddModelError(nameof(request.Role), "Role must be either 'Organiser' or 'Participant'.");
            return ValidationProblem(ModelState);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return ConflictMessage("An account with this email address already exists.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            RoleId = roleId,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _db.Entry(user).Reference(u => u.Role).LoadAsync();

        return Created($"/api/users/{user.UserId}", UserProfileDto.From(user));
    }

    /// <summary>Logs a user in and starts an authenticated session.</summary>
    /// <remarks>
    /// On success the server stores the user's ID and role in the session and returns a session cookie.
    /// Browsers (including this Swagger page) send the cookie automatically on later requests,
    /// so every protected endpoint can be tried here straight after logging in.
    /// </remarks>
    /// <response code="200">Logged in. Returns the user's profile; the session cookie is set.</response>
    /// <response code="400">Email or password missing.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Email == email);

        var verification = user is null
            ? PasswordVerificationResult.Failed
            : _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (user is null || verification == PasswordVerificationResult.Failed)
            return Unauthorized(new MessageResponse("Invalid email or password."));

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, request.Password);
            await _db.SaveChangesAsync();
        }

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.RoleName);
        HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
        HttpContext.Session.SetString(SessionKeys.Email, user.Email);
        await HttpContext.Session.CommitAsync();

        return Ok(new LoginResponse { Message = "Login successful.", User = UserProfileDto.From(user) });
    }

    /// <summary>Ends the current session.</summary>
    /// <remarks>Clears the server-side session. Safe to call when not logged in.</remarks>
    /// <response code="200">Logged out.</response>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();
        await HttpContext.Session.CommitAsync();
        return Ok(new MessageResponse("Logged out."));
    }
}
