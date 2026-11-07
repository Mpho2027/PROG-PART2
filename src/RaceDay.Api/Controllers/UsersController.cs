using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Storage;

namespace RaceDay.Api.Controllers;

/// <summary>User profiles. Both Organisers and Participants manage their own profile here.</summary>
[Route("api/users")]
public class UsersController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IImageStorage _images;
    private readonly ILogger<UsersController> _logger;

    public UsersController(RaceDayDbContext db, IImageStorage images, ILogger<UsersController> logger)
    {
        _db = db;
        _images = images;
        _logger = logger;
    }

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

    /// <summary>Uploads or replaces the logged-in user's profile picture (stored in Azure Blob Storage).</summary>
    /// <remarks>
    /// Send the image as multipart/form-data in a field named "file". JPG, PNG, GIF and WEBP images up to 5 MB are accepted.
    /// </remarks>
    /// <response code="200">Picture saved. Returns the profile (hasProfilePicture is true).</response>
    /// <response code="400">No file, not a supported image, or larger than 5 MB.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="503">Azure Blob Storage is not reachable or not configured.</response>
    [HttpPut("me/photo")]
    [Authorize]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadPhoto([FromForm] ImageUploadRequest request)
    {
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
        {
            HttpContext.Session.Clear();
            return Unauthorized(new MessageResponse("Your session is no longer valid. Please log in again."));
        }
        if (!ImageRules.TryValidate(request.File, out var contentType, out var extension, out var error))
            return BadRequestMessage(error);

        string blobName;
        try
        {
            await using var stream = request.File!.OpenReadStream();
            blobName = await _images.SaveAsync($"profiles/{user.UserId}", stream, contentType, extension);
        }
        catch (ImageStorageException ex)
        {
            return StorageUnavailable(ex);
        }

        var previous = user.ProfileImageName;
        user.ProfileImageName = blobName;
        await _db.SaveChangesAsync();
        if (previous is not null)
        {
            try { await _images.DeleteAsync(previous); }
            catch (ImageStorageException ex) { _logger.LogWarning(ex, "Old profile picture {BlobName} could not be deleted", previous); }
        }

        return Ok(UserProfileDto.From(user));
    }

    /// <summary>Returns the logged-in user's profile picture.</summary>
    /// <response code="200">The image file.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">No profile picture has been uploaded.</response>
    /// <response code="503">Azure Blob Storage is not reachable or not configured.</response>
    [HttpGet("me/photo")]
    [Authorize]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPhoto()
    {
        var blobName = await _db.Users.Where(u => u.UserId == CurrentUserId).Select(u => u.ProfileImageName).SingleOrDefaultAsync();
        if (blobName is null) return NotFoundMessage("No profile picture has been uploaded.");

        try
        {
            var image = await _images.GetAsync(blobName);
            return image is null ? NotFoundMessage("No profile picture has been uploaded.") : Image(image);
        }
        catch (ImageStorageException ex)
        {
            return StorageUnavailable(ex);
        }
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
