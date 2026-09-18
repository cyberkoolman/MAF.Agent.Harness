using Microsoft.Playwright;

namespace MAF.RPTeam.Phases.Phase2.Browser;

internal sealed record BrowserElementReference(long Generation, ILocator Locator);

internal sealed class BrowserReferenceRegistry
{
    private readonly Dictionary<string, BrowserElementReference> _references =
        new(StringComparer.OrdinalIgnoreCase);
    private long _generation;

    public long Generation => _generation;

    public void Invalidate()
    {
        _generation++;
        _references.Clear();
    }

    public string Add(ILocator locator)
    {
        var reference = $"ref-{_references.Count + 1}";
        _references.Add(reference, new BrowserElementReference(_generation, locator));
        return reference;
    }

    public ILocator Resolve(string reference)
    {
        if (!_references.TryGetValue(reference.Trim(), out var element))
        {
            throw new InvalidOperationException(
                $"Unknown browser reference '{reference}'. Run BrowserSnapshot again.");
        }

        if (element.Generation != _generation)
        {
            throw new InvalidOperationException(
                $"Browser reference '{reference}' is stale. Run BrowserSnapshot again.");
        }

        return element.Locator;
    }
}
