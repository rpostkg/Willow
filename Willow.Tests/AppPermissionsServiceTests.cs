using Willow.Services;
using Xunit;

namespace Willow.Tests;

public class AppPermissionsServiceTests
{
    [Fact]
    public void GetDisplayName_UwpKey_StripsPublisherSuffix()
    {
        var result = AppPermissionsService.GetDisplayName("Microsoft.WindowsCamera_8wekyb3d8bbwe");
        Assert.Equal("Microsoft.WindowsCamera", result);
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
