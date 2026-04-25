namespace Modules.Analytics.Api;

public interface IAnalyticsApi
{
    Task RecordAsync(string actor, string role, string action, string target, string sourceIp, CancellationToken cancellationToken = default);
}
