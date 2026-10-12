using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Controllers;

/// <summary>User profiles. Both Organisers and Participants manage their own profile here.</summary>
[Route("api/users")]
public class UsersController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db) => _db = db;

    /// <summary>Returns the profile of the currently logged-in user.</summary>
    /// <response code="200">The logged-in user's profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMe()
    {
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
        {
            // The account was removed after login; the session is stale.
            HttpContext.Session.Clear();
            return Unauthorized(new MessageResponse("Your session is no longer valid. Please log in again."));
        }
        return Ok(UserProfileDto.From(user));
    }

    /// <summary>Updates the logged-in user's own profile details.</summary>
    /// <remarks>Only the full name and phone number can be changed. Email and role are fixed.</remarks>
    /// <response code="200">Profile updated. Returns the updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
    {
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
        {
            HttpContext.Session.Clear();
            return Unauthorized(new MessageResponse("Your session is no longer valid. Please log in again."));
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        await _db.SaveChangesAsync();

        HttpContext.Session.SetString(SessionKeys.FullName, user.FullName);
        return Ok(UserProfileDto.From(user));
    }

    /// <summary>Retrieves a specific user's profile (administration).</summary>
    /// <response code="200">The requested user.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Only an Admin may look up other users.</response>
    /// <response code="404">No user with this ID.</response>
    [HttpGet("{id:int}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == id);
        return user is null ? NotFoundMessage($"User {id} was not found.") : Ok(UserProfileDto.From(user));
    }
}
