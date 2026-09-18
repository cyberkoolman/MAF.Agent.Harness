using System.Text;

namespace MAF.RPTeam.Phases.Phase2.Browser;

internal sealed record BrowserDiagnostic(
    DateTimeOffset Timestamp,
    string Url,
    string Level,
    string Kind,
    string Message);

internal sealed class BrowserDiagnosticBuffer
{
    private static readonly IReadOnlyDictionary<string, int> LevelRanks =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["debug"] = 0,
            ["log"] = 1,
            ["info"] = 1,
            ["warning"] = 2,
            ["warn"] = 2,
            ["error"] = 3,
            ["assert"] = 3,
            ["trace"] = 1,
            ["table"] = 1,
            ["dir"] = 1,
            ["group"] = 1,
            ["startgroup"] = 1,
            ["endgroup"] = 1,
            ["count"] = 1,
            ["timeend"] = 1,
            ["verbose"] = 1,
        };

    private readonly object _sync = new();
    private readonly int _capacity;
    private readonly Queue<BrowserDiagnostic> _items = new();

    public BrowserDiagnosticBuffer(int capacity)
    {
        _capacity = capacity;
    }

    public void Add(BrowserDiagnostic diagnostic)
    {
        lock (_sync)
        {
            _items.Enqueue(diagnostic);
            while (_items.Count > _capacity)
            {
                _items.Dequeue();
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _items.Clear();
        }
    }

    public IReadOnlyList<BrowserDiagnostic> Read(string minimumLevel)
    {
        var threshold = GetRank(minimumLevel, unknownRank: 3);
        lock (_sync)
        {
            return _items
                .Where(item => GetRank(item.Level, unknownRank: 1) >= threshold)
                .ToArray();
        }
    }

    public string Format(string minimumLevel)
    {
        var items = Read(minimumLevel);
        if (items.Count == 0)
        {
            return $"No browser console or page errors at level '{minimumLevel}' or higher.";
        }

        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.Append('[')
                .Append(item.Timestamp.ToString("HH:mm:ss.fff"))
                .Append("] ")
                .Append(item.Level.ToUpperInvariant())
                .Append(' ')
                .Append(item.Kind)
                .Append(" at ")
                .Append(item.Url)
                .Append(": ")
                .AppendLine(item.Message);
        }

        return sb.ToString().TrimEnd();
    }

    private static int GetRank(string level, int unknownRank) =>
        LevelRanks.TryGetValue(level.Trim(), out var rank) ? rank : unknownRank;
}
