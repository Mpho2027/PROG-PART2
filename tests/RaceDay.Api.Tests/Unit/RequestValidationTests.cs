using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Auth;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Tests.Unit;

/// <summary>Validation rules on request models, tested without HTTP.</summary>
public class RequestValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Category_WithMinAgeAboveMaxAge_IsInvalid()
    {
        var results = Validate(new CategoryRequest { CategoryName = "Veterans", MinAge = 50, MaxAge = 40 });

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CategoryRequest.MinAge)));
    }

    [Fact]
    public void Category_AgeOnlyOrDistanceOnly_IsValid()
    {
        Assert.Empty(Validate(new CategoryRequest { CategoryName = "Under 20", MaxAge = 19 }));
        Assert.Empty(Validate(new CategoryRequest { CategoryName = "10km", DistanceKm = 10 }));
    }

    [Theory]
    [InlineData("00:52:14", true)]
    [InlineData("3:45:02", true)]
    [InlineData("23:59:59", true)]
    [InlineData("24:00:00", false)]
    [InlineData("00:60:00", false)]
    [InlineData("52:14", false)]
    public void Result_FinishTimeFormat(string finishTime, bool valid)
    {
        var results = Validate(new ResultRequest { FinishTime = finishTime, Position = 1 });

        Assert.Equal(valid, results.Count == 0);
    }

    [Fact]
    public void Event_WithoutDateTypeOrDistance_IsInvalid()
    {
        var results = Validate(new EventRequest { EventName = "Race", Location = "Pretoria" });

        var invalid = results.SelectMany(r => r.MemberNames).ToHashSet();
        Assert.Contains(nameof(EventRequest.EventDate), invalid);
        Assert.Contains(nameof(EventRequest.EventType), invalid);
        Assert.Contains(nameof(EventRequest.DistanceKm), invalid);
    }

    [Theory]
    [InlineData("Organiser", true)]
    [InlineData("participant", true)]
    [InlineData("Admin", false)]
    public void OnlyOrganiserAndParticipant_AreSelfRegisterable(string role, bool allowed)
    {
        Assert.Equal(allowed, AppRoles.Registerable.ContainsKey(role));
    }
}
