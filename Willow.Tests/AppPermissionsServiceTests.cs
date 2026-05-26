using Willow.Services;
using Xunit;

namespace Willow.Tests;

public class AppPermissionsServiceTests
{
    [Fact]
    public void GetDisplayName_UwpKeyKnownPackage_ReturnsDisplayNameWithoutPublisherId()
    {
        // PackageManager resolves Microsoft.WindowsCamera to its friendly name (e.g. "Windows Camera").
        // Either way the publisher ID suffix must not appear in the result.
        var result = AppPermissionsService.GetDisplayName("Microsoft.WindowsCamera_8wekyb3d8bbwe");
        Assert.NotEmpty(result);
        Assert.DoesNotContain("_8wekyb3d8bbwe", result);
    }

    [Fact]
    public void GetDisplayName_UwpKeyUnknownPackage_StripsPublisherSuffix()
    {
        // PackageManager can't resolve a fake PFN; fallback strips the "_publisherId" part.
        var result = AppPermissionsService.GetDisplayName("SomeFakeApp.That.Does.Not.Exist_8wekyb3d8bbwe");
        Assert.Equal("SomeFakeApp.That.Does.Not.Exist", result);
    }

    [Fact]
    public void GetDisplayName_UwpKeyNoUnderscore_ReturnsAsIs()
    {
        var result = AppPermissionsService.GetDisplayName("SomeApp");
        Assert.Equal("SomeApp", result);
    }

    [Fact]
    public void GetDisplayName_NonPackagedKey_ExtractsFilenameWithoutExtension()
    {
        var result = AppPermissionsService.GetDisplayName(@"NonPackaged\C:#Windows#System32#notepad.exe");
        Assert.Equal("notepad", result);
    }

    [Fact]
    public void GetDisplayName_NonPackagedNoExtension_ExtractsFilename()
    {
        var result = AppPermissionsService.GetDisplayName(@"NonPackaged\C:#Program Files#MyApp#myapp");
        Assert.Equal("myapp", result);
    }
}
