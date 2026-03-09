using CkCommons.HybridSaver;

namespace Loci;

/// <summary>
///     Helps encapsulate all the configuration file names into a single place.
/// </summary>
public class FileProvider : IConfigFileProvider
{
    // Shared Config Directories
    public static string AssemblyLocation       => Svc.PluginInterface.AssemblyLocation.FullName;
    public static string AssemblyDirectoryName  => Svc.PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty;
    public static string AssemblyDirectory      => Svc.PluginInterface.AssemblyLocation.Directory?.FullName ?? string.Empty;
    public static string Directory              => Svc.PluginInterface.ConfigDirectory.FullName;
    public static string FileSysDirectory       { get; private set; } = string.Empty;    
    // Configs
    public readonly string MainConfig;
    public readonly string DataConfig;
    public readonly string ManagersConfig;
    public readonly string Favorites;

    // Shared FileSystem Configs.
    public string DDS_Managers => Path.Combine(FileSysDirectory, "dds-managers.json");
    public string CKFS_Statuses => Path.Combine(FileSysDirectory, "fs-statuses.json");
    public string CKFS_Presets => Path.Combine(FileSysDirectory, "fs-presets.json");

    public FileProvider()
    {

        FileSysDirectory = Path.Combine(Directory, "filesystem");

        // Ensure directory existence.
        if (!System.IO.Directory.Exists(FileSysDirectory))
            System.IO.Directory.CreateDirectory(FileSysDirectory);

        // Configs.
        MainConfig = Path.Combine(Directory, "config.json");
        DataConfig = Path.Combine(Directory, "lociData.json");
        ManagersConfig = Path.Combine(Directory, "managers.json");
        Favorites = Path.Combine(Directory, "favorites.json");
    }

    // not profile specific.
    public bool HasValidProfileConfigs => true;
}
