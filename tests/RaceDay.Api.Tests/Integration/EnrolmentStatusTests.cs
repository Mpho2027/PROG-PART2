using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests.Integration;

/// <summary>Organisers changing the status of enrolments for their events.</summary>
public class EnrolmentStatusTests : ApiTestBase
{
    public EnrolmentStatusTests(RaceDayApiFactory factory) : base(factory) { }

    [Theory]
    [InlineData("Pending")]
    [InlineData("confirmed")]
    [InlineData("Cancelled")]
    public async Task Organiser_CanChangeStatus_AndParticipantSeesIt(string status)
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var (participant, _) = await LoggedInAsync("Participant");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expected = char.ToUpperInvariant(status[0]) + status[1..].ToLowerInvariant();
        Assert.Equal(expected, (await ReadAsync<EnrolmentDto>(response)).Status);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/users/me/enrolments", Json);
        Assert.Equal(expected, mine!.Single(e => e.EnrolmentId == enrolment.EnrolmentId).Status);
    }

    [Fact]
    public async Task UnknownStatus_Returns400()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var (participant, _) = await LoggedInAsync("Participant");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status = "Paid" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnotherOrganiser_CannotChangeStatus()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var (otherOrganiser, _) = await LoggedInAsync("Organiser");
        var (participant, _) = await LoggedInAsync("Participant");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await otherOrganiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status = "Cancelled" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnknownEnrolment_Returns404()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");

        var response = await organiser.PutAsJsonAsync("/api/enrolments/999999/status", new { status = "Pending" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelledEnrolment_FreesItsPlace()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var (first, _) = await LoggedInAsync("Participant");
        var (second, _) = await LoggedInAsync("Participant");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId, maxParticipants: 1);
        var enrolment = await EnrolAsync(first, category.CategoryId);

        var full = await second.PostAsync($"/api/categories/{category.CategoryId}/enrol", null);
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);

        await organiser.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status = "Cancelled" });

        var categories = await NewClient().GetFromJsonAsync<List<CategoryDto>>($"/api/events/{ev.EventId}/categories", Json);
        Assert.Equal(1, categories!.Single().SpotsRemaining);
        var retry = await second.PostAsync($"/api/categories/{category.CategoryId}/enrol", null);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task ResultCannotBeCaptured_ForACancelledEnrolment()
    {
        var (organiser, organiserUser) = await LoggedInAsync("Organiser");
        var (_, participant) = await LoggedInAsync("Participant");
        var (_, ids) = await SeedPastEnrolmentsAsync(organiserUser.UserId, participant.UserId);
        await organiser.PutAsJsonAsync($"/api/enrolments/{ids[0]}/status", new { status = "Cancelled" });

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{ids[0]}/result", new { finishTime = "00:40:00", position = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EnrolmentWithAResult_CannotBeCancelled()
    {
        var (organiser, organiserUser) = await LoggedInAsync("Organiser");
        var (_, participant) = await LoggedInAsync("Participant");
        var (_, ids) = await SeedPastEnrolmentsAsync(organiserUser.UserId, participant.UserId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{ids[0]}/result", new { finishTime = "00:40:00", position = 1 });

        var response = await organiser.PutAsJsonAsync($"/api/enrolments/{ids[0]}/status", new { status = "Cancelled" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task EventEnrolments_IncludeTheCapturedResult()
    {
        var (organiser, organiserUser) = await LoggedInAsync("Organiser");
        var (_, participant) = await LoggedInAsync("Participant");
        var (categoryId, ids) = await SeedPastEnrolmentsAsync(organiserUser.UserId, participant.UserId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{ids[0]}/result", new { finishTime = "01:02:03", position = 4 });

        var enrolments = await organiser.GetFromJsonAsync<List<EnrolmentDto>>($"/api/categories/{categoryId}/enrolments", Json);

        var row = Assert.Single(enrolments!);
        Assert.True(row.HasResult);
        Assert.Equal("01:02:03", row.FinishTime);
        Assert.Equal(4, row.Position);
    }
}
