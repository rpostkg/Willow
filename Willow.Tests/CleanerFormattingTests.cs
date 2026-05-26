using Willow.ViewModels;
using Xunit;

namespace Willow.Tests;

public class CleanerFormattingTests
{
    [Theory]
    [InlineData(0,                      "0.00 B")]
    [InlineData(1,                      "1.00 B")]
    [InlineData(1023,                   "1023.00 B")]
    [InlineData(1024,                   "1.00 KB")]
    [InlineData(1536,                   "1.50 KB")]
    [InlineData(1024 * 1024,            "1.00 MB")]
    [InlineData(1024L * 1024 * 1024,    "1.00 GB")]
    [InlineData(1024L * 1024 * 1024 * 1024, "1.00 TB")]
    public void FormatSize_KnownValues_ReturnsExpectedString(long bytes, string expected)
    {
        Assert.Equal(expected, CleanerViewModel.FormatSize(bytes));
    }

    [Fact]
    public void FormatSize_HalfGigabyte_ReturnsCorrectDecimal()
    {
        long halfGb = 512L * 1024 * 1024;
        Assert.Equal("512.00 MB", CleanerViewModel.FormatSize(halfGb));
    }

    [Fact]
    public void FormatSize_JustBelowOneMb_StaysInKb()
    {
        long justBelow = 1024 * 1024 - 1;
        var result = CleanerViewModel.FormatSize(justBelow);
        Assert.EndsWith("KB", result);
    }
}
