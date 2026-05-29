using Microsoft.Win32.SafeHandles;
using Microsoft.Windows.ApplicationModel.Resources;
using Willow.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;

namespace Willow.Services;

public class DiskHealthService
{
    private readonly string _healthGood;
    private readonly string _healthCaution;
    private readonly string _healthBad;
    private readonly string _healthUnknown;

    public DiskHealthService()
    {
        var res = new ResourceLoader();
        _healthGood    = res.GetString("DiskHealthPage_HealthGood");
        _healthCaution = res.GetString("DiskHealthPage_HealthCaution");
        _healthBad     = res.GetString("DiskHealthPage_HealthBad");
        _healthUnknown = res.GetString("DiskHealthPage_HealthUnknown");
    }

    public List<DiskDriveInfo> GetDrives()
    {
        var results = new List<(uint index, string pnp, DiskDriveInfo info)>();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            foreach (ManagementObject disk in searcher.Get())
            {
                uint index  = Convert.ToUInt32(disk["Index"]);
                long size   = Convert.ToInt64(disk["Size"] ?? 0L);
                string model       = disk["Model"]?.ToString() ?? "Unknown";
                string serial      = disk["SerialNumber"]?.ToString()?.Trim() ?? string.Empty;
                string mediaTypeWmi = disk["MediaType"]?.ToString() ?? string.Empty;
                string pnp         = disk["PNPDeviceID"]?.ToString() ?? string.Empty;

                results.Add((index, pnp, new DiskDriveInfo
                {
                    Model        = model,
                    SerialNumber = serial,
                    SizeBytes    = size,
                    MediaType    = DeriveMediaType(mediaTypeWmi, model),
                    Health       = HealthStatus.Unknown,
                    HealthText   = _healthUnknown,
                }));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DiskHealthService] Win32_DiskDrive query failed: {ex.Message}");
        }

        // Baseline enrichment (works for SATA and NVMe): media type, health status, and the
        // reliability counters (power-on hours, temperature, cycles).
        foreach (var (index, _, info) in results)
            TryEnrichFromStorageNamespace(index, info);

        // Precise SATA/ATA overlay: exact SMART attributes and authoritative failure prediction.
        foreach (var (_, pnp, info) in results)
            TryReadSmart(pnp, info);

        // NVMe power-on-hours / power-cycles aren't exposed through WMI, so read the NVMe
        // SMART/Health log (page 0x02) directly from the device.
        foreach (var (index, _, info) in results)
            if (info.MediaType == "NVMe")
                TryReadNvmeLog(index, info);

