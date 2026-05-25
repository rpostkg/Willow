using Microsoft.UI.Xaml;
using System.Collections.Generic;

namespace Rewind.Models;

public class TweakChangeReport
{
    public List<ChangeItem> Changes { get; set; } = new();
}

public class ChangeItem
{
    public string TweakId { get; set; } = string.Empty;

    // Storage values — used for backup and revert logic; may contain sentinel strings
    public string Target { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public ActionType Type { get; set; }

    // Registry-specific backup fields
    public string Hive { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string ValueType { get; set; } = string.Empty;

    // Localized display values — set by TweakEngineService; never used for logic or storage
    public string DisplayTarget { get; set; } = string.Empty;
    public string DisplayOldValue { get; set; } = string.Empty;

    public Visibility IsRegistryOrServiceVisible => Type == ActionType.Script ? Visibility.Collapsed : Visibility.Visible;
}
