using RaceDay.Web.Infrastructure;

namespace RaceDay.Web.Tests.Unit;

public class DisplayTests
{
    [Theory]
    [InlineData(1, "1st")]
    [InlineData(2, "2nd")]
    [InlineData(3, "3rd")]
    [InlineData(4, "4th")]
    [InlineData(11, "11th")]
    [InlineData(12, "12th")]
    [InlineData(13, "13th")]
    [InlineData(21, "21st")]
    [InlineData(47, "47th")]
    [InlineData(102, "102nd")]
    [InlineData(111, "111th")]
    public void Ordinal_UsesTheRightSuffix(int number, string expected) => Assert.Equal(expected, Display.Ordinal(number));

    [Fact]
    public void Placing_ShowsPositionOutOfFinishers()
    {
        Assert.Equal("47th of 312 finishers", Display.Placing(47, 312));
        Assert.Equal("1st of 1 finisher", Display.Placing(1, 1));
    }

    [Theory]
    [InlineData(null, 19, "Under 20")]
    [InlineData(40, null, "40+")]
    [InlineData(20, 39, "Ages 20 to 39")]
    [InlineData(null, null, "")]
    public void AgeRange_DescribesAgeCategories(int? min, int? max, string expected) =>
        Assert.Equal(expected, Display.AgeRange(min, max));

    [Theory]
    [InlineData(0, "Free")]
    [InlineData(150, "R150")]
    [InlineData(1250.5, "R1,250.50")]
    public void Rand_FormatsEntryFees(decimal amount, string expected) => Assert.Equal(expected, Display.Rand(amount));
}
