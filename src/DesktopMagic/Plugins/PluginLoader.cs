using DesktopMagic.BuiltInWindowElements;
using DesktopMagic.Api;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Linq;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

namespace DesktopMagic.Plugins;

internal class PluginLoader
{
    private static readonly Dictionary<PluginMetadata, Type> builtInPlugins = new()
    {
        {new("Music Visualizer", 1), typeof(MusicVisualizerPlugin)},
        {new("Time",2), typeof(TimePlugin)},
        {new("Date",3), typeof(DatePlugin)},
        {new("Cpu Usage", 4), typeof(CpuMonitorPlugin)}
    };

    private readonly Dictionary<uint, InternalPluginData> pluginRegistry = [];
    private readonly Dictionary<uint, PluginLoadContext> loadedPlugins = [];

    public IEnumerable<InternalPluginData> PluginRegistry => pluginRegistry.Values;

    public int LoadPluginRegistry()
    {
        pluginRegistry.Clear();

        foreach (PluginMetadata metadata in builtInPlugins.Keys)
        {
            pluginRegistry.Add(metadata.Id, new(metadata, string.Empty));
        }

        string pluginsPath = App.ApplicationDataPath + "\\Plugins";

        foreach (string directory in Directory.GetDirectories(pluginsPath))
        {
            string? pluginDllPath = Directory.GetFiles(directory, "main.dll").FirstOrDefault();
            string? pluginMetadataPath = Directory.GetFiles(directory, "metadata.json").FirstOrDefault();

            if (pluginDllPath is null)
            {
                App.Logger.LogError($"Plugin \"{directory}\" has no \"main.dll\"", source: "Main");
                continue;
            }

            if (pluginMetadataPath is null)
            {
                App.Logger.LogWarn($"Plugin \"{directory}\" has no \"metadata.json\"", source: "Main");
                continue;
            }

            PluginMetadata? pluginMetadata = JsonSerializer.Deserialize<PluginMetadata>(File.ReadAllText(pluginMetadataPath));

            if (pluginMetadata is null)
            {
                App.Logger.LogError($"Plugin \"{directory}\" has no valid \"metadata.json\"", source: "Main");
                continue;
            }

            if (pluginRegistry.ContainsKey(pluginMetadata.Id))
            {
                App.Logger.LogError($"Plugin \"{directory}\" has the same id as another plugin", source: "Main");
                continue;
            }

            pluginRegistry.Add(pluginMetadata.Id, new(pluginMetadata, directory));
        }

        return pluginRegistry.Count;
    }

    public Plugin LoadPlugin(uint pluginId)
    {
        if (loadedPlugins.TryGetValue(pluginId, out PluginLoadContext? existingLoadContext))
        {
            return (Plugin)Activator.CreateInstance(existingLoadContext.PluginType)!;
        }
        if (!TryGetPluginData(pluginId, out InternalPluginData? pluginData))
        {
            throw new ArgumentException($"Could not find a plugin with ID \"{pluginId}\"");
        }
        if (builtInPlugins.TryGetValue(pluginData.Metadata, out Type? builtInPluginType))
        {
            return (Plugin)Activator.CreateInstance(builtInPluginType)!;
        }

        App.Logger.LogInfo($"\"{pluginData.Metadata.Name}\" - Loading plugin", source: "Plugin");

        AssemblyLoadContext loadContext = new(pluginData.Metadata.Name, true);
        loadContext.Resolving += pluginData.LoadDependency;
        if (!Path.Exists(Path.Combine(pluginData.DirectoryPath, "main.dll")))
            throw new IOException($"\"{pluginData.Metadata.Name}\" - File \"main.dll\" does not exist");

        var fileStream = File.OpenRead(Path.Combine(pluginData.DirectoryPath, "main.dll"));
        Assembly pluginAssembly = loadContext.LoadFromStream(fileStream);
        fileStream.Dispose();

        Type pluginType = Array.Find(pluginAssembly.GetTypes(), type => type.GetTypeInfo().BaseType == typeof(Plugin))
            ?? throw new InvalidCastException($"The \"Plugin\" class could not be found! It has to inherit from \"{typeof(Plugin).FullName}\"");

        PluginLoadContext pluginLoadContext = new(loadContext, pluginData, pluginType);
        loadedPlugins.Add(pluginId, pluginLoadContext);
        return (Plugin)Activator.CreateInstance(pluginType)!;
    }

    public bool UnloadPlugin(uint pluginId)
    {
        if (!loadedPlugins.TryGetValue(pluginId, out PluginLoadContext? loadContext))
        {
            return false;
        }

        if (builtInPlugins.ContainsKey(loadContext.PluginData.Metadata))
        {
            return false;
        }

        loadedPlugins.Remove(pluginId);
        loadContext.LoadContext.Unload();
        loadContext = null;

        return true;
    }

    public bool TryGetPluginData(uint pluginId, [NotNullWhen(true)] out InternalPluginData? pluginData)
    {
        return pluginRegistry.TryGetValue(pluginId, out pluginData);
    }

    private class PluginLoadContext(AssemblyLoadContext loadContext, InternalPluginData pluginData, Type pluginType)
    {
        public AssemblyLoadContext LoadContext { get; init; } = loadContext;
        public InternalPluginData PluginData { get; init; } = pluginData;
        public Type PluginType { get; init; } = pluginType;
    }
}
