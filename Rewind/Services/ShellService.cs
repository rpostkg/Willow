using System;
using System.Diagnostics;
using System.IO;

namespace Rewind.Services;

public static class ShellService
{
    public static bool RunInlinePowerShell(string script)
    {
        if (string.IsNullOrWhiteSpace(script)) return true;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"{script.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // Alternatively, for larger inline scripts, write to a temp file and execute to avoid command-line length limits.
            // Since some YAML scripts might be multi-line, writing to temp file is safer.
            string tempScript = Path.Combine(Path.GetTempPath(), $"Rewind_Inline_{Guid.NewGuid():N}.ps1");
            File.WriteAllText(tempScript, script);
            
            psi.Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScript}\"";

            using var process = Process.Start(psi);
            if (process == null) return false;

            // Optional timeout
            bool exited = process.WaitForExit(60000); // 1 minute timeout

            if (!exited)
            {
                process.Kill();
                Debug.WriteLine("[ShellService] Process timed out and was killed.");
                return false;
            }

            string stderr = process.StandardError.ReadToEnd();
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                Debug.WriteLine($"[ShellService] Error Output: {stderr}");
            }

            try
            {
                File.Delete(tempScript);
            }
            catch { }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ShellService] Failed to run PowerShell script: {ex.Message}");
            return false;
        }
    }
}
