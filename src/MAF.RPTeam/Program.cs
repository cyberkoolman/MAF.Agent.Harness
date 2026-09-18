using Azure.AI.OpenAI;
using Azure.Identity;
using System.Diagnostics;
using MAF.RPTeam;
using MAF.RPTeam.Agents;
using MAF.RPTeam.Observability;
using MAF.RPTeam.Orchestration;
using MAF.RPTeam.Phases.Phase1;
using MAF.RPTeam.Phases.Phase2.Browser;
using MAF.RPTeam.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

// ---------------------------------------------------------------------------
// MAF Agent Harness - Mission Control on Microsoft Agent Framework
//
// Phase 1 remains available through Phases.Phase1.NullBrowserTools.
// Phase 2 browser classes live under Phases.Phase2.Browser.
//
// The MAF-specific parts of this file are marked [MAF]. There are only three:
// building an IChatClient, wrapping it as agents, and running one. Everything
// the agent then does between model calls comes from the harness pipeline.
// See docs/Code-Repair-Case-Study.md.
// ---------------------------------------------------------------------------

// Four sources, lowest precedence first:
//   appsettings.Local.json   gitignored, copied to the output dir by the csproj
//   user secrets             outside the repo entirely: dotnet user-secrets set ...
//   RPTEAM_ env vars         e.g. RPTEAM_AzureOpenAI__Endpoint (double underscore = nesting)
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables("RPTEAM_")
    .Build();

var aoai = config.GetSection("AzureOpenAI").Get<AzureOpenAISettings>() ?? new AzureOpenAISettings();
var mission = config.GetSection("Mission").Get<MissionSettings>() ?? new MissionSettings();
var browserSettings = config.GetSection("Browser").Get<BrowserSettings>() ?? new BrowserSettings();
MissionTelemetry telemetry;
try
{
    telemetry = MissionTelemetry.Create(config);
}
catch (Exception ex)
{
    Console.Error.WriteLine("Azure Monitor telemetry configuration is invalid.");
    Console.Error.WriteLine($"  detail : {ex.Message}");
    return 1;
}

using var telemetryLifetime = telemetry;

if (args.Length == 1 &&
    string.Equals(args[0], "--telemetry-smoke", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var traceId = telemetry.RunSmoke();
        Console.WriteLine("Azure Monitor telemetry smoke: sent");
        Console.WriteLine($"  service  : {telemetry.ServiceName}");
        Console.WriteLine($"  trace id : {traceId}");
        Console.WriteLine("  query    : search for name == 'telemetry.smoke'");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Azure Monitor telemetry smoke: failed");
        Console.Error.WriteLine($"  detail : {ex.Message}");
        return 1;
    }
}

// --- Preflight -------------------------------------------------------------
if (!Directory.Exists(mission.WorkspaceRoot))
{
    Console.Error.WriteLine($"Mission:WorkspaceRoot does not exist: {mission.WorkspaceRoot}");
    Console.Error.WriteLine("Run from the repository root or set Mission:WorkspaceRoot explicitly.");
    return 1;
}

