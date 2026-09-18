namespace MAF.RPTeam;

/// <summary>Configuration for the sample application the agents may inspect and repair.</summary>
public sealed class MissionSettings
{
    /// <summary>Path to the sample application the agents may touch.</summary>
    public string WorkspaceRoot { get; set; } = string.Empty;

    public string FrontendUrl { get; set; } = "http://localhost:3001";

    public string BackendUrl { get; set; } = "http://localhost:4000";

    public int AcceptanceMaxRounds { get; set; } = 3;
}

public sealed class AzureOpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;

    public string Deployment { get; set; } = "gpt-4o";
}

public sealed class AzureMonitorSettings
{
    public string ConnectionString { get; set; } = string.Empty;

    public string ServiceName { get; set; } = "MAF.RPTeam";

    public string Environment { get; set; } = "Development";
}
