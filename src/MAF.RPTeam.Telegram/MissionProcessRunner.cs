using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace MAF.RPTeam.Telegram;

internal sealed record MissionRunResult(int ExitCode, string Output);

internal sealed class MissionProcessRunner(string projectPath)
{
    public async Task<MissionRunResult> RunAsync(
        string mission,
        string missionId,
        long chatId,
        CancellationToken cancellationToken)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var repositoryRoot = Path.GetFullPath(Path.Combine(projectDirectory, "..", ".."));
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(mission);
        foreach (var key in startInfo.Environment.Keys
                     .Where(key => key.StartsWith("RPTEAM_TELEGRAM_", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            startInfo.Environment.Remove(key);
        }
        startInfo.Environment["RPTEAM_TELEMETRY_MISSION_ID"] = missionId;
        startInfo.Environment["RPTEAM_TELEMETRY_TRIGGER"] = "telegram";
        startInfo.Environment["RPTEAM_TELEMETRY_CHANNEL_ID"] = HashChannelId(chatId);

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                output.AppendLine(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                output.AppendLine(eventArgs.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("The Mission Control process did not start.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return new MissionRunResult(process.ExitCode, output.ToString().Trim());
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            throw;
        }
    }

    private static string HashChannelId(long chatId)
    {
        var bytes = Encoding.UTF8.GetBytes(chatId.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..16];
    }
}
