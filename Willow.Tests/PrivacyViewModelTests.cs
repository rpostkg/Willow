using Willow.ViewModels;
using Xunit;

namespace Willow.Tests;

public class PrivacyViewModelTests
{
    [Theory]
    [InlineData(0, 4, PrivacyScore.Low)]
    [InlineData(1, 4, PrivacyScore.Fair)]
    [InlineData(2, 4, PrivacyScore.Fair)]
    [InlineData(3, 4, PrivacyScore.Good)]
    [InlineData(4, 4, PrivacyScore.Good)]
    [InlineData(0, 0, PrivacyScore.Low)]
    public void CalculateScore_ReturnsExpectedTier(int applied, int total, PrivacyScore expected)
    {
        Assert.Equal(expected, PrivacyViewModel.CalculateScore(applied, total));
    }
}
