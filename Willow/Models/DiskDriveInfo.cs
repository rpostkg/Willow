using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Willow.Models;

public enum HealthStatus { Good, Caution, Bad, Unknown }

public class DiskDriveInfo
{
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public HealthStatus Health { get; set; } = HealthStatus.Unknown;
    public string HealthText { get; set; } = string.Empty;
    public int? TemperatureCelsius { get; set; }
    public int? ReallocatedSectors { get; set; }
    public long? PowerOnHours { get; set; }
    public int? PowerCycleCount { get; set; }

    public string SizeText => SizeBytes > 0 ? $"{SizeBytes / 1_000_000_000.0:F1} GB" : "—";
    public string TempText => TemperatureCelsius.HasValue ? $"{TemperatureCelsius}°C" : string.Empty;
    public string PowerHoursText => PowerOnHours.HasValue ? $"{PowerOnHours:N0}" : "—";
    public string ReallocText => ReallocatedSectors.HasValue ? ReallocatedSectors.ToString()! : "—";
    public string PowerCyclesText => PowerCycleCount.HasValue ? PowerCycleCount.ToString()! : "—";

    public Visibility TempVisibility => TemperatureCelsius.HasValue ? Visibility.Visible : Visibility.Collapsed;

    public SolidColorBrush HealthBrush => Health switch
    {
        HealthStatus.Good    => new SolidColorBrush(Color.FromArgb(255, 22,  198, 12)),
        HealthStatus.Caution => new SolidColorBrush(Color.FromArgb(255, 202, 80,  16)),
        HealthStatus.Bad     => new SolidColorBrush(Color.FromArgb(255, 255, 67,  67)),
        _                    => new SolidColorBrush(Color.FromArgb(255, 136, 136, 136)),
    };
}
