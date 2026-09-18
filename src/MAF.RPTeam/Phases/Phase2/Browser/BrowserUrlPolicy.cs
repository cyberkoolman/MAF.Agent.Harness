namespace MAF.RPTeam.Phases.Phase2.Browser;

internal sealed class BrowserUrlPolicy
{
    private readonly Uri _frontendOrigin;
    private readonly IReadOnlyList<Uri> _allowedNetworkOrigins;

    public BrowserUrlPolicy(string frontendUrl, string backendUrl)
    {
        _frontendOrigin = ValidateConfiguredOrigin(frontendUrl, "Mission:FrontendUrl");
        _allowedNetworkOrigins =
        [
            _frontendOrigin,
            ValidateConfiguredOrigin(backendUrl, "Mission:BackendUrl"),
        ];
    }

    public Uri ValidateNavigation(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var target))
        {
            throw new InvalidOperationException(
                $"Browser navigation requires an absolute URL: {value}");
        }

        if (!IsHttp(target))
        {
            throw new InvalidOperationException(
                "Browser navigation permits only HTTP and HTTPS URLs.");
        }

        if (!string.IsNullOrEmpty(target.UserInfo))
        {
            throw new InvalidOperationException(
                "Browser navigation does not permit embedded credentials.");
        }

        if (!IsSameOrigin(target, _frontendOrigin))
        {
            throw new InvalidOperationException(
                $"Browser navigation is limited to {FormatOrigin(_frontendOrigin)}.");
        }

        return target;
    }

    public bool IsAllowedNavigation(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var target) &&
        IsSameOrigin(target, _frontendOrigin);

    public bool IsAllowedRequest(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var target))
        {
            return false;
        }

        if (target.Scheme is "data" or "blob" or "about")
        {
            return true;
        }

        return _allowedNetworkOrigins.Any(origin => IsSameOrigin(target, origin));
    }

    private static Uri ValidateConfiguredOrigin(string value, string settingName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var origin))
        {
            throw new InvalidOperationException(
                $"{settingName} is not a valid absolute URL: {value}");
        }

        if (!IsHttp(origin) || !string.IsNullOrEmpty(origin.UserInfo))
        {
            throw new InvalidOperationException(
                $"{settingName} must use HTTP or HTTPS without embedded credentials.");
        }

        return origin;
    }

    private static bool IsSameOrigin(Uri target, Uri origin) =>
        IsHttp(target) &&
        string.Equals(target.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(target.Host, origin.Host, StringComparison.OrdinalIgnoreCase) &&
        target.Port == origin.Port;

    private static bool IsHttp(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static string FormatOrigin(Uri uri) =>
        $"{uri.Scheme}://{uri.Host}:{uri.Port}";
}