var forceBrowser =
    args.Length == 1 &&
    (string.Equals(args[0], "--browser-smoke", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(args[0], "--browser-vision-smoke", StringComparison.OrdinalIgnoreCase));
IBrowserRuntime browserRuntime;
try
{
    browserRuntime = browserSettings.Enabled || forceBrowser
        ? new Phase2BrowserRuntime(
            browserSettings,
            mission.FrontendUrl,
            mission.BackendUrl)
        : new Phase1BrowserRuntime();
}
catch (Exception ex)
{
    Console.Error.WriteLine("Browser configuration is invalid.");
    Console.Error.WriteLine($"  detail : {ex.Message}");
    return 1;
}

await using var browserRuntimeLifetime = browserRuntime;

if (args.Length == 1 &&
    string.Equals(args[0], "--browser-smoke", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var report = await BrowserContractSmoke.RunAsync(
            (Phase2BrowserRuntime)browserRuntime);

        Console.WriteLine("Phase 2 browser smoke: ready");
        Console.WriteLine($"  detail     : {report}");
        Console.WriteLine("  multimodal : run --browser-vision-smoke for the live model proof");
        return 0;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.Error.WriteLine("Phase 2 browser smoke: unavailable");
        Console.Error.WriteLine($"  detail : {ex.Message}");
        Console.Error.WriteLine("  install: pwsh .\\src\\MAF.RPTeam\\bin\\Debug\\net8.0\\playwright.ps1 install chromium");
        return 1;
    }
}

if (args.Length == 1 && string.Equals(args[0], "--verify-only", StringComparison.OrdinalIgnoreCase))
{
    using var standaloneVerifier = new MissionAcceptanceVerifier(
        mission,
        browserRuntime.Acceptance);
    var result = await standaloneVerifier.VerifyAsync(requireReadmeChange: false);

    Console.WriteLine("Mission acceptance verification");
    Console.WriteLine(result.ToReport());
    return result.Passed ? 0 : 1;
}

if (string.IsNullOrWhiteSpace(aoai.Endpoint) || aoai.Endpoint.Contains("<your-resource>"))
{
    var localInOutput = Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json");

    Console.Error.WriteLine("AzureOpenAI:Endpoint is not set.");
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Config is read from: {AppContext.BaseDirectory}");
    Console.Error.WriteLine($"  appsettings.json            {(File.Exists(Path.Combine(AppContext.BaseDirectory, "appsettings.json")) ? "found" : "MISSING")}");
    Console.Error.WriteLine($"  appsettings.Local.json      {(File.Exists(localInOutput) ? "found" : "not present")}");
    Console.Error.WriteLine($"  user secrets                id 'maf-rpteam-mission-control'");
    Console.Error.WriteLine($"  RPTEAM_AzureOpenAI__Endpoint {(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RPTEAM_AzureOpenAI__Endpoint")) ? "not set" : "set")}");
    Console.Error.WriteLine();

    if (!File.Exists(localInOutput))
    {
        Console.Error.WriteLine("Note: appsettings.Local.json must live NEXT TO the source .csproj — the build");
        Console.Error.WriteLine("copies it to the output directory above. If you created it there and still see");
        Console.Error.WriteLine("this, run 'dotnet build' so the copy happens.");
        Console.Error.WriteLine();
    }

    Console.Error.WriteLine("Fastest fix, no file needed:");
    Console.Error.WriteLine("  dotnet user-secrets set --project .\\src\\MAF.RPTeam \"AzureOpenAI:Endpoint\" \"https://<resource>.openai.azure.com/\"");
    Console.Error.WriteLine("  dotnet user-secrets set --project .\\src\\MAF.RPTeam \"AzureOpenAI:Deployment\" \"gpt-4o\"");
    return 1;
}

// --- [MAF] 1 of 3: the chat client ----------------------------------------
// Microsoft.Extensions.AI's IChatClient is the provider abstraction the whole
// framework sits on. Swapping Azure OpenAI for Foundry, Anthropic, Bedrock,
// Gemini or Ollama is a change to these six lines and nothing else - the agents,
// the harness options and the skills are all provider-agnostic.
//
// Note: no .UseFunctionInvocation() here on purpose. Both ChatClientAgent and
// HarnessAgent wrap the supplied client with their own chat pipeline (see
// ChatClientAgentOptions.UseProvidedChatClientAsIs, which defaults to false),
// and the harness explicitly lists function invocation as one of its stages.
// Adding it here as well would risk two nested tool-calling loops.
var azureClient = new AzureOpenAIClient(new Uri(aoai.Endpoint), new DefaultAzureCredential());

IChatClient providerChatClient = azureClient
    .GetChatClient(aoai.Deployment)
    .AsIChatClient();

using IChatClient chatClient = browserRuntime.IsAvailable
    ? new ToolImagePromotingChatClient(providerChatClient)
    : providerChatClient;

if (args.Length == 1 &&
    string.Equals(args[0], "--browser-vision-smoke", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        var phase2Browser = ((Phase2BrowserRuntime)browserRuntime).Smoke;
        var expectedMarker = await phase2Browser.PrepareVisionSmokeAsync();
        var visionAgent = chatClient.AsAIAgent(
            instructions:
                "You verify screenshots. Call BrowserScreenshot exactly once. Read the " +
                "large PHASE 2 VISION CODE from the image and return only that code.",
            name: "phase2-vision-smoke",
            description: "Confirms that browser tool screenshots reach the model as images.",
            tools: phase2Browser.VisionSmokeTools());
        var response = await visionAgent.RunAsync(
            "Read the exact PHASE 2 VISION CODE shown in the browser.",
            cancellationToken: CancellationToken.None);
        var passed = response.Text.Contains(expectedMarker, StringComparison.OrdinalIgnoreCase);

        Console.WriteLine($"Phase 2 vision smoke: {(passed ? "PASS" : "FAIL")}");
        Console.WriteLine($"  expected : {expectedMarker}");
        Console.WriteLine($"  observed : {response.Text.Trim()}");
        return passed ? 0 : 1;
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        Console.Error.WriteLine("Phase 2 vision smoke: unavailable");
        Console.Error.WriteLine($"  detail : {ex.Message}");
        return 1;
    }
}

