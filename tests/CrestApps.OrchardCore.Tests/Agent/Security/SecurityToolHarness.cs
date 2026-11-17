using System.Text.Json;
using CrestApps.OrchardCore.Tests.Agent.AuditTrail;
using CrestApps.OrchardCore.Tests.Agent.Communications;
using CrestApps.OrchardCore.Tests.Agent.Notifications;
using CrestApps.OrchardCore.Tests.Agent.OpenId;
using CrestApps.OrchardCore.Tests.Agent.RateLimits;
using CrestApps.OrchardCore.Tests.Agent.Secrets;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Agent.Security;

/// <summary>
/// Builds the services of a tool call for the OpenID, rate limit, secret, audit trail, notification, test email and
/// test SMS tools, on doubles that keep their records in memory. Every field of the fixture that holds a secret, and
/// every record that belongs to another user, holds a value that starts with <see cref="Canary"/>, so a test can tell
/// that no tool result carries one.
/// </summary>
internal sealed class SecurityToolHarness
{
    /// <summary>
    /// The text every value a tool must not return starts with.
    /// </summary>
    public const string Canary = "CANARY";

    private readonly string _initialState;

    public SecurityToolHarness()
    {
        _initialState = GetState();
    }

    public OpenIdToolDoubles OpenId { get; } = new();

    public RateLimitToolDoubles RateLimits { get; } = new();

    public SecretToolDoubles Secrets { get; } = new();

    public AuditTrailToolDoubles AuditTrail { get; } = new();

    public NotificationToolDoubles Notifications { get; } = new();

    public TestMessageToolDoubles Messages { get; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether a user is signed in. The default is signed in.
    /// </summary>
    public bool SignedIn { get; set; } = true;

    /// <summary>
    /// Gets or sets the rule that grants or refuses a permission on a resource. The default grants everything.
    /// </summary>
    public Func<Permission, object, bool> Authorize { get; set; } = (_, _) => true;

    /// <summary>
    /// Builds the services of a tool call.
    /// </summary>
    /// <returns>The service provider.</returns>
    public ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices(SignedIn, Authorize);

        OpenId.AddTo(services);
        RateLimits.AddTo(services);
        Secrets.AddTo(services);
        AuditTrail.AddTo(services);
        Notifications.AddTo(services);
        Messages.AddTo(services, SignedIn);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Checks that no tool changed a record, read the value of a secret, sent a message or asked for the tenant to
    /// reload.
    /// </summary>
    public void VerifyNothingChanged()
    {
        Assert.Equal(_initialState, GetState());
        Assert.Empty(OpenId.Steps);
        Assert.Equal(0, RateLimits.ReleaseRequests);
        Assert.Empty(Messages.Emails);
        Assert.Empty(Messages.TextMessages);

        foreach (var store in new[] { Secrets.Database, Secrets.KeyVault, Secrets.Configuration })
        {
            Assert.Empty(store.ValueReads);
            Assert.Empty(store.Saves);
        }
    }

    private string GetState()
    {
        return JsonSerializer.Serialize(new
        {
            Applications = OpenId.Applications,
            Scopes = OpenId.Scopes,
            Policies = RateLimits.Policies.Select(policy => new
            {
                policy.PolicyId,
                policy.Name,
                policy.Description,
                policy.Scope,
                policy.Path,
                policy.GroupName,
                policy.IsEnabled,
                Limiters = policy.Limiters.Select(limiter => limiter.Properties.ToJsonString()),
            }),
            Secrets = new[] { Secrets.Database, Secrets.KeyVault, Secrets.Configuration }.Select(store => new
            {
                store.Name,
                Infos = store.GetSecretInfosAsync().GetAwaiter().GetResult(),
            }),
            Notifications = Notifications.Notifications.Select(notification => new
            {
                notification.NotificationId,
                notification.UserId,
                Read = Notifications.IsRead(notification.NotificationId),
            }),
        });
    }
}
