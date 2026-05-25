using System.Collections.Generic;

namespace Rewind.Models;

public class UserPreferences
{
    public bool DisableBackups { get; set; } = false;
    public bool InformedOfBackups { get; set; } = false;
    public string Language { get; set; } = "uk-UA";
    public bool ResolveShortcuts { get; set; } = false;
    public List<string> CustomCleanerPaths { get; set; } = new();
    public Dictionary<string, List<BackedUpState>> OldRegistryData { get; set; } = new();
}

public class BackedUpState
{
    public ActionType Type { get; set; }
    public string Target { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    
    // For registry, we need specifics to reconstruct it dynamically
    public string Hive { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
}
