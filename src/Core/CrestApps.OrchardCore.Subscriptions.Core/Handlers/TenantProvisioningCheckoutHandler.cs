using System.Text.Json;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Setup.Services;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Adds the "new site" step to a checkout for a plan that provisions a tenant, checks the requested name is
/// still free before the customer pays, and records a durable provisioning job once they have.
/// </summary>
/// <remarks>
/// The split between checking and doing is the point. Everything that can be known before payment is checked
/// before payment, because telling somebody their site name is taken is fine and telling them after taking
/// their money is not. Everything that can only fail during setup is deferred to a durable job, because
/// creating a tenant inline in the completing request means a slow recipe, a brief database outage, or a
/// process restart leaves a customer who has paid and has no site.
/// </remarks>
public sealed class TenantProvisioningCheckoutHandler : CheckoutHandlerBase
{
    private readonly IShellHost _shellHost;
    private readonly ISetupService _setupService;
    private readonly IContentManager _contentManager;
    private readonly ITenantProvisioningJobStore _jobStore;
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly DocumentJsonSerializerOptions _jsonOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningCheckoutHandler"/> class.
    /// </summary>
    /// <param name="shellHost">The shell host used to check tenant name, prefix, and domain availability.</param>
    /// <param name="setupService">The setup service used to confirm a usable recipe exists.</param>
    /// <param name="contentManager">The content manager used to read the plan being bought.</param>
    /// <param name="jobStore">The durable provisioning job store.</param>
    /// <param name="subscriptionManager">The subscription manager used to tie the site to the agreement.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="jsonOptions">The serializer options used for persisted step data.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TenantProvisioningCheckoutHandler(
        IShellHost shellHost,
        ISetupService setupService,
        IContentManager contentManager,
        ITenantProvisioningJobStore jobStore,
        ISubscriptionManager subscriptionManager,
        IClock clock,
        ILogger<TenantProvisioningCheckoutHandler> logger,
        IOptions<DocumentJsonSerializerOptions> jsonOptions,
        IStringLocalizer<TenantProvisioningCheckoutHandler> stringLocalizer)
    {
        _shellHost = shellHost;
        _setupService = setupService;
        _contentManager = contentManager;
        _jobStore = jobStore;
        _subscriptionManager = subscriptionManager;
        _clock = clock;
        _logger = logger;
        _jsonOptions = jsonOptions.Value;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var plan = await ResolvePlanAsync(context.Session);

        if (plan is null || !plan.TryGet<TenantOnboardingPart>(out var onboarding))
        {
            return;
        }

        var step = new CheckoutFlowStep
        {
            Title = S["Your new site"],
            Description = S["Choose a name and an administrator for the site you are buying."],
            Key = SubscriptionConstants.StepKey.TenantProvisioning,
            CollectData = true,

            // After the plan's own steps and before payment: the customer should know their site name is
            // available before they are asked for a card.
            Order = 100,
        };

        step.Data["RecipeName"] = onboarding.RecipeName;
        step.Data["FeatureProfile"] = onboarding.FeatureProfile;

        context.Session.Steps.Add(step);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This runs after the money is confirmed, so it only blocks what would make the purchase impossible to
    /// fulfil at all. Throwing here fails the checkout, which compensates the payment, and that is the right
    /// outcome for a site that could never be created. Everything the customer can still fix — a name that is
    /// taken, a domain that belongs to another site — is checked by the step editor before they reach payment.
    /// </remarks>
    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        if (context.Flow.Session is not CheckoutSession session)
        {
            return;
        }

        var info = ReadStep(session);

        if (info is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(info.TenantName) || string.IsNullOrEmpty(info.ProtectedAdminPassword))
        {
            throw new InvalidOperationException("The new-site step of this checkout is incomplete, so the site cannot be created.");
        }

        var recipes = await _setupService.GetSetupRecipesAsync();

        if (!recipes.Any())
        {
            // Nothing the customer can do about this. Failing the checkout refunds them rather than taking
            // money for a site the installation cannot build.
            throw new InvalidOperationException("No setup recipes are available, so the purchased site cannot be created.");
        }
    }

    /// <inheritdoc/>
    public override async Task CompletedAsync(CheckoutFlowCompletedContext context)
    {
        if (context.Flow.Session is not CheckoutSession session)
        {
            return;
        }

        var info = ReadStep(session);

        if (info is null)
        {
            return;
        }

        // A checkout can complete more than once. Recording the intent twice would try to create the same
        // tenant twice, and the second attempt would fail on a name that is now taken by the first.
        var existing = await _jobStore.GetByCheckoutSessionAsync(session.SessionId);

        if (existing is not null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var contact = session.TryGet<CheckoutContactInfo>(out var contactInfo) ? contactInfo : null;
        var subscription = await FindSubscriptionAsync(session.SessionId);

        var job = new TenantProvisioningJob { ItemId = IdGenerator.GenerateId() };

        job.CheckoutSessionId = session.SessionId;
        job.SubscriptionId = subscription?.ItemId;
        job.OwnerId = session.OwnerId;
        job.ContactEmail = contact?.Email ?? info.AdminEmail;
        job.TenantName = info.TenantName;
        job.TenantTitle = info.TenantTitle;
        job.AdminUsername = info.AdminUsername;
        job.AdminEmail = info.AdminEmail;
        job.ProtectedAdminPassword = info.ProtectedAdminPassword;
        job.Prefix = info.Prefix;
        job.Domains = info.Domains;
        job.RecipeName = info.RecipeName;
        job.FeatureProfile = info.FeatureProfile;
        job.Status = TenantProvisioningStatus.Pending;
        job.CreatedUtc = now;
        job.UpdatedUtc = now;

        await _jobStore.CreateAsync(job);

        if (subscription is not null)
        {
            // The site exists for as long as the subscription does, so the agreement carries an entitlement
            // naming it. That is what lets the site be suspended when the customer stops paying.
            subscription.Entitlements.Add(new SubscriptionEntitlement
            {
                Kind = SubscriptionConstants.EntitlementKinds.Tenant,
                Value = info.TenantName,
            });

            await _subscriptionManager.UpdateAsync(subscription);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Recorded a tenant provisioning job for '{TenantName}' from checkout session '{SessionId}'.", info.TenantName, session.SessionId);
        }
    }

    private TenantProvisioningStep ReadStep(CheckoutSession session)
    {
        if (!session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.TenantProvisioning, out var node) || node is null)
        {
            return null;
        }

        try
        {
            return node.Deserialize<TenantProvisioningStep>(_jsonOptions.SerializerOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "The saved new-site step of checkout session '{SessionId}' could not be read.", session.SessionId);

            return null;
        }
    }

    private async Task<Subscription> FindSubscriptionAsync(string sessionId)
    {
        // The activation handler creates the agreement from the same completion and handler order is not
        // guaranteed, so a missing subscription here is normal rather than an error. The site is still
        // created; it simply is not tied to an agreement, and a one-off site purchase legitimately has none.
        var subscriptions = await _subscriptionManager.GetByCheckoutSessionAsync(sessionId);

        return subscriptions.Count == 0 ? null : subscriptions[0];
    }

    private async Task<ContentItem> ResolvePlanAsync(CheckoutSession session)
    {
        if (string.IsNullOrEmpty(session?.ReferenceId))
        {
            return null;
        }

        try
        {
            var plan = await _contentManager.GetAsync(session.ReferenceId);

            // A checkout for something that is not a priced content item is not a plan, so there is no site
            // to provision.
            return plan is not null && plan.Has<ProductPart>() ? plan : null;
        }
        catch (Exception exception)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(exception, "Could not read the plan '{ReferenceId}' while activating a checkout.", session.ReferenceId);
            }

            return null;
        }
    }
}
