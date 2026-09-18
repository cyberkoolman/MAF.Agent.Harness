using System.ComponentModel;
using System.Net;
using Microsoft.Extensions.AI;

namespace MAF.RPTeam.Tools;

/// <summary>
/// Replaces the <c>curl</c> calls in the OpenClaw skills with a first-class tool.
///
/// Restricted to loopback by default: the demo agents only ever talk to the local
/// frontend and backend, and an agent that reads a web app should not be able to
/// reach arbitrary hosts.
/// </summary>
public sealed class HttpTools : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _loopbackOnly;

    public HttpTools(bool loopbackOnly = true, TimeSpan? timeout = null)
    {
        _loopbackOnly = loopbackOnly;
        _client = new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(30) };
    }

    public IList<AITool> AsTools() => new List<AITool>
    {
        AIFunctionFactory.Create(HttpGet),
    };

    [Description("Issue an HTTP GET and return the status code and response body. The curl replacement.")]
    public async Task<string> HttpGet(
        [Description("Absolute URL, for example http://localhost:4000/api/users/999")] string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return $"ERROR: not an absolute URL: {url}";
        }

        if (_loopbackOnly && !IsLoopback(uri))
        {
            return $"ERROR: only loopback URLs are permitted in this demo. Rejected host: {uri.Host}";
        }

        try
        {
            using var response = await _client.GetAsync(uri);
            var body = await response.Content.ReadAsStringAsync();

            if (body.Length > 8_000)
            {
                body = body.Substring(0, 8_000) + "\n... [truncated]";
            }

            return $"status={(int)response.StatusCode} {response.StatusCode}\n--- body ---\n{body}";
        }
        catch (Exception ex)
        {
            return $"ERROR: request failed: {ex.GetType().Name}: {ex.Message}";
        }
    }

    private static bool IsLoopback(Uri uri) =>
        uri.IsLoopback ||
        string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(uri.Host, out var ip) && IPAddress.IsLoopback(ip));

    public void Dispose() => _client.Dispose();
}
