using Microsoft.Windows.ApplicationModel.Resources;
using Rewind.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Rewind.Services;

public class CleanerService
{
    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private readonly string _catUserTemp, _catUserTempDesc;
    private readonly string _catWinTemp, _catWinTempDesc;
    private readonly string _catWinUpdate, _catWinUpdateDesc;
    private readonly string _catPrefetch, _catPrefetchDesc;
    private readonly string _catDeliveryOpt, _catDeliveryOptDesc;
    private readonly string _catMemDumps, _catMemDumpsDesc;
    private readonly string _catWer, _catWerDesc;
    private readonly string _catEdge, _catEdgeDesc;
    private readonly string _catChrome, _catChromeDesc;
    private readonly string _catFirefox, _catFirefoxDesc;
    private readonly string _catRecycleBin, _catRecycleBinDesc;
    private readonly string _catCustom;

    public CleanerService()
    {
        var res = new ResourceLoader();
        _catUserTemp     = res.GetString("Cleaner_CatUserTemp");
        _catUserTempDesc = res.GetString("Cleaner_CatUserTempDesc");
        _catWinTemp      = res.GetString("Cleaner_CatWinTemp");
        _catWinTempDesc  = res.GetString("Cleaner_CatWinTempDesc");
        _catWinUpdate    = res.GetString("Cleaner_CatWinUpdate");
        _catWinUpdateDesc = res.GetString("Cleaner_CatWinUpdateDesc");
        _catPrefetch     = res.GetString("Cleaner_CatPrefetch");
        _catPrefetchDesc = res.GetString("Cleaner_CatPrefetchDesc");
        _catDeliveryOpt  = res.GetString("Cleaner_CatDeliveryOpt");
        _catDeliveryOptDesc = res.GetString("Cleaner_CatDeliveryOptDesc");
        _catMemDumps     = res.GetString("Cleaner_CatMemDumps");
        _catMemDumpsDesc = res.GetString("Cleaner_CatMemDumpsDesc");
        _catWer          = res.GetString("Cleaner_CatWer");
        _catWerDesc      = res.GetString("Cleaner_CatWerDesc");
        _catEdge         = res.GetString("Cleaner_CatEdge");
        _catEdgeDesc     = res.GetString("Cleaner_CatEdgeDesc");
        _catChrome       = res.GetString("Cleaner_CatChrome");
        _catChromeDesc   = res.GetString("Cleaner_CatChromeDesc");
        _catFirefox      = res.GetString("Cleaner_CatFirefox");
        _catFirefoxDesc  = res.GetString("Cleaner_CatFirefoxDesc");
        _catRecycleBin   = res.GetString("Cleaner_CatRecycleBin");
        _catRecycleBinDesc = res.GetString("Cleaner_CatRecycleBinDesc");
        _catCustom       = res.GetString("Cleaner_CatCustom");
    }

    public List<CleanerCategory> GetCategories(List<string> customPaths)
    {
        var list = new List<CleanerCategory>
        {
            Make("user-temp",     _catUserTemp,     _catUserTempDesc,    [Expand("%TEMP%")],                                          admin: false),
            Make("windows-temp",  _catWinTemp,      _catWinTempDesc,     [@"C:\Windows\Temp"],                                        admin: true),
            Make("windows-update",_catWinUpdate,    _catWinUpdateDesc,   [@"C:\Windows\SoftwareDistribution\Download"],               admin: true),
            Make("prefetch",      _catPrefetch,     _catPrefetchDesc,    [@"C:\Windows\Prefetch"],                                    admin: true),
            Make("delivery-opt",  _catDeliveryOpt,  _catDeliveryOptDesc, [@"C:\Windows\SoftwareDistribution\DeliveryOptimization\Cache"], admin: true),
            Make("memory-dumps",  _catMemDumps,     _catMemDumpsDesc,    [@"C:\Windows\Minidump"],                                   admin: true),
            Make("wer",           _catWer,          _catWerDesc,         [
                Expand(@"%LOCALAPPDATA%\Microsoft\Windows\WER\ReportArchive"),
                Expand(@"%LOCALAPPDATA%\Microsoft\Windows\WER\ReportQueue"),
            ], admin: false),
            Make("edge-cache",    _catEdge,         _catEdgeDesc,        [Expand(@"%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Cache\Cache_Data")],   admin: false),
            Make("chrome-cache",  _catChrome,       _catChromeDesc,      [Expand(@"%LOCALAPPDATA%\Google\Chrome\User Data\Default\Cache\Cache_Data")],    admin: false),
            Make("firefox-cache", _catFirefox,      _catFirefoxDesc,     GetFirefoxCachePaths(),                                     admin: false),
            new CleanerCategory { Id = "recycle-bin", Name = _catRecycleBin, Description = _catRecycleBinDesc, Paths = [], RequiresAdmin = false },
        };

        foreach (var path in customPaths)
            list.Add(new CleanerCategory { Id = $"custom|{path}", Name = _catCustom, Description = path, Paths = [path], IsCustom = true });

        return list;
    }

    public Task<long> ScanAsync(CleanerCategory cat) =>
        Task.Run(() => cat.Id == "recycle-bin" ? ScanRecycleBin() : cat.Paths.Sum(GetDirectorySize));

    public void CleanCategory(CleanerCategory cat)
    {
        if (cat.Id == "recycle-bin") { SHEmptyRecycleBin(IntPtr.Zero, null, 0x0007); return; }
        foreach (var path in cat.Paths) CleanDirectory(path);
    }

    private static string[] GetFirefoxCachePaths()
    {
        var profilesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mozilla", "Firefox", "Profiles");
        if (!Directory.Exists(profilesDir)) return [];
        try
        {
            return Directory.GetDirectories(profilesDir)
                .Select(p => Path.Combine(p, "cache2", "entries"))
                .ToArray();
        }
        catch { return []; }
    }

    private static long ScanRecycleBin()
    {
        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (sid == null) return 0;
            var binPath = Path.Combine(@"C:\$Recycle.Bin", sid);
            return GetDirectorySize(binPath);
        }
        catch { return 0; }
    }

    private static long GetDirectorySize(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return 0;
            return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
        }
        catch { return 0; }
    }

    private static void CleanDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try { foreach (var f in Directory.GetFiles(path)) try { File.Delete(f); } catch { } } catch { }
        try { foreach (var d in Directory.GetDirectories(path)) try { Directory.Delete(d, true); } catch { } } catch { }
    }

    private static CleanerCategory Make(string id, string name, string desc, string[] paths, bool admin) =>
        new() { Id = id, Name = name, Description = desc, Paths = paths, RequiresAdmin = admin };

    private static string Expand(string path) => Environment.ExpandEnvironmentVariables(path);
}
