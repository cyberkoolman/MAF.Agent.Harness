namespace MAF.RPTeam.Tools;

/// <summary>
/// Confines an agent to a subtree of the demo workspace.
///
/// This is the structural replacement for OpenClaw's prompt-level rules
/// ("Never touch backend files", "Stay in your lane - frontend only").
/// In the original those were instructions the model could choose to ignore.
/// Here they are enforced in code: a path outside the scope is refused before any I/O.
/// </summary>
public sealed class WorkspaceScope
{
    private readonly string[] _allowedRoots;

    public string Root { get; }

    public string Name { get; }

    /// <param name="root">Absolute path to the included sample application.</param>
    /// <param name="name">Scope name, surfaced to the agent in refusal messages.</param>
    /// <param name="allowedSubpaths">
    /// Workspace-relative directories this scope may touch. Empty means the whole root.
    /// </param>
    public WorkspaceScope(string root, string name, params string[] allowedSubpaths)
    {
        Root = Path.GetFullPath(root);
        Name = name;

        if (!Directory.Exists(Root))
        {
            throw new DirectoryNotFoundException($"Workspace root not found: {Root}");
        }

        _allowedRoots = allowedSubpaths.Length == 0
            ? new[] { Root }
            : allowedSubpaths.Select(p => Path.GetFullPath(Path.Combine(Root, p))).ToArray();
    }

    /// <summary>
    /// Resolves a workspace-relative path, or reports why it was refused.
    ///
    /// Prefer this over <see cref="Resolve"/> anywhere the caller is a tool the model
    /// invokes. A thrown exception reaches the model as an opaque tool failure and it then
    /// guesses at the cause — in testing, a scope refusal was reported back to the user as
    /// "verify if this file exists", which is both wrong and impossible to debug. Returning
    /// an explicit reason makes the guardrail legible instead of merely effective.
    /// </summary>
    public bool TryResolve(string path, out string full, out string error)
    {
        full = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "ERROR: no path supplied.";
            return false;
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Root, path));
        }
        catch (Exception ex)
        {
            error = $"ERROR: '{path}' is not a usable path ({ex.GetType().Name}).";
            return false;
        }

        if (IsAllowed(candidate))
        {
            if (TryFindReparsePoint(candidate, out var reparsePoint))
            {
                error =
                    $"ERROR: access denied by workspace policy. '{path}' traverses the symbolic link " +
                    $"or junction '{ToRelative(reparsePoint)}'. Linked paths are not permitted.";
                return false;
            }

            full = candidate;
            return true;
        }

        // Say plainly that this was a policy decision, not a missing file, and name the
        // agent whose scope refused it so the report attributes the refusal correctly.
        error =
            $"ERROR: access denied by workspace policy. '{path}' is outside the '{Name}' scope. " +
            $"This agent may only touch: {AllowedDescription()}. " +
            "The path may well exist — it is not yours to read or write. " +
            "Report this as a scope restriction, not as a missing file, and do not retry it.";

        return false;
    }

    /// <summary>
    /// Resolves a workspace-relative path, or throws. For programmatic callers only —
    /// tools invoked by the model should use <see cref="TryResolve"/>.
    /// </summary>
    public string Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (TryResolve(path, out var full, out var error))
        {
            return full;
        }

        throw new UnauthorizedAccessException(error);
    }

    public string ToRelative(string fullPath) =>
        Path.GetRelativePath(Root, fullPath).Replace('\\', '/');

    private bool IsAllowed(string fullPath)
    {
        foreach (var allowed in _allowedRoots)
        {
            if (fullPath.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindReparsePoint(string candidate, out string reparsePoint)
    {
        reparsePoint = string.Empty;
        var relative = Path.GetRelativePath(Root, candidate);
        var current = Root;

        if (HasReparsePoint(current))
        {
            reparsePoint = current;
            return true;
        }

        foreach (var segment in relative.Split(
                     Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (TryGetAttributes(current, out var attributes) &&
                (attributes & FileAttributes.ReparsePoint) != 0)
            {
                reparsePoint = current;
                return true;
            }
        }

        return false;
    }

    private static bool HasReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            attributes = default;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            attributes = default;
            return false;
        }
    }

    private string AllowedDescription() =>
        string.Join(", ", _allowedRoots.Select(r =>
        {
            var rel = Path.GetRelativePath(Root, r).Replace('\\', '/');
            return rel == "." ? "the whole workspace" : rel + "/";
        }));
}
