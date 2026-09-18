using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MAF.RPTeam.Observability;

public sealed class MissionTelemetry : IDisposable
{
    public const string HarnessSourceName = "MAF.RPTeam";
    public const string ApplicationSourceName = "MAF.RPTeam.Application";

    private static readonly ActivitySource Source = new(ApplicationSourceName);
    private readonly TracerProvider? _tracerProvider;

    private MissionTelemetry(TracerProvider? tracerProvider, string serviceName)
    {
        _tracerProvider = tracerProvider;
        ServiceName = serviceName;
    }

    public bool Enabled => _tracerProvider is not null;

    public string ServiceName { get; }

    public static MissionTelemetry Create(IConfiguration configuration)
    {
        var settings =
            configuration.GetSection("AzureMonitor").Get<AzureMonitorSettings>() ??
            new AzureMonitorSettings();
        var environmentConnectionString =
            Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        var connectionString = string.IsNullOrWhiteSpace(environmentConnectionString)
            ? settings.ConnectionString
            : environmentConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new MissionTelemetry(null, settings.ServiceName);
        }

        var resource = ResourceBuilder.CreateDefault()
            .AddService(settings.ServiceName)
            .AddAttributes(
            [
                new KeyValuePair<string, object>("deployment.environment.name", settings.Environment),
            ]);

        var provider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resource)
            .AddSource(HarnessSourceName, ApplicationSourceName)
            .AddAzureMonitorTraceExporter(options => options.ConnectionString = connectionString)
            .Build();

        return new MissionTelemetry(provider, settings.ServiceName);
    }

    public Activity? StartMission()
    {
        var activity = Source.StartActivity("mission.run", ActivityKind.Internal);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag(
            "mission.id",
            Environment.GetEnvironmentVariable("RPTEAM_TELEMETRY_MISSION_ID") ??
            $"mission-{Guid.NewGuid():N}");
        activity.SetTag(
            "mission.trigger",
            Environment.GetEnvironmentVariable("RPTEAM_TELEMETRY_TRIGGER") ?? "cli");

        var channelId = Environment.GetEnvironmentVariable("RPTEAM_TELEMETRY_CHANNEL_ID");
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            activity.SetTag("messaging.channel.id", channelId);
        }

        return activity;
    }

    public static Activity? StartAcceptanceRound(int round)
    {
        var activity = Source.StartActivity("mission.acceptance", ActivityKind.Internal);
        activity?.SetTag("mission.acceptance.round", round);
        return activity;
    }

    public string RunSmoke()
    {
        if (!Enabled)
        {
            throw new InvalidOperationException(
                "AzureMonitor:ConnectionString is not configured.");
        }

        using var activity = Source.StartActivity("telemetry.smoke", ActivityKind.Internal);
        activity?.SetTag("telemetry.smoke", true);
        activity?.SetStatus(ActivityStatusCode.Ok);
        var traceId = activity?.TraceId.ToString() ?? "unavailable";
        activity?.Stop();

        if (_tracerProvider?.ForceFlush(10000) != true)
        {
            throw new TimeoutException("Azure Monitor telemetry did not flush within 10 seconds.");
        }

        return traceId;
    }

    public bool ForceFlush() => _tracerProvider?.ForceFlush(10000) ?? true;

    public void Dispose() => _tracerProvider?.Dispose();
}
