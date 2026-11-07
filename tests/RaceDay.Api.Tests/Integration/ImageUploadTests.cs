using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests.Integration;

/// <summary>Event banners and profile pictures, stored through the API's image storage.</summary>
public class ImageUploadTests : ApiTestBase
{
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3 };
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 9, 9, 9 };

    public ImageUploadTests(RaceDayApiFactory factory) : base(factory) { }

    private static MultipartFormDataContent ImageForm(byte[] bytes, string fileName = "banner.png", string contentType = "image/png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    [Fact]
    public async Task Organiser_UploadsBanner_AndAnyoneCanViewIt()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);

        var upload = await organiser.PutAsync($"/api/events/{ev.EventId}/banner", ImageForm(Png));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.True((await ReadAsync<EventDto>(upload)).HasBanner);

        var fetched = await NewClient().GetAsync($"/api/events/{ev.EventId}/banner");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal("image/png", fetched.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await fetched.Content.ReadAsByteArrayAsync());

        var detail = await NewClient().GetFromJsonAsync<EventDto>($"/api/events/{ev.EventId}", Json);
        Assert.True(detail!.HasBanner);
    }

    [Fact]
    public async Task ReplacingTheBanner_DeletesTheOldImage()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        await organiser.PutAsync($"/api/events/{ev.EventId}/banner", ImageForm(Png));
        var first = Factory.Images.BlobNames.Single(n => n.StartsWith($"events/{ev.EventId}/"));

        await organiser.PutAsync($"/api/events/{ev.EventId}/banner", ImageForm(Jpeg, "new.jpg", "image/jpeg"));

        var remaining = Factory.Images.BlobNames.Where(n => n.StartsWith($"events/{ev.EventId}/")).ToList();
        Assert.DoesNotContain(first, remaining);
        var current = Assert.Single(remaining);
        Assert.EndsWith(".jpg", current);
    }

    [Fact]
    public async Task UploadBanner_ThatIsNotAnImage_Returns400()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PutAsync($"/api/events/{ev.EventId}/banner",
            ImageForm("this is plain text"u8.ToArray(), "fake.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((await NewClient().GetFromJsonAsync<EventDto>($"/api/events/{ev.EventId}", Json))!.HasBanner);
    }

    [Fact]
    public async Task UploadBanner_WithoutAFile_Returns400()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);

        var response = await organiser.PutAsync($"/api/events/{ev.EventId}/banner", new MultipartFormDataContent { { new StringContent("x"), "other" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ForAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await LoggedInAsync("Organiser");
        var (other, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(owner);

        var response = await other.PutAsync($"/api/events/{ev.EventId}/banner", ImageForm(Png));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ForMissingEvent_Returns404()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");

        var response = await organiser.PutAsync("/api/events/999999/banner", ImageForm(Png));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetBanner_WhenNoneWasUploaded_Returns404()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);

        var response = await NewClient().GetAsync($"/api/events/{ev.EventId}/banner");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletingAnEvent_RemovesItsBanner()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        await organiser.PutAsync($"/api/events/{ev.EventId}/banner", ImageForm(Png));

        var delete = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain(Factory.Images.BlobNames, n => n.StartsWith($"events/{ev.EventId}/"));
    }

    [Theory]
    [InlineData("Participant")]
    [InlineData("Organiser")]
    public async Task User_UploadsProfilePicture_AndCanReadItBack(string role)
    {
        var (client, _) = await LoggedInAsync(role);

        var upload = await client.PutAsync("/api/users/me/photo", ImageForm(Jpeg, "me.jpg", "image/jpeg"));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.True((await ReadAsync<UserProfileDto>(upload)).HasProfilePicture);

        var photo = await client.GetAsync("/api/users/me/photo");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Jpeg, await photo.Content.ReadAsByteArrayAsync());

        var me = await client.GetFromJsonAsync<UserProfileDto>("/api/users/me", Json);
        Assert.True(me!.HasProfilePicture);
    }

    [Fact]
    public async Task GetProfilePicture_WhenNoneWasUploaded_Returns404()
    {
        var (client, _) = await LoggedInAsync("Participant");

        var response = await client.GetAsync("/api/users/me/photo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>What the API does when Azure Blob Storage cannot be reached. Uses its own API host.</summary>
public class ImageStorageUnavailableTests : ApiTestBase
{
    public ImageStorageUnavailableTests(RaceDayApiFactory factory) : base(factory) { }

    [Fact]
    public async Task UploadWhileStorageIsDown_Returns503_AndTheEventStillWorks()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var file = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1 });
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        Factory.Images.Unavailable = true;
        try
        {
            var response = await organiser.PutAsync($"/api/events/{ev.EventId}/banner", new MultipartFormDataContent { { file, "file", "b.png" } });

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            var detail = await NewClient().GetAsync($"/api/events/{ev.EventId}");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        }
        finally
        {
            Factory.Images.Unavailable = false;
        }
    }
}
