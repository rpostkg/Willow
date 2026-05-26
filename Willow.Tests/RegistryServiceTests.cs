using Microsoft.Win32;
using Willow.Services;
using Xunit;

namespace Willow.Tests;

public class RegistryServiceTests
{
    // ── GetRootKey ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("HKCU")]
    [InlineData("CurrentUser")]
    [InlineData("hkcu")]
    [InlineData("currentuser")]
    public void GetRootKey_CurrentUserVariants_ReturnsCurrentUser(string hive)
    {
        var key = RegistryService.GetRootKey(hive);
        Assert.Equal(Registry.CurrentUser.Name, key.Name);
    }

    [Theory]
    [InlineData("HKLM")]
    [InlineData("LocalMachine")]
    [InlineData("hklm")]
    [InlineData("anything_else")]
    public void GetRootKey_LocalMachineVariants_ReturnsLocalMachine(string hive)
    {
        var key = RegistryService.GetRootKey(hive);
        Assert.Equal(Registry.LocalMachine.Name, key.Name);
    }

    // ── ParseBinaryValue ───────────────────────────────────────────────────────

    [Fact]
    public void ParseBinaryValue_ValidThreeBytes_ReturnsByteArray()
    {
        var result = RegistryService.ParseBinaryValue("([byte[]](1,2,3))");
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void ParseBinaryValue_BoundaryBytes_ParsesCorrectly()
    {
        var result = RegistryService.ParseBinaryValue("([byte[]](0,255))");
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 0, 255 }, result);
    }

    [Fact]
    public void ParseBinaryValue_WhitespaceAroundTokens_ParsesCorrectly()
    {
        var result = RegistryService.ParseBinaryValue("([byte[]]( 10 , 20 , 30 ))");
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 10, 20, 30 }, result);
    }

    [Fact]
    public void ParseBinaryValue_SingleByte_ReturnsOneElementArray()
    {
        var result = RegistryService.ParseBinaryValue("([byte[]](42))");
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 42 }, result);
    }

    [Theory]
    [InlineData("1,2,3")]
    [InlineData("plain string")]
    [InlineData("")]
    [InlineData("[byte[]](1,2)")]
    public void ParseBinaryValue_NotPowerShellArrayFormat_ReturnsNull(string input)
    {
        var result = RegistryService.ParseBinaryValue(input);
        Assert.Null(result);
    }
}
