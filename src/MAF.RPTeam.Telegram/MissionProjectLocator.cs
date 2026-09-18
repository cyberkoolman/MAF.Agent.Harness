namespace MAF.RPTeam.Telegram;

internal static class MissionProjectLocator
{
    private const string RelativeProjectPath = @"src\MAF.RPTeam\MAF.RPTeam.csproj";

    public static string Resolve(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath);
            return File.Exists(fullPath)
                ? fullPath
                : throw new FileNotFoundException("The configured mission project was not found.", fullPath);
        }

        foreach (var startingPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(startingPath);
                 directory is not null;
                 directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, RelativeProjectPath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new FileNotFoundException(
            $"Could not locate {RelativeProjectPath}. Set Telegram:MissionProjectPath explicitly.");
    }
}
