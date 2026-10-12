using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests.Integration;

/// <summary>Organisers capturing results; Participants viewing their own.</summary>
public class ResultTests : ApiTestBase
{
    public ResultTests(RaceDayApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Organiser_CanCaptureAResult_ThenUpdateIt()
    {
        var (organiser, organiserUser) = await LoggedInAsync("Organiser");
        var (_, participantUser) = await LoggedInAsync("Participant");
        var (_, ids) = await SeedPastEnrolmentsAsync(organiserUser.UserId, participantUser.UserId);

        var created = await organiser.PostAsJsonAsync($"/api/enrolments/{ids[0]}/result", new { finishTime = "00:52:14", position = 3 });
        var updated = await organiser.PostAsJsonAsync($"/api/enrolments/{ids[0]}/result", new { finishTime = "00:51:59", position = 2 });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("00:52:14", (await ReadAsync<ResultDto>(created)).FinishTime);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var result = await ReadAsync<ResultDto>(updated);
        Assert.Equal("00:51:59", result.FinishTime);
        Assert.Equal(2, result.Position);
        Assert.Equal("Final", result.Status);
    }

    [Fact]
    public async Task CaptureResult_BeforeTheEventHasTakenPlace_Returns400()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser, daysFromToday: 10);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync((await LoggedInAsync("Participant")).Client, category.CategoryId);

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/result", new { finishTime = "01:00:00", position = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("52 minutes", 1)]
    [InlineData("00:61:00", 1)]
    [InlineData("00:45:00", 0)]
    public async Task CaptureResult_WithInvalidTimeOrPosition_Returns400(string finishTime, int position)
    {
        var (organiser, organiserUser) = await LoggedInAsync("Organiser");
        var (_, participantUser) = await LoggedInAsync("Participant");
        var (_, ids) = await SeedPastEnrolmentsAsync(organiserUser.UserId, participantUser.UserId);

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{ids[0]}/result", new { finishTime, position });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_WithAPositionAlreadyTakenInTheCategory_Returns409()
    {
        var (organiser, organiserUser) = await LoggedInAsync("Organiser");
        var (_, p1) = await LoggedInAsync("Participant");
        var (_, p2) = await LoggedInAsync("Participant");
        var (_, ids) = await SeedPastEnrolmentsAsync(organiserUser.UserId, p1.UserId, p2.UserId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{ids[0]}/result", new { finishTime = "00:40:00", position = 1 });

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{ids[1]}/result", new { finishTime = "00:41:00", position = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForAMissingEnrolment_Returns404()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");

        var response = await organiser.PostAsJsonAsync("/api/enrolments/999999/result", new { finishTime = "00:40:00", position = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCaptureResultsForAnotherOrganisersEvent()
    {
        var (_, owner) = await LoggedInAsync("Organiser");
        var (other, _) = await LoggedInAsync("Organiser");
        var (_, participant) = await LoggedInAsync("Participant");
        var (_, ids) = await SeedPastEnrolmentsAsync(owner.UserId, participant.UserId);

        var response = await other.PostAsJsonAsync($"/api/enrolments/{ids[0]}/result", new { finishTime = "00:40:00", position = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewOnlyTheirOwnResults()
    {
        var (organiser, organiserUser) = await LoggedInAsync("Organiser");
        var (alice, aliceUser) = await LoggedInAsync("Participant");
        var (_, bobUser) = await LoggedInAsync("Participant");
        var (_, ids) = await SeedPastEnrolmentsAsync(organiserUser.UserId, aliceUser.UserId, bobUser.UserId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{ids[0]}/result", new { finishTime = "00:40:00", position = 1 });
        await organiser.PostAsJsonAsync($"/api/enrolments/{ids[1]}/result", new { finishTime = "00:42:00", position = 2 });

        var response = await alice.GetAsync("/api/users/me/results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await ReadAsync<List<ResultDto>>(response);
        var only = Assert.Single(results);
        Assert.Equal(aliceUser.UserId, only.ParticipantId);
        Assert.Equal(1, only.Position);
    }

    [Fact]
    public async Task Leaderboard_IsPublicAndOrderedByPosition()
    {
        var (organiser, organiserUser) = await LoggedInAsync("Organiser");
        var (_, p1) = await LoggedInAsync("Participant");
        var (_, p2) = await LoggedInAsync("Participant");
        var (categoryId, ids) = await SeedPastEnrolmentsAsync(organiserUser.UserId, p1.UserId, p2.UserId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{ids[0]}/result", new { finishTime = "00:44:00", position = 2 });
        await organiser.PostAsJsonAsync($"/api/enrolments/{ids[1]}/result", new { finishTime = "00:41:00", position = 1 });

        var results = await NewClient().GetFromJsonAsync<List<ResultDto>>($"/api/categories/{categoryId}/results", Json);

        Assert.Equal(new int?[] { 1, 2 }, results!.Select(r => r.Position).ToArray());
    }
}
