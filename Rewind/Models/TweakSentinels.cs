namespace Rewind.Models;

// Internal sentinel strings stored in userpreferences.yaml backup data.
// These must NEVER be localized — they are storage keys, not display strings.
// All display-facing code should map these through TweakEngineService's localized lookup.
internal static class TweakSentinels
{
    public const string NewKey        = "NEW_KEY";
    public const string NotFound      = "NOT_FOUND";
    public const string UnknownError  = "UNKNOWN_ERROR";
    public const string ServicePrefix = "Service: ";
}
