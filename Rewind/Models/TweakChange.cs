using System.Collections.Generic;

namespace Rewind.Models;

public class TweakChangeReport
{
    public List<ChangeItem> Changes { get; set; } = new();
}

public class ChangeItem
{
    public string Target { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public ActionType Type { get; set; }
}
