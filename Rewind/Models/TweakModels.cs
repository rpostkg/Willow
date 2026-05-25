using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using YamlDotNet.Serialization;

namespace Rewind.Models;

public class TweakFile
{
    public List<Tweak> Tweaks { get; set; } = new();
}

public partial class Tweak : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [YamlMember(Alias = "minversion")]
    public int MinVersion { get; set; } = 0;
    [YamlMember(Alias = "maxversion")]
    public int MaxVersion { get; set; } = int.MaxValue;

    private bool _isEnabled = false;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppliedBadgeVisibility))]
    private bool isApplied;

    public Visibility AppliedBadgeVisibility => IsApplied ? Visibility.Visible : Visibility.Collapsed;

    public List<TweakAction> Actions { get; set; } = new();
    public List<TweakAction> RevertActions { get; set; } = new();
}

public enum ActionType
{
    Registry,
    Service,
    Script
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
    
    // Script specific
    public string Script { get; set; } = string.Empty;
}
