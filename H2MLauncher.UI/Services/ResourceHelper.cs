using System.Collections;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;

namespace H2MLauncher.UI.Services;

public static class ResourceHelper
{
    /// <summary>
    /// Gets a list of resource names under a specific folder (case-insensitive).
    /// </summary>
    /// <param name="folder">The folder path within the resources (e.g., "Images" or "Data/TextFiles").</param>
    /// <returns>An array of resource names (relative to the folder).</returns>
    public static List<string> GetResourcesUnder(string folder)
    {
        folder = folder.ToLower().TrimEnd('/') + "/"; // Ensure consistent folder format
        Assembly assembly = Assembly.GetCallingAssembly(); // Or Assembly.GetEntryAssembly() for the main executable
        string resourcesName = assembly.GetName().Name + ".g.resources";

        using Stream? stream = assembly.GetManifestResourceStream(resourcesName);
        if (stream is null)
        {
            // This can happen if there are no 'Resource' files in the assembly,
            // or if the assembly name is different from what's expected.
            return [];
        }

        using ResourceReader resourceReader = new(stream);
        List<string> resourceNames = [];
        foreach (DictionaryEntry entry in resourceReader)
        {
            string? resourceKey = entry.Key.ToString()?.ToLower();
            if (resourceKey is not null && resourceKey.StartsWith(folder))
            {
                resourceNames.Add(resourceKey.Substring(folder.Length));
            }
        }
        return resourceNames;
    }

    /// <summary>
    /// Gets a Stream for a specific WPF resource file using a Pack URI.
    /// </summary>
    /// <param name="relativePath">The relative path to the resource within your project (e.g., "Images/myImage.png").</param>
    /// <returns>A Stream for the resource, or null if not found.</returns>
    public static Stream? GetWpfResourceStream(string relativePath)
    {
        try
        {
            // Construct a Pack URI. The "///" indicates the current assembly.
            // Adjust if the resource is in a different assembly.
            Uri uri = new Uri($"pack://application:,,,/{relativePath.Replace('\\', '/')}", UriKind.Absolute);
            return Application.GetResourceStream(uri).Stream;
        }
        catch
        {
            return null;
        }
    }
}
