namespace toms.App;

public sealed record AppPaths(string RootDirectory, string ConnectionsFilePath)
{
    public static AppPaths Create()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "toms");

        var sharedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UserbusToolsForSky");

        return new AppPaths(
            root,
            Path.Combine(sharedRoot, "connections.hjson"));
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(ConnectionsFilePath)!);
    }
}
