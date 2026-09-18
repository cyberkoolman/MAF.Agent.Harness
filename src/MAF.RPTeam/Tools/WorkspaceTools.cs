using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.AI;

namespace MAF.RPTeam.Tools;

/// <summary>
/// Read / edit / list / run, confined to a <see cref="WorkspaceScope"/> with an executable allowlist.
///
/// Replaces OpenClaw's ambient <c>exec</c> and <c>edit</c> tools. Deliberately hand-written
/// rather than using the experimental FileAccessProvider or the pre-release shell package,
/// so Phase 1 sits entirely on generally available surface.
///
/// Every method returns a string rather than throwing. A thrown exception reaches the model
/// as an opaque tool failure and it then invents an explanation — during testing a scope
/// refusal came back to the user as "verify if this file exists", which was wrong. Explicit
/// ERROR results let the agent report the actual reason.
/// </summary>
public sealed class WorkspaceTools
{
    private readonly WorkspaceScope _scope;
    private readonly string[] _allowedExecutables;
    private readonly TimeSpan _timeout;

    public WorkspaceTools(WorkspaceScope scope, string[] allowedExecutables, TimeSpan? commandTimeout = null)
    {
        _scope = scope;
        _allowedExecutables = allowedExecutables;
        _timeout = commandTimeout ?? TimeSpan.FromMinutes(3);
    }

    public IList<AITool> AsTools() => new List<AITool>
    {
        AIFunctionFactory.Create(ReadFile),
        AIFunctionFactory.Create(EditFile),
        AIFunctionFactory.Create(WriteFile),
        AIFunctionFactory.Create(ListFiles),
        AIFunctionFactory.Create(RunCommand),
    };

    [Description("Read a text file from the workspace. Returns the contents with 1-based line numbers.")]
    public string ReadFile(
        [Description("Workspace-relative path, for example backend/routes/users.js")] string path)
    {
        if (!_scope.TryResolve(path, out var full, out var denied))
        {
            return denied;
        }

        if (!File.Exists(full))
        {
            return $"ERROR: file not found: {_scope.ToRelative(full)} (the path is within your scope, but nothing is there).";
        }

        var sb = new StringBuilder();
        var lines = File.ReadAllLines(full);
        for (var i = 0; i < lines.Length; i++)
        {
            sb.Append(i + 1).Append('\t').AppendLine(lines[i]);
        }

        return sb.ToString();
    }

    [Description("Replace an exact substring in a workspace file. The substring must appear exactly once.")]
    public string EditFile(
        [Description("Workspace-relative path.")] string path,
        [Description("Exact text to find, including original indentation.")] string find,
        [Description("Replacement text.")] string replace)
    {
        if (!_scope.TryResolve(path, out var full, out var denied))
        {
            return denied;
        }

        if (!File.Exists(full))
        {
            return $"ERROR: file not found: {_scope.ToRelative(full)}";
        }

        var original = File.ReadAllText(full);
        var occurrences = CountOccurrences(original, find);

        if (occurrences == 0)
        {
            return $"ERROR: text not found in {_scope.ToRelative(full)}. Read the file and match it exactly.";
        }

        if (occurrences > 1)
        {
            return $"ERROR: text appears {occurrences} times in {_scope.ToRelative(full)}. Include more surrounding context to make it unique.";
        }

        File.WriteAllText(full, original.Replace(find, replace));
        return $"OK: edited {_scope.ToRelative(full)} (1 replacement).";
    }

