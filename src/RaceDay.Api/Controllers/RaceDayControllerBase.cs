using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Controllers;

/// <summary>Shared helpers for RaceDay controllers.</summary>
[ApiController]
[Produces("application/json")]
public abstract class RaceDayControllerBase : ControllerBase
{
    /// <summary>The logged-in user's UserID (only valid on [Authorize] endpoints).</summary>
    protected int CurrentUserId => User.GetUserId();

    protected bool IsInRole(string role) => User.IsInRole(role);

    /// <summary>403 for a user with the right role who does not own the resource.</summary>
    protected ObjectResult NotOwner(string message) =>
        StatusCode(StatusCodes.Status403Forbidden, new MessageResponse(message));

    protected NotFoundObjectResult NotFoundMessage(string message) => NotFound(new MessageResponse(message));

    protected ConflictObjectResult ConflictMessage(string message) => Conflict(new MessageResponse(message));

    protected BadRequestObjectResult BadRequestMessage(string message) => BadRequest(new MessageResponse(message));

    protected static DateTime Today => DateTime.Today;
}
