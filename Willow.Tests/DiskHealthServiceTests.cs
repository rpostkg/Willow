using System;
using Willow.Models;
using Willow.Services;
using Xunit;

namespace Willow.Tests;

public class DiskHealthServiceTests
{
    // ── DeriveMediaType ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("", "Samsung NVMe SSD")]
    [InlineData("Fixed hard disk media", "WD Black NVMe")]
    [InlineData("", "nvme drive")]
    public void DeriveMediaType_NvmeInModelName_ReturnsNvme(string wmiType, string model)
    {
        Assert.Equal("NVMe", DiskHealthService.DeriveMediaType(wmiType, model));
    }

    [Theory]
    [InlineData("", "Samsung SSD 870")]
    [InlineData("", "Crucial Solid State Drive")]
    public void DeriveMediaType_SsdInModelName_ReturnsSsd(string wmiType, string model)
    {
        Assert.Equal("SSD", DiskHealthService.DeriveMediaType(wmiType, model));
    }

    [Fact]
    public void DeriveMediaType_FixedHardDiskPlainModel_ReturnsHdd()
    {
        Assert.Equal("HDD", DiskHealthService.DeriveMediaType("Fixed hard disk media", "WD Blue 1TB"));
    }

    [Fact]
    public void DeriveMediaType_UnknownWmiAndPlainModel_ReturnsSsdFallback()
    {
        Assert.Equal("SSD", DiskHealthService.DeriveMediaType("", "Generic Drive"));
    }

    // ── ParseAttributes ────────────────────────────────────────────────────────

    // Helper: build a 362-byte SMART vendor-specific blob with one attribute at slot 0.
    // Layout per record (12 bytes): [id, flags_lo, flags_hi, current, worst, raw0..raw3, 0, 0, 0]
    // The raw 32-bit value lives at offset+5 (raw0..raw3).
    private static byte[] MakeSmartBlob(byte attrId, uint rawValue, byte rawByte5 = 0)
    {
        var blob = new byte[362];
        int offset = 2; // first attribute record starts at byte 2
        blob[offset] = attrId;
        var raw = BitConverter.GetBytes(rawValue);
        blob[offset + 5] = raw[0];
        blob[offset + 6] = raw[1];
        blob[offset + 7] = raw[2];
        blob[offset + 8] = raw[3];
        if (rawByte5 != 0) blob[offset + 5] = rawByte5; // for temperature (byte at offset+5)
        return blob;
    }

    [Fact]
    public void ParseAttributes_TooShortBlob_LeavesInfoUnchanged()
    {
        var info = new DiskDriveInfo { Health = HealthStatus.Good };
        DiskHealthService.ParseAttributes(info, new byte[100], "caution", "bad");
        Assert.Null(info.ReallocatedSectors);
        Assert.Null(info.PowerOnHours);
        Assert.Equal(HealthStatus.Good, info.Health);
    }

    [Fact]
    public void ParseAttributes_Attr9_SetsPowerOnHours()
    {
        var blob = MakeSmartBlob(9, 1234);
        var info = new DiskDriveInfo();
        DiskHealthService.ParseAttributes(info, blob, "caution", "bad");
        Assert.Equal((long)1234, info.PowerOnHours);
    }

    [Fact]
    public void ParseAttributes_Attr12_SetsPowerCycleCount()
    {
        var blob = MakeSmartBlob(12, 99);
        var info = new DiskDriveInfo();
        DiskHealthService.ParseAttributes(info, blob, "caution", "bad");
        Assert.Equal(99, info.PowerCycleCount);
    }

    [Fact]
    public void ParseAttributes_Attr194_SetsTemperature()
    {
        // Temperature is stored in the raw byte at offset+5 directly
        var blob = MakeSmartBlob(194, 0, rawByte5: 42);
        var info = new DiskDriveInfo();
        DiskHealthService.ParseAttributes(info, blob, "caution", "bad");
        Assert.Equal(42, info.TemperatureCelsius);
    }

    [Fact]
    public void ParseAttributes_Attr5_ZeroReallocated_HealthUnchanged()
    {
        var blob = MakeSmartBlob(5, 0);
        var info = new DiskDriveInfo { Health = HealthStatus.Good };
        DiskHealthService.ParseAttributes(info, blob, "caution", "bad");
        Assert.Equal(0, info.ReallocatedSectors);
        Assert.Equal(HealthStatus.Good, info.Health);
    }

    [Fact]
    public void ParseAttributes_Attr5_NonZeroReallocated_WhenGood_UpgradesToCaution()
    {
        var blob = MakeSmartBlob(5, 3);
        var info = new DiskDriveInfo { Health = HealthStatus.Good };
        DiskHealthService.ParseAttributes(info, blob, "CAUTION_TEXT", "bad");
        Assert.Equal(3, info.ReallocatedSectors);
        Assert.Equal(HealthStatus.Caution, info.Health);
        Assert.Equal("CAUTION_TEXT", info.HealthText);
    }

    [Fact]
    public void ParseAttributes_Attr5_NonZeroReallocated_WhenBad_HealthStaysBad()
    {
        var blob = MakeSmartBlob(5, 3);
        var info = new DiskDriveInfo { Health = HealthStatus.Bad };
        DiskHealthService.ParseAttributes(info, blob, "caution", "bad");
        Assert.Equal(HealthStatus.Bad, info.Health);
    }

    [Fact]
    public void ParseAttributes_ZeroAttributeId_IsSkipped()
    {
        var blob = new byte[362]; // all zeros — attr id 0 is skipped
        var info = new DiskDriveInfo();
        DiskHealthService.ParseAttributes(info, blob, "caution", "bad");
        Assert.Null(info.PowerOnHours);
        Assert.Null(info.TemperatureCelsius);
    }
}
