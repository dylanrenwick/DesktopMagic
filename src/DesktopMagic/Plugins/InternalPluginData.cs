using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace DesktopMagic.Plugins;

internal class InternalPluginData(PluginMetadata pluginMetadata, string directoryPath)
{
    public PluginMetadata Metadata { get; set; } = pluginMetadata;
    public string DirectoryPath { get; set; } = directoryPath;

    public Assembly? LoadDependency(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        string assemblyPath = Path.Combine(DirectoryPath, assemblyName.Name = ".dll");
        if (!File.Exists(assemblyPath))
        {
            return null;
        }
        return context.LoadFromAssemblyPath(assemblyPath);
    }
}