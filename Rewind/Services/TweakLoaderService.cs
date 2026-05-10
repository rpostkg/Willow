using Rewind.Models;
using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Rewind.Services;

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
                        if (currentBuild >= tweak.MinVersion && currentBuild <= tweak.MaxVersion)
                        {
                            tweaks.Add(tweak);
                        }
                    }
                }
            }
            catch { }
        }

        return tweaks;
    }
}
