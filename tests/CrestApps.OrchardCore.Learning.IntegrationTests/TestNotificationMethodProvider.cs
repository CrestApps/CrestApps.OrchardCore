using Microsoft.Extensions.Localization;
using OrchardCore.Infrastructure;
using OrchardCore.Notifications;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// A notification delivery method that delivers nothing, so the suite's site has a method people can turn on or off in
/// their notification preferences without an e-mail or SMS provider.
/// </summary>
public sealed class TestNotificationMethodProvider : INotificationMethodProvider
{
    public const string MethodName = "TestMethod";

    public string Method => MethodName;

    public LocalizedString Name => new(MethodName, "Test delivery");

    public Task<Result> SendAsync(object notify, INotificationMessage message, CancellationToken cancellationToken = default)
        => Task.FromResult(Result.Success());
}
