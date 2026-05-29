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

    // ── DeriveMediaType (Storage namespace numeric overload) ────────────────────

    [Fact]
    public void DeriveMediaType_NvmeBusType_ReturnsNvme()
    {
        // BusType 17 = NVMe, even though MediaType reports SSD and spindle 0
        Assert.Equal("NVMe", DiskHealthService.DeriveMediaType(17, 4, 0, "Samsung SSD 980 1TB"));
    }

    [Fact]
    public void DeriveMediaType_SpindleZero_ReturnsSsd()
    {
        // Kingston SATA SSD: model lacks "SSD", but spindle speed 0 marks it solid-state
        Assert.Equal("SSD", DiskHealthService.DeriveMediaType(11, 0, 0, "KINGSTON SUV400S37240G"));
    }

    [Fact]
    public void DeriveMediaType_MediaTypeSsd_ReturnsSsd()
    {
        Assert.Equal("SSD", DiskHealthService.DeriveMediaType(11, 4, 0xFFFFFFFF, "Some Drive"));
    }

    [Fact]
    public void DeriveMediaType_SpinningDisk_ReturnsHdd()
    {
        Assert.Equal("HDD", DiskHealthService.DeriveMediaType(11, 3, 7200, "WD Blue 1TB"));
    }

    [Fact]
    public void DeriveMediaType_AllUnknown_FallsBackToStringHeuristic()
    {
        // BusType non-NVMe, MediaType 0 (Unspecified), spindle unknown → string fallback,
        // which keys off the model name (contains "SSD").
        Assert.Equal("SSD", DiskHealthService.DeriveMediaType(0, 0, 0xFFFFFFFF, "Crucial SSD"));
    }

    // ── MatchesInstance ─────────────────────────────────────────────────────────

    [Fact]
    public void MatchesInstance_PnpIdWithUnderscoreSuffix_Matches()
    {
        const string pnp = @"SCSI\DISK&VEN_SAMSUNG&PROD_SSD_860_EVO\4&abcd&0&000000";
        Assert.True(DiskHealthService.MatchesInstance(pnp + "_0", pnp));
    }

    [Fact]
    public void MatchesInstance_DifferentCase_Matches()
    {
        const string pnp = @"SCSI\DISK&VEN_SAMSUNG&PROD_SSD_860_EVO\4&abcd&0&000000";
        Assert.True(DiskHealthService.MatchesInstance(pnp.ToUpperInvariant() + "_0", pnp.ToLowerInvariant()));
    }

    [Fact]
    public void MatchesInstance_UnrelatedInstance_DoesNotMatch()
    {
        Assert.False(DiskHealthService.MatchesInstance(
            @"SCSI\DISK&VEN_KINGSTON\5&xyz&0&000000_0",
            @"SCSI\DISK&VEN_SAMSUNG&PROD_SSD_860_EVO\4&abcd&0&000000"));
    }

    [Fact]
    public void MatchesInstance_PhysicalDriveLiteral_DoesNotMatchPnpId()
    {
        // Regression guard: the old code filtered on "PHYSICALDRIVE{index}", which never
        // matches a real PnP-style InstanceName.
        Assert.False(DiskHealthService.MatchesInstance(
            "PHYSICALDRIVE0", @"SCSI\DISK&VEN_SAMSUNG&PROD_SSD_860_EVO\4&abcd&0&000000"));
    }

    [Theory]
    [InlineData("", "pnp")]
    [InlineData("instance", "")]
    public void MatchesInstance_EmptyInputs_DoNotMatch(string instanceName, string pnp)
    {
        Assert.False(DiskHealthService.MatchesInstance(instanceName, pnp));
    }

    // ── MapPhysicalDiskHealth ───────────────────────────────────────────────────

    [Theory]
    [InlineData((ushort)0, HealthStatus.Good, "good")]
    [InlineData((ushort)1, HealthStatus.Caution, "caution")]
    [InlineData((ushort)2, HealthStatus.Bad, "bad")]
    [InlineData((ushort)3, HealthStatus.Bad, "bad")]
    [InlineData((ushort)99, HealthStatus.Unknown, "unknown")]
    public void MapPhysicalDiskHealth_MapsStatusCodes(ushort code, HealthStatus expected, string expectedText)
    {
        var (status, text) = DiskHealthService.MapPhysicalDiskHealth(code, "good", "caution", "bad", "unknown");
        Assert.Equal(expected, status);
        Assert.Equal(expectedText, text);
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

    // ── ParseNvmeHealthLog ──────────────────────────────────────────────────────

    // Builds a buffer with a 512-byte NVMe SMART/Health log starting at logStart.
    // Field offsets within the log: temp(K) @1 (2 bytes), power cycles @112, power-on hours @128.
    private static byte[] MakeNvmeLog(int logStart, ushort tempKelvin, ulong powerCycles, ulong powerOnHours)
    {
        var buffer = new byte[logStart + 512];
        BitConverter.GetBytes(tempKelvin).CopyTo(buffer, logStart + 1);
        BitConverter.GetBytes(powerCycles).CopyTo(buffer, logStart + 112);
        BitConverter.GetBytes(powerOnHours).CopyTo(buffer, logStart + 128);
        return buffer;
    }

    [Fact]
    public void ParseNvmeHealthLog_PopulatesHoursCyclesAndTemperature()
    {
        var buffer = MakeNvmeLog(48, tempKelvin: 309, powerCycles: 2982, powerOnHours: 43861);
        var info = new DiskDriveInfo();
        DiskHealthService.ParseNvmeHealthLog(info, buffer, 48);
        Assert.Equal(36, info.TemperatureCelsius);   // 309 K - 273
        Assert.Equal(2982, info.PowerCycleCount);
        Assert.Equal((long)43861, info.PowerOnHours);
    }

    [Fact]
    public void ParseNvmeHealthLog_ZeroFields_LeaveValuesNull()
    {
        var buffer = MakeNvmeLog(48, tempKelvin: 0, powerCycles: 0, powerOnHours: 0);
        var info = new DiskDriveInfo();
        DiskHealthService.ParseNvmeHealthLog(info, buffer, 48);
        Assert.Null(info.TemperatureCelsius);
        Assert.Null(info.PowerCycleCount);
        Assert.Null(info.PowerOnHours);
    }

    [Fact]
    public void ParseNvmeHealthLog_BufferTooShort_LeavesInfoUnchanged()
    {
        var info = new DiskDriveInfo();
        DiskHealthService.ParseNvmeHealthLog(info, new byte[100], 48);
        Assert.Null(info.PowerOnHours);
        Assert.Null(info.PowerCycleCount);
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
