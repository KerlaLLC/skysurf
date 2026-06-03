namespace skysurf.App;

public sealed record AppPaths(string RootDirectory, string ConnectionsFilePath, string SavedQueriesFilePath, string CacheDirectory, string ResultsCacheDirectory)
{
    public static AppPaths Create()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "skysurf");

        var sharedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UserbusToolsForSky");

        return new AppPaths(
            root,
            Path.Combine(sharedRoot, "connections.hjson"),
            Path.Combine(root, "saved-queries.hjson"),
            Path.Combine(root, "cache"),
            Path.Combine(root, "cache", "results"));
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(ResultsCacheDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(ConnectionsFilePath)!);
    }
}
