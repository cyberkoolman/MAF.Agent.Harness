namespace MAF.RPTeam.Phases.Phase2.Browser;

public sealed class BrowserSettings
{
    public bool Enabled { get; set; }

    public bool Headless { get; set; } = true;

    public int NavigationTimeoutSeconds { get; set; } = 15;

    public int ActionTimeoutSeconds { get; set; } = 10;

    public int LaunchTimeoutSeconds { get; set; } = 30;

    public int ViewportWidth { get; set; } = 1440;

    public int ViewportHeight { get; set; } = 900;

    public int ConsoleCapacity { get; set; } = 200;

    public int MaxActionableElements { get; set; } = 40;

    public int MaxSnapshotCharacters { get; set; } = 12000;

    public void Validate()
    {
        if (NavigationTimeoutSeconds is < 1 or > 120)
        {
            throw new InvalidOperationException(
                "Browser:NavigationTimeoutSeconds must be between 1 and 120.");
        }

        if (ActionTimeoutSeconds is < 1 or > 120)
        {
            throw new InvalidOperationException(
                "Browser:ActionTimeoutSeconds must be between 1 and 120.");
        }

        if (LaunchTimeoutSeconds is < 5 or > 120)
        {
            throw new InvalidOperationException(
                "Browser:LaunchTimeoutSeconds must be between 5 and 120.");
        }

        if (ViewportWidth is < 640 or > 3840 || ViewportHeight is < 480 or > 2160)
        {
            throw new InvalidOperationException(
                "Browser viewport must be between 640x480 and 3840x2160.");
        }

        if (ConsoleCapacity is < 10 or > 1000)
        {
            throw new InvalidOperationException(
                "Browser:ConsoleCapacity must be between 10 and 1000.");
        }

        if (MaxActionableElements is < 1 or > 200)
        {
            throw new InvalidOperationException(
                "Browser:MaxActionableElements must be between 1 and 200.");
        }

        if (MaxSnapshotCharacters is < 1000 or > 50000)
        {
            throw new InvalidOperationException(
                "Browser:MaxSnapshotCharacters must be between 1000 and 50000.");
        }
    }
}
