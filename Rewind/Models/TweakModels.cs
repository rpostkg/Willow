using System.Collections.Generic;

namespace Rewind.Models;

public class TweakFile
{
    public List<Tweak> Tweaks { get; set; } = new();
}

public class Tweak
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MinVersion { get; set; } = 0;
    public int MaxVersion { get; set; } = int.MaxValue;
    public bool IsEnabled { get; set; } = false;
    public List<TweakAction> Actions { get; set; } = new();
    public List<TweakAction> RevertActions { get; set; } = new();
}

public enum ActionType
{
    Registry,
    Service
}

public class TweakAction
{
    public ActionType Type { get; set; }
    
    // Registry specific
    public string Hive { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;
    
    // Service specific
    public string Name { get; set; } = string.Empty;
    public string TargetState { get; set; } = string.Empty;
}
