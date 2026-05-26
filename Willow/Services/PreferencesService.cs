using Willow.Models;
using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Willow.Services;

public class PreferencesService
{
    private readonly string _prefsFilePath;
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public PreferencesService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "Willow");
        Directory.CreateDirectory(appFolder);
        _prefsFilePath = Path.Combine(appFolder, "userpreferences.yaml");

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        if (!File.Exists(_prefsFilePath))
        {
            SavePreferences(new UserPreferences());
        }
    }

    public UserPreferences LoadPreferences()
    {
        if (!File.Exists(_prefsFilePath))
            return new UserPreferences();

        try
        {
            var yaml = File.ReadAllText(_prefsFilePath);
            var prefs = _deserializer.Deserialize<UserPreferences>(yaml);
            return prefs ?? new UserPreferences();
        }
        catch
        {
            return new UserPreferences();
        }
    }

    public void SavePreferences(UserPreferences prefs)
    {
        try
        {
            var yaml = _serializer.Serialize(prefs);
            File.WriteAllText(_prefsFilePath, yaml);
        }
        catch { }
    }
}
