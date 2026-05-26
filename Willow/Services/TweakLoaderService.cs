using Willow.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Willow.Services;

public class TweakLoaderService
{
    public List<Tweak> LoadTweaks()
    {
        var tweaks = new List<Tweak>();
        var tweaksFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tweaks");

        if (!Directory.Exists(tweaksFolder))
            return tweaks;

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        int currentBuild = SystemVersionService.GetCurrentBuildNumber();

        foreach (var file in Directory.GetFiles(tweaksFolder, "*.yaml"))
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var tweakFile = deserializer.Deserialize<TweakFile>(yaml);

                if (tweakFile?.Tweaks != null)
                {
                    foreach (var tweak in tweakFile.Tweaks)
                    {
                        if (PassesVersionFilter(tweak, currentBuild))
                            tweaks.Add(tweak);
                    }
                }
            }
            catch { }
        }

        var locale = new PreferencesService().LoadPreferences().Language;
        if (!string.IsNullOrEmpty(locale))
            ApplyLocale(tweaks, tweaksFolder, locale);

        return tweaks;
    }

    public List<Tweak> LoadTweaksFromFile(string fileName)
    {
        var tweaks = new List<Tweak>();
        var tweaksFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tweaks");
        var filePath = Path.Combine(tweaksFolder, fileName);

        if (!File.Exists(filePath))
            return tweaks;

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        int currentBuild = SystemVersionService.GetCurrentBuildNumber();

        try
        {
            var yaml = File.ReadAllText(filePath);
            var tweakFile = deserializer.Deserialize<TweakFile>(yaml);
            if (tweakFile?.Tweaks != null)
                foreach (var tweak in tweakFile.Tweaks)
                    if (PassesVersionFilter(tweak, currentBuild))
                        tweaks.Add(tweak);
        }
        catch { }

        var locale = new PreferencesService().LoadPreferences().Language;
        if (!string.IsNullOrEmpty(locale))
            ApplyLocale(tweaks, tweaksFolder, locale);

        return tweaks;
    }

    internal static bool PassesVersionFilter(Tweak tweak, int buildNumber) =>
        buildNumber >= tweak.MinVersion && buildNumber <= tweak.MaxVersion;

    internal void ApplyLocale(List<Tweak> tweaks, string tweaksFolder, string locale)
    {
        var localeFolder = Path.Combine(tweaksFolder, "Locale", locale);
        if (!Directory.Exists(localeFolder)) return;

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        foreach (var file in Directory.GetFiles(localeFolder, "*.yaml"))
        {
            try
            {
                var localeFile = deserializer.Deserialize<TweakLocaleFile>(File.ReadAllText(file));
                ApplyLocaleEntries(tweaks, localeFile?.Tweaks ?? []);
            }
            catch { }
        }
    }

    internal static void ApplyLocaleEntries(List<Tweak> tweaks, IEnumerable<TweakLocaleEntry> entries)
    {
        var byId = tweaks.ToDictionary(t => t.Id);
        foreach (var entry in entries)
        {
            if (!byId.TryGetValue(entry.Id, out var tweak)) continue;
            if (entry.Name is not null) tweak.Name = entry.Name;
            if (entry.Category is not null) tweak.Category = entry.Category;
            if (entry.Description is not null) tweak.Description = entry.Description;
        }
    }

    internal class TweakLocaleEntry
    {
        public string Id { get; set; } = "";
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
    }

    private class TweakLocaleFile
    {
        public List<TweakLocaleEntry> Tweaks { get; set; } = [];
    }
}
