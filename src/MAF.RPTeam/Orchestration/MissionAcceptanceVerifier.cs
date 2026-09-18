using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MAF.RPTeam.Tools;

namespace MAF.RPTeam.Orchestration;

public sealed record MissionAcceptanceCheck(string Name, bool Passed, string Detail);

public sealed class MissionAcceptanceException : Exception
{
    public MissionAcceptanceException(string message)
        : base(message)
    {
    }
}

public sealed class MissionAcceptanceResult
{
    public MissionAcceptanceResult(IReadOnlyList<MissionAcceptanceCheck> checks)
    {
        Checks = checks;
    }

    public IReadOnlyList<MissionAcceptanceCheck> Checks { get; }

    public bool Passed => Checks.All(check => check.Passed);

    public string ToCorrectivePrompt()
    {
        var failed = Checks.Where(check => !check.Passed);
        var lines = failed.Select(check => $"- {check.Name}: {check.Detail}");

        return
            "The application-enforced acceptance gate failed. Continue the existing mission " +
            "and delegate corrections to the agent that owns each area. Do not ask the user " +
            "questions and do not report completion until every item passes.\n\n" +
            string.Join('\n', lines);
    }

    public string ToReport()
    {
        var sb = new StringBuilder();
        foreach (var check in Checks)
        {
            sb.Append(check.Passed ? "[PASS] " : "[FAIL] ")
                .Append(check.Name)
                .Append(": ")
                .AppendLine(check.Detail);
        }

        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Verifies the Mission Control result independently of agent narration.
/// </summary>
public sealed class MissionAcceptanceVerifier : IDisposable
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(3);

    private readonly string _workspaceRoot;
    private readonly string _frontendUrl;
    private readonly string _backendUrl;
    private readonly string? _initialReadme;
    private readonly IBrowserAcceptance? _browserAcceptance;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public MissionAcceptanceVerifier(
        MissionSettings settings,
        IBrowserAcceptance? browserAcceptance = null)
    {
        _workspaceRoot = Path.GetFullPath(settings.WorkspaceRoot);
        _frontendUrl = settings.FrontendUrl.TrimEnd('/');
        _backendUrl = settings.BackendUrl.TrimEnd('/');

        var readme = Path.Combine(_workspaceRoot, "README.md");
        _initialReadme = File.Exists(readme) ? File.ReadAllText(readme) : null;
        _browserAcceptance = browserAcceptance;
    }

    public async Task<MissionAcceptanceResult> VerifyAsync(
        bool requireReadmeChange = true,
        CancellationToken cancellationToken = default)
    {
        var checks = new List<MissionAcceptanceCheck>
        {
            VerifyNavbarSource(),
        };

        checks.Add(await VerifyStatusAsync(
            "missing user returns 404",
            $"{_backendUrl}/api/users/999",
            HttpStatusCode.NotFound,
            cancellationToken));
        checks.Add(await VerifyStatusAsync(
            "user stats returns 200",
            $"{_backendUrl}/api/users/1/stats",
            HttpStatusCode.OK,
            cancellationToken));
        checks.Add(await VerifyStatusAsync(
            "frontend responds",
            _frontendUrl,
            HttpStatusCode.OK,
            cancellationToken));

        if (_browserAcceptance is not null)
        {
            var browser = await _browserAcceptance.VerifyFrontendAsync(cancellationToken);
            checks.Add(new MissionAcceptanceCheck(
                "browser visual verification",
                browser.Passed,
                browser.Detail));
        }

        var backendTests = await RunCommandAsync(
            "npm test -- --runInBand --coverage=false",
            Path.Combine(_workspaceRoot, "backend"),
            cancellationToken);
        checks.Add(new MissionAcceptanceCheck(
            "backend Jest suite",
            backendTests.ExitCode == 0 && HasNonZeroTests(backendTests.Output),
            SummarizeCommand(backendTests)));

        var frontendTests = await RunCommandAsync(
            "npm test -- --watchAll=false --runInBand Navbar.test.js",
            Path.Combine(_workspaceRoot, "frontend"),
            cancellationToken);
        checks.Add(new MissionAcceptanceCheck(
            "focused Navbar Jest suite",
            frontendTests.ExitCode == 0 && HasNonZeroTests(frontendTests.Output),
            SummarizeCommand(frontendTests)));

        checks.Add(requireReadmeChange ? VerifyReadmeChanged() : VerifyReadmeExists());

        return new MissionAcceptanceResult(checks);
    }

    private MissionAcceptanceCheck VerifyNavbarSource()
    {
        var path = Path.Combine(_workspaceRoot, "frontend", "src", "components", "Navbar.jsx");
        if (!File.Exists(path))
        {
            return new MissionAcceptanceCheck("Navbar source", false, "Navbar.jsx is missing.");
        }

        var source = File.ReadAllText(path);
        var correct = Regex.IsMatch(source, @"\bitems\.map\s*\(");
        var invalid = source.Contains("items.items", StringComparison.Ordinal) ||
                      source.Contains("items.itmes", StringComparison.Ordinal) ||
                      Regex.IsMatch(source, @"(?<!\.)\bitmes\.map\s*\(");

        return new MissionAcceptanceCheck(
            "Navbar source",
            correct && !invalid,
            correct && !invalid
                ? "Navbar.jsx uses items.map(...)."
                : "Navbar.jsx must use exactly items.map(...), with no items.items or items.itmes expression.");
    }

    private MissionAcceptanceCheck VerifyReadmeChanged()
    {
        var path = Path.Combine(_workspaceRoot, "README.md");
        if (!File.Exists(path))
        {
            return new MissionAcceptanceCheck("README update", false, "README.md is missing.");
        }

        var current = File.ReadAllText(path);
        var changed = _initialReadme is null || !string.Equals(
            current,
            _initialReadme,
            StringComparison.Ordinal);

        return new MissionAcceptanceCheck(
            "README update",
            changed,
            changed ? "README.md changed during this mission." : "README.md did not change during this mission.");
    }

    private MissionAcceptanceCheck VerifyReadmeExists()
    {
        var path = Path.Combine(_workspaceRoot, "README.md");
        return new MissionAcceptanceCheck(
            "README present",
            File.Exists(path),
            File.Exists(path) ? "README.md exists." : "README.md is missing.");
    }

    private async Task<MissionAcceptanceCheck> VerifyStatusAsync(
        string name,
        string url,
        HttpStatusCode expected,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            return new MissionAcceptanceCheck(
                name,
                response.StatusCode == expected,
                $"GET {url} returned {(int)response.StatusCode}; expected {(int)expected}.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new MissionAcceptanceCheck(name, false, $"GET {url} failed: {ex.Message}");
        }
    }

    private static bool HasNonZeroTests(string output)
    {
        var match = Regex.Match(output, @"(?m)^Tests:\s+.*?(\d+)\s+passed\b");
        return match.Success && int.TryParse(match.Groups[1].Value, out var passed) && passed > 0;
    }

    private static string SummarizeCommand(CommandResult result)
    {
        var lines = result.Output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line =>
                line.StartsWith("Test Suites:", StringComparison.Ordinal) ||
                line.StartsWith("Tests:", StringComparison.Ordinal) ||
                line.StartsWith("Time:", StringComparison.Ordinal))
            .ToArray();

        var summary = lines.Length > 0
            ? string.Join(" | ", lines)
            : Truncate(result.Output, 600);

        return $"exit={result.ExitCode}; {summary}";
    }

    private static async Task<CommandResult> RunCommandAsync(
        string command,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(workingDirectory))
        {
            return new CommandResult(-1, $"Working directory is missing: {workingDirectory}");
        }

        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", "/d /s /c " + command)
            : new ProcessStartInfo("/bin/sh", "-c " + Quote(command));

        psi.WorkingDirectory = workingDirectory;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;

        Process? process = null;
        try
        {
            process = Process.Start(psi);
            if (process is null)
            {
                return new CommandResult(-1, $"Could not start command: {command}");
            }

            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CommandTimeout);

            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + Environment.NewLine + await stderr;
            return new CommandResult(process.ExitCode, output);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await TerminateAsync(process);
            return new CommandResult(-1, $"Command timed out after {CommandTimeout.TotalMinutes:F0} minutes: {command}");
        }
        catch (OperationCanceledException)
        {
            await TerminateAsync(process);
            throw;
        }
        catch (Exception ex)
        {
            return new CommandResult(-1, $"Command failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static async Task TerminateAsync(Process? process)
    {
        if (process is null || process.HasExited)
        {
            return;
        }

        try
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and termination.
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength] + "...";

    public void Dispose() => _httpClient.Dispose();

    private sealed record CommandResult(int ExitCode, string Output);
}