        var drives = new List<DiskDriveInfo>(results.Count);
        foreach (var (_, _, info) in results) drives.Add(info);
        return drives;
    }

    // Reads MSFT_PhysicalDisk (matched by disk number) and its associated
    // MSFT_StorageReliabilityCounter from the Storage WMI namespace. Covers NVMe, which the
    // legacy MSStorageDriver_* classes don't expose.
    private void TryEnrichFromStorageNamespace(uint index, DiskDriveInfo info)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage",
                $"SELECT * FROM MSFT_PhysicalDisk WHERE DeviceId = '{index.ToString(CultureInfo.InvariantCulture)}'");
            foreach (ManagementObject phys in searcher.Get())
            {
                ushort busType     = ToUShort(phys["BusType"]);
                ushort mediaTypeId = ToUShort(phys["MediaType"]);
                uint spindleSpeed  = ToUInt(phys["SpindleSpeed"]);
                info.MediaType = DeriveMediaType(busType, mediaTypeId, spindleSpeed, info.Model);

                ushort healthStatus = ToUShort(phys["HealthStatus"]);
                (info.Health, info.HealthText) =
                    MapPhysicalDiskHealth(healthStatus, _healthGood, _healthCaution, _healthBad, _healthUnknown);

                using var counters = phys.GetRelated("MSFT_StorageReliabilityCounter");
                foreach (ManagementObject rc in counters)
                {
                    if (rc["PowerOnHours"] is object poh)
                        info.PowerOnHours = Convert.ToInt64(poh);
                    if (rc["Temperature"] is object temp)
                    {
                        int t = Convert.ToInt32(temp);
                        if (t > 0) info.TemperatureCelsius = t;
                    }
                    if (rc["StartStopCycleCount"] is object ssc)
                    {
                        int c = Convert.ToInt32(ssc);
                        if (c > 0) info.PowerCycleCount = c;
                    }
                    break;
                }
                break;
            }
        }
        catch (Exception ex)
        {
            // Storage namespace unavailable (e.g. virtual disk in a VM) — leave defaults.
            Debug.WriteLine($"[DiskHealthService] Storage namespace query failed for disk {index}: {ex.Message}");
        }
    }

    // SATA/ATA SMART via root\WMI. Correlates by the drive's PnP device id rather than a
    // PHYSICALDRIVE index, because the MSStorageDriver_* InstanceName is the PnP path with a
    // "_0" suffix — never "PHYSICALDRIVE{index}".
    private void TryReadSmart(string pnpDeviceId, DiskDriveInfo info)
    {
        if (string.IsNullOrEmpty(pnpDeviceId)) return;

        try
        {
            using var statusSearcher = new ManagementObjectSearcher(@"root\WMI",
                "SELECT * FROM MSStorageDriver_FailurePredictStatus");
            foreach (ManagementObject status in statusSearcher.Get())
            {
                if (!MatchesInstance(status["InstanceName"]?.ToString() ?? string.Empty, pnpDeviceId))
                    continue;
                bool fail = (bool)(status["PredictFailure"] ?? false);
                info.Health     = fail ? HealthStatus.Bad : HealthStatus.Good;
                info.HealthText = fail ? _healthBad : _healthGood;
                break;
            }

            using var dataSearcher = new ManagementObjectSearcher(@"root\WMI",
                "SELECT * FROM MSStorageDriver_FailurePredictData");
            foreach (ManagementObject data in dataSearcher.Get())
            {
                if (!MatchesInstance(data["InstanceName"]?.ToString() ?? string.Empty, pnpDeviceId))
                    continue;
                if (data["VendorSpecific"] is byte[] vs)
                    ParseAttributes(info, vs, _healthCaution, _healthBad);
                break;
            }
        }
        catch
        {
            // SMART unavailable (NVMe, insufficient privileges) — keep whatever enrichment found.
        }
    }

    // True when an MSStorageDriver InstanceName belongs to the given Win32_DiskDrive PNPDeviceID.
    // The InstanceName is the PnP id with a trailing "_0", so StartsWith is the correct test.
    internal static bool MatchesInstance(string instanceName, string pnpDeviceId)
        => !string.IsNullOrEmpty(instanceName)
           && !string.IsNullOrEmpty(pnpDeviceId)
           && instanceName.StartsWith(pnpDeviceId, StringComparison.OrdinalIgnoreCase);

    internal static (HealthStatus, string) MapPhysicalDiskHealth(
        ushort healthStatus, string good, string caution, string bad, string unknown)
        => healthStatus switch
        {
            0      => (HealthStatus.Good, good),
            1      => (HealthStatus.Caution, caution),
            2 or 3 => (HealthStatus.Bad, bad),
            _      => (HealthStatus.Unknown, unknown),
        };

    // Reads the NVMe SMART / Health Information log (log page 0x02) via IOCTL_STORAGE_QUERY_PROPERTY.
    // This is the only source for NVMe power-on hours and power cycles — Windows' WMI reliability
    // counter leaves them empty for NVMe.
    private static void TryReadNvmeLog(uint index, DiskDriveInfo info)
    {
        SafeFileHandle? handle = null;
        try
        {
            handle = CreateFile($@"\\.\PhysicalDrive{index}",
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle.IsInvalid) return;

            // The STORAGE_PROTOCOL_SPECIFIC_DATA sits at offset 8 in both the input
            // (STORAGE_PROPERTY_QUERY) and the output (STORAGE_PROTOCOL_DATA_DESCRIPTOR) buffers,
            // so a single buffer serves both.
            const int specificDataOffset = 8;
            const int specificDataSize   = 40;   // sizeof(STORAGE_PROTOCOL_SPECIFIC_DATA)
            const int nvmeLogSize        = 512;  // NVMe SMART/Health log
            int bufferSize = specificDataOffset + specificDataSize + nvmeLogSize;
            var buffer = new byte[bufferSize];

            // STORAGE_PROPERTY_QUERY
            PutInt(buffer, 0, StorageDeviceProtocolSpecificProperty);
            PutInt(buffer, 4, PropertyStandardQuery);
            // STORAGE_PROTOCOL_SPECIFIC_DATA
            PutInt(buffer, specificDataOffset + 0,  ProtocolTypeNvme);
            PutInt(buffer, specificDataOffset + 4,  NVMeDataTypeLogPage);
            PutInt(buffer, specificDataOffset + 8,  NVMeLogPageHealthInfo);
            PutInt(buffer, specificDataOffset + 16, specificDataSize);   // ProtocolDataOffset
            PutInt(buffer, specificDataOffset + 20, nvmeLogSize);        // ProtocolDataLength

            bool ok = DeviceIoControl(handle, IOCTL_STORAGE_QUERY_PROPERTY,
                buffer, bufferSize, buffer, bufferSize, out uint bytesReturned, IntPtr.Zero);
            if (!ok || bytesReturned == 0) return;

            int dataOffset = BitConverter.ToInt32(buffer, specificDataOffset + 16);
            int logStart = specificDataOffset + (dataOffset > 0 ? dataOffset : specificDataSize);
            ParseNvmeHealthLog(info, buffer, logStart);
        }
        catch
        {
            // Native NVMe read unavailable — keep whatever WMI provided.
        }
        finally
        {
            handle?.Dispose();
        }
    }

    // Parses power-on hours, power cycles and composite temperature out of a 512-byte NVMe
    // SMART/Health log that begins at logStart within buffer. Pure for unit testing.
    internal static void ParseNvmeHealthLog(DiskDriveInfo info, byte[] buffer, int logStart)
    {
        if (logStart < 0 || logStart + 136 > buffer.Length) return;

        ushort tempKelvin = BitConverter.ToUInt16(buffer, logStart + 1);   // Composite Temperature (Kelvin)
        if (tempKelvin > 0) info.TemperatureCelsius = tempKelvin - 273;

        ulong powerCycles = BitConverter.ToUInt64(buffer, logStart + 112); // Power Cycles (low 64 bits)
        if (powerCycles > 0) info.PowerCycleCount = (int)powerCycles;

        ulong powerOnHours = BitConverter.ToUInt64(buffer, logStart + 128); // Power On Hours (low 64 bits)
        if (powerOnHours > 0) info.PowerOnHours = (long)powerOnHours;
    }

    internal static void ParseAttributes(DiskDriveInfo info, byte[] vs, string cautionText, string badText)
    {
        if (vs.Length < 362) return;

        for (int i = 0; i < 30; i++)
        {
            int offset = 2 + i * 12;
            if (offset + 12 > vs.Length) break;

            byte id = vs[offset];
            if (id == 0) continue;

            uint raw32 = BitConverter.ToUInt32(vs, offset + 5);

            switch (id)
            {
                case 5:
                    info.ReallocatedSectors = (int)raw32;
                    if (info.ReallocatedSectors > 0 && info.Health == HealthStatus.Good)
                    {
                        info.Health     = HealthStatus.Caution;
                        info.HealthText = cautionText;
                    }
                    break;
                case 9:
                    info.PowerOnHours = raw32;
                    break;
                case 12:
                    info.PowerCycleCount = (int)raw32;
                    break;
                case 194:
                    info.TemperatureCelsius = vs[offset + 5];
                    break;
            }
        }
    }

    // Storage-namespace-aware media-type detection. Falls back to the string heuristic when the
    // numeric hints are inconclusive (e.g. Storage namespace unavailable).
    internal static string DeriveMediaType(ushort busType, ushort mediaType, uint spindleSpeed, string model)
    {
        if (busType == 17) return "NVMe";                                  // BusType 17 = NVMe
        if (mediaType == 4 || spindleSpeed == 0) return "SSD";             // MediaType 4 = SSD, spindle 0 = solid-state
        if (mediaType == 3 || (spindleSpeed > 0 && spindleSpeed != uint.MaxValue)) return "HDD"; // MediaType 3 = HDD
        return DeriveMediaType(string.Empty, model);
    }

    internal static string DeriveMediaType(string wmiMediaType, string model)
    {
        if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase))     return "NVMe";
        if (model.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
            model.Contains("Solid State", StringComparison.OrdinalIgnoreCase)) return "SSD";
        if (wmiMediaType.Contains("Fixed", StringComparison.OrdinalIgnoreCase) &&
            !wmiMediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase)) return "HDD";
        return "SSD";
    }

    private static ushort ToUShort(object? o) => o == null ? (ushort)0 : Convert.ToUInt16(o);
    private static uint ToUInt(object? o) => o == null ? 0u : Convert.ToUInt32(o);

    private static void PutInt(byte[] buffer, int offset, int value)
        => BitConverter.GetBytes(value).CopyTo(buffer, offset);

    // ── Native NVMe SMART/Health log interop ────────────────────────────────────

    private const uint GENERIC_READ  = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ  = 0x1;
    private const uint FILE_SHARE_WRITE = 0x2;
    private const uint OPEN_EXISTING = 3;
    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;

    private const int StorageDeviceProtocolSpecificProperty = 50; // STORAGE_PROPERTY_ID
    private const int PropertyStandardQuery = 0;                   // STORAGE_QUERY_TYPE
    private const int ProtocolTypeNvme = 3;                        // STORAGE_PROTOCOL_TYPE
    private const int NVMeDataTypeLogPage = 2;                     // STORAGE_PROTOCOL_NVME_DATA_TYPE
    private const int NVMeLogPageHealthInfo = 2;                   // SMART/Health Information log id

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess,
        uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode,
        byte[] lpInBuffer, int nInBufferSize, byte[] lpOutBuffer, int nOutBufferSize,
        out uint lpBytesReturned, IntPtr lpOverlapped);
}