// --- [MAF] 2 of 3: the team ----------------------------------------------
// Four plain ChatClientAgents, and one HarnessAgent that delegates to them.
// TeamFactory is where the harness capabilities are configured and annotated.
using var factory = new TeamFactory(
    chatClient,
    mission,
    browserRuntime.Ember,
    browserRuntime.Sentinel);
using var verifier = new MissionAcceptanceVerifier(
    mission,
    browserRuntime.Acceptance);

var skillsPath = Path.Combine(AppContext.BaseDirectory, "skills");
var specialists = factory.CreateSpecialists(skillsPath);

var phoenixOptions = factory.BuildPhoenixOptions(specialists, skillsPath);
var phoenix = chatClient.AsHarnessAgent(phoenixOptions);

// --- Banner ----------------------------------------------------------------
Console.WriteLine("MAF Agent Harness - Mission Control");
Console.WriteLine($"  model      : {aoai.Deployment} @ {new Uri(aoai.Endpoint).Host}");
Console.WriteLine($"  workspace  : {mission.WorkspaceRoot}");
Console.WriteLine($"  skills     : {skillsPath}");
Console.WriteLine($"  team       : phoenix (harness) + {string.Join(", ", specialists.Select(a => a.Name))} (plain)");
Console.WriteLine($"  browser    : {(browserRuntime.IsAvailable ? $"playwright ({(browserSettings.Headless ? "headless" : "headed")})" : "unavailable (Phase 1 - textual verification)")}");
Console.WriteLine($"  telemetry  : {(telemetry.Enabled ? $"azure monitor ({telemetry.ServiceName})" : "not configured")}");
Console.WriteLine();

// Show what the harness is actually supplying, read from the options object.
HarnessComposition.Print(phoenixOptions);

// --- [MAF] 3 of 3: run it -------------------------------------------------
var orchestrator = new BackgroundAgentsOrchestrator(
    phoenix,
    verifier,
    mission.AcceptanceMaxRounds,
    enforceAcceptance: !args.Contains("--skip-acceptance", StringComparer.OrdinalIgnoreCase));

var missionArgs = args
    .Where(arg => !string.Equals(arg, "--skip-acceptance", StringComparison.OrdinalIgnoreCase))
    .ToArray();

var missionText = missionArgs.Length > 0
    ? string.Join(' ', missionArgs)
    : "Mission Control: the Team Dashboard app is broken. The frontend crashes on load and " +
      "the API returns 500 errors. There are no tests and the README is out of date. " +
      "Diagnose and fix everything, then report what each agent did.";

Console.WriteLine($"Mission: {missionText}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine();
    Console.WriteLine("Cancellation requested - letting in-flight tasks finish...");
    cts.Cancel();
};

using var missionActivity = telemetry.StartMission();
if (missionActivity is not null)
{
    Console.WriteLine($"Trace ID: {missionActivity.TraceId}");
}

try
{
    var report = await orchestrator.RunMissionStreamingAsync(missionText, cts.Token);
    missionActivity?.SetStatus(ActivityStatusCode.Ok);

    Console.WriteLine();
    Console.WriteLine("=== FINAL REPORT ===");
    Console.WriteLine(report);
    return 0;
}
catch (OperationCanceledException)
{
    missionActivity?.SetStatus(ActivityStatusCode.Error, "Mission cancelled.");
    Console.WriteLine("Mission cancelled.");
    return 130;
}
catch (MissionAcceptanceException ex)
{
    missionActivity?.SetStatus(ActivityStatusCode.Error, "Application acceptance failed.");
    Console.Error.WriteLine();
    Console.Error.WriteLine(ex.Message);
    return 2;
}
catch (Exception ex)
{
    missionActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    // A demo should not stack-trace on a bad endpoint or an expired credential.
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Mission failed: {ex.GetType().Name}: {ex.Message}");

    var root = ex;
    while (root.InnerException is not null)
    {
        root = root.InnerException;
    }

    if (!ReferenceEquals(root, ex))
    {
        Console.Error.WriteLine($"  root cause: {root.GetType().Name}: {root.Message}");
    }

    Console.Error.WriteLine();
    Console.Error.WriteLine("Check AzureOpenAI:Endpoint, the deployment name, and your credential.");
    return 1;
}
finally
{
    missionActivity?.Stop();
    if (!telemetry.ForceFlush())
    {
        Console.Error.WriteLine("Azure Monitor telemetry flush timed out.");
    }
}
