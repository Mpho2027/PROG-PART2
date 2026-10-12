using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Dtos;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests.Integration;

/// <summary>Participants entering events by category, and Organisers viewing enrolments.</summary>
public class EnrolmentTests : ApiTestBase
{
    public EnrolmentTests(RaceDayApiFactory factory) : base(factory) { }

    [Fact]
    public async Task Participant_CanEnrol_AndTheLinkBetweenParticipantEventAndCategoryIsRecorded()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, user) = await LoggedInAsync("Participant");

        var response = await participant.PostAsync($"/api/categories/{category.CategoryId}/enrol", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await ReadAsync<EnrolmentDto>(response);
        Assert.Equal(user.UserId, enrolment.ParticipantId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal("Confirmed", enrolment.Status);
        Assert.False(string.IsNullOrEmpty(enrolment.BibNumber));

        var stored = await Factory.WithDbAsync(db => db.EventEnrolments
            .Include(en => en.Category)
            .SingleAsync(en => en.EnrolmentId == enrolment.EnrolmentId));
        Assert.Equal(user.UserId, stored.ParticipantId);
        Assert.Equal(category.CategoryId, stored.CategoryId);
        Assert.Equal(ev.EventId, stored.Category.EventId);
    }

    [Fact]
    public async Task Enrolment_AppearsInTheParticipantsOwnList()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await LoggedInAsync("Participant");
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/users/me/enrolments", Json);

        Assert.Contains(mine!, e => e.EnrolmentId == enrolment.EnrolmentId);
    }

    [Fact]
    public async Task Participant_SeesOnlyTheirOwnEnrolments()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (alice, _) = await LoggedInAsync("Participant");
        var (bob, bobUser) = await LoggedInAsync("Participant");
        await EnrolAsync(alice, category.CategoryId);
        await EnrolAsync(bob, category.CategoryId);

        var bobs = await bob.GetFromJsonAsync<List<EnrolmentDto>>("/api/users/me/enrolments", Json);

        Assert.All(bobs!, e => Assert.Equal(bobUser.UserId, e.ParticipantId));
    }

    [Fact]
    public async Task Enrol_TwiceInTheSameCategory_Returns409()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await LoggedInAsync("Participant");
        await EnrolAsync(participant, category.CategoryId);

        var second = await participant.PostAsync($"/api/categories/{category.CategoryId}/enrol", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_InAFullCategory_Returns409()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId, maxParticipants: 1);
        await EnrolAsync((await LoggedInAsync("Participant")).Client, category.CategoryId);
        var (late, _) = await LoggedInAsync("Participant");

        var response = await late.PostAsync($"/api/categories/{category.CategoryId}/enrol", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InAMissingCategory_Returns404()
    {
        var (participant, _) = await LoggedInAsync("Participant");

        var response = await participant.PostAsync("/api/categories/999999/enrol", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InAnEventThatHasAlreadyTakenPlace_Returns400()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser, daysFromToday: -1);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await LoggedInAsync("Participant");

        var response = await participant.PostAsync($"/api/categories/{category.CategoryId}/enrol", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WhenNotLoggedIn_Returns401()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);

        var response = await NewClient().PostAsync($"/api/categories/{category.CategoryId}/enrol", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewAllEnrolmentsForTheirEvent_ByCategoryAndByEvent()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var tenK = await CreateCategoryAsync(organiser, ev.EventId);
        var twentyOneK = await CreateCategoryAsync(organiser, ev.EventId);
        await EnrolAsync((await LoggedInAsync("Participant")).Client, tenK.CategoryId);
        await EnrolAsync((await LoggedInAsync("Participant")).Client, twentyOneK.CategoryId);

        var byCategory = await organiser.GetAsync($"/api/categories/{tenK.CategoryId}/enrolments");
        var byEvent = await organiser.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, byCategory.StatusCode);
        Assert.Single(await ReadAsync<List<EnrolmentDto>>(byCategory));
        Assert.Equal(HttpStatusCode.OK, byEvent.StatusCode);
        Assert.Equal(2, (await ReadAsync<List<EnrolmentDto>>(byEvent)).Count);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsForAnotherOrganisersEvent()
    {
        var (owner, _) = await LoggedInAsync("Organiser");
        var (other, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(owner);
        var category = await CreateCategoryAsync(owner, ev.EventId);

        var byCategory = await other.GetAsync($"/api/categories/{category.CategoryId}/enrolments");
        var byEvent = await other.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, byCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byEvent.StatusCode);
    }

    [Fact]
    public async Task Participant_CanWithdrawTheirOwnEnrolment()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (participant, _) = await LoggedInAsync("Participant");
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await Factory.WithDbAsync(db => db.EventEnrolments.AnyAsync(e => e.EnrolmentId == enrolment.EnrolmentId)));
    }

    [Fact]
    public async Task Participant_CannotWithdrawSomeoneElsesEnrolment()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var (owner, _) = await LoggedInAsync("Participant");
        var (other, _) = await LoggedInAsync("Participant");
        var enrolment = await EnrolAsync(owner, category.CategoryId);

        var response = await other.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCancelAnyEnrolment()
    {
        var (organiser, _) = await LoggedInAsync("Organiser");
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync((await LoggedInAsync("Participant")).Client, category.CategoryId);
        var admin = await LoggedInAsAdminAsync();

        var response = await admin.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Withdraw_AfterAResultIsCaptured_Returns409()
    {
        var (organiser, organiserUser) = await LoggedInAsync("Organiser");
        var (participant, participantUser) = await LoggedInAsync("Participant");
        var (_, enrolmentIds) = await SeedPastEnrolmentsAsync(organiserUser.UserId, participantUser.UserId);
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentIds[0]}/result", new { finishTime = "00:45:00", position = 1 });

        var response = await participant.DeleteAsync($"/api/enrolments/{enrolmentIds[0]}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
