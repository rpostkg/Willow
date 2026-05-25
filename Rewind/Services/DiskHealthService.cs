using Microsoft.Windows.ApplicationModel.Resources;
using Rewind.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace Rewind.Services;

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
        var results = new List<(uint index, DiskDriveInfo info)>();

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

                results.Add((index, new DiskDriveInfo
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

        foreach (var (index, info) in results)
            TryReadSmart(index, info);

        var drives = new List<DiskDriveInfo>(results.Count);
        foreach (var (_, info) in results) drives.Add(info);
        return drives;
    }

    private void TryReadSmart(uint index, DiskDriveInfo info)
    {
        try
        {
            string filter = $"InstanceName LIKE 'PHYSICALDRIVE{index}%'";

            using var statusSearcher = new ManagementObjectSearcher(@"root\WMI",
                $"SELECT * FROM MSStorageDriver_FailurePredictStatus WHERE {filter}");
            foreach (ManagementObject status in statusSearcher.Get())
            {
                bool fail = (bool)(status["PredictFailure"] ?? false);
                info.Health     = fail ? HealthStatus.Bad : HealthStatus.Good;
                info.HealthText = fail ? _healthBad : _healthGood;
                break;
            }

            using var dataSearcher = new ManagementObjectSearcher(@"root\WMI",
                $"SELECT * FROM MSStorageDriver_FailurePredictData WHERE {filter}");
            foreach (ManagementObject data in dataSearcher.Get())
            {
                if (data["VendorSpecific"] is byte[] vs)
                    ParseAttributes(info, vs, _healthCaution, _healthBad);
                break;
            }
        }
        catch
        {
            // SMART unavailable (NVMe, insufficient privileges) — leave as Unknown
        }
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

    internal static string DeriveMediaType(string wmiMediaType, string model)
    {
        if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase))     return "NVMe";
        if (model.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
            model.Contains("Solid State", StringComparison.OrdinalIgnoreCase)) return "SSD";
        if (wmiMediaType.Contains("Fixed", StringComparison.OrdinalIgnoreCase) &&
            !wmiMediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase)) return "HDD";
        return "SSD";
    }
}