    [Description("Replace the complete contents of an existing workspace file.")]
    public string WriteFile(
        [Description("Workspace-relative path.")] string path,
        [Description("Complete file contents to write.")] string content)
    {
        if (content is null)
        {
            return "ERROR: no file contents supplied.";
        }

        if (!_scope.TryResolve(path, out var full, out var denied))
        {
            return denied;
        }

        if (!File.Exists(full))
        {
            return $"ERROR: file not found: {_scope.ToRelative(full)}. File creation is not permitted.";
        }

        try
        {
            File.WriteAllText(full, content);
            return $"OK: wrote {_scope.ToRelative(full)} ({content.Length} characters).";
        }
        catch (Exception ex)
        {
            return $"ERROR: could not write {_scope.ToRelative(full)}: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [Description("List files under a workspace directory, recursively, excluding node_modules and build output.")]
    public string ListFiles(
        [Description("Workspace-relative directory. Use a single dot for the scope root.")] string directory = ".")
    {
        if (!_scope.TryResolve(directory, out var full, out var denied))
        {
            return denied;
        }

        if (!Directory.Exists(full))
        {
            return $"ERROR: directory not found: {_scope.ToRelative(full)}";
        }

        var skip = new HashSet<string>(
            ["node_modules", "coverage", "build", ".git", "dist"],
            StringComparer.OrdinalIgnoreCase);

        List<string> files;
        try
        {
            files = EnumerateFilesWithoutLinks(full, skip)
                .Select(_scope.ToRelative)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Take(300)
                .ToList();
        }
        catch (Exception ex)
        {
            return $"ERROR: could not list {_scope.ToRelative(full)}: {ex.GetType().Name}: {ex.Message}";
        }

        return files.Count == 0
            ? $"(no files under {_scope.ToRelative(full)})"
            : string.Join('\n', files);
    }

    [Description("Run an allowlisted shell command inside the workspace. Returns exit code, stdout and stderr.")]
    public string RunCommand(
        [Description("Command line, for example: npx jest --verbose")] string command,
        [Description("Workspace-relative working directory. Defaults to the scope root.")] string workingDirectory = ".")
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "ERROR: no command supplied.";
        }

        var exe = FirstToken(command);

        if (!_allowedExecutables.Contains(exe, StringComparer.OrdinalIgnoreCase))
        {
            return
                $"ERROR: access denied by workspace policy. '{exe}' is not on this agent's allowlist. " +
                $"Permitted commands: {string.Join(", ", _allowedExecutables)}. " +
                "Report this as a policy restriction and do not retry it.";
        }

        if (!_scope.TryResolve(workingDirectory, out var cwd, out var denied))
        {
            return denied;
        }

        // npm/npx/yarn ship as .cmd shims on Windows and cannot be started directly.
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd.exe", "/c " + command)
            : new ProcessStartInfo("/bin/sh", "-c " + Quote(command));

        psi.WorkingDirectory = cwd;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null)
            {
                return $"ERROR: failed to start '{exe}'.";
            }

            var stdout = proc.StandardOutput.ReadToEndAsync();
            var stderr = proc.StandardError.ReadToEndAsync();

            if (!proc.WaitForExit((int)_timeout.TotalMilliseconds))
            {
                try
                {
                    proc.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Process already exited between the timeout and the kill.
                }

                return $"ERROR: '{command}' timed out after {_timeout.TotalSeconds:F0}s.";
            }

            var outText = Truncate(stdout.Result, 12_000);
            var errText = Truncate(stderr.Result, 4_000);

            return $"exit={proc.ExitCode}\n--- stdout ---\n{outText}\n--- stderr ---\n{errText}";
        }
        catch (Exception ex)
        {
            return $"ERROR: could not run '{command}': {ex.GetType().Name}: {ex.Message}";
        }
    }

    private static string FirstToken(string command)
    {
        var trimmed = command.TrimStart();
        var idx = trimmed.IndexOf(' ');
        return idx < 0 ? trimmed : trimmed.Substring(0, idx);
    }

    private static string Quote(string s) => "\"" + s.Replace("\"", "\\\"") + "\"";

    private static IEnumerable<string> EnumerateFilesWithoutLinks(
        string root,
        IReadOnlySet<string> skippedDirectoryNames)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            foreach (var file in Directory.EnumerateFiles(current))
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0)
                {
                    yield return file;
                }
            }

            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (skippedDirectoryNames.Contains(Path.GetFileName(directory)))
                {
                    continue;
                }

                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
                {
                    pending.Push(directory);
                }
            }
        }
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(needle))
        {
            return 0;
        }

        var count = 0;
        var i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
        {
            count++;
            i += needle.Length;
        }

        return count;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + $"\n... [truncated {s.Length - max} chars]";
}
