using System.Collections.Generic;

namespace Rewind.Models;

public class UserPreferences
{
    public bool DisableBackupWarnings { get; set; } = false;
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
