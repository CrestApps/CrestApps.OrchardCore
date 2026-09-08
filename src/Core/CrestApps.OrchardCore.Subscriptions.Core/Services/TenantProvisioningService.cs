using System.Security.Cryptography;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Abstractions.Setup;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Setup.Services;
using OrchardCore.Workflows.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// Creates the sites customers have paid for, one durable job at a time.
/// </summary>
/// <remarks>
/// Provisioning is separated from the checkout on purpose. It is slow, it touches a database, and it fails
/// for reasons that have nothing to do with the buyer. Running it inline means a slow recipe, a brief
/// outage, or a deployment restarting the process leaves somebody who paid with no site and nothing to
/// retry.
///
/// So the work here is arranged around one rule: never lose a paid-for site. The job is claimed under a
/// lock so two nodes cannot build the same tenant twice, every failure is recorded on the job with a
/// growing back-off, and after enough failures the job is marked abandoned rather than retried forever —
/// because at that point the honest answer is that a person needs to look at it, not that the site will
/// eventually appear.
/// </remarks>
public sealed class TenantProvisioningService : ITenantProvisioningService
{
    // Enough attempts to ride out a restart, a migration, and a transient database problem, but not so many
    // that a genuinely broken recipe is retried for days while the customer waits.
    private const int MaxAttempts = 5;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(15);

    private readonly ITenantProvisioningJobStore _jobStore;
    private readonly ISiteService _siteService;
    private readonly IShellHost _shellHost;
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly ISetupService _setupService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IDistributedLock _distributedLock;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningService"/> class.
    /// </summary>
    /// <param name="jobStore">The durable provisioning job store.</param>
    /// <param name="siteService">The site service used to read the operator's onboarding settings.</param>
    /// <param name="shellHost">The shell host.</param>
    /// <param name="shellSettingsManager">The manager used to create tenant shell settings.</param>
    /// <param name="setupService">The setup service that initializes the new tenant.</param>
    /// <param name="dataProtectionProvider">The provider used to unprotect the administrator password.</param>
    /// <param name="distributedLock">The lock that keeps two nodes from building the same tenant.</param>
    /// <param name="serviceProvider">The service provider used to resolve the optional workflow manager.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public TenantProvisioningService(
        ITenantProvisioningJobStore jobStore,
        ISiteService siteService,
        IShellHost shellHost,
        IShellSettingsManager shellSettingsManager,
        ISetupService setupService,
        IDataProtectionProvider dataProtectionProvider,
        IDistributedLock distributedLock,
        IServiceProvider serviceProvider,
        ISession session,
        IClock clock,
        ILogger<TenantProvisioningService> logger)
    {
        _jobStore = jobStore;
        _siteService = siteService;
        _shellHost = shellHost;
        _shellSettingsManager = shellSettingsManager;
        _setupService = setupService;
        _dataProtectionProvider = dataProtectionProvider;
        _distributedLock = distributedLock;
        _serviceProvider = serviceProvider;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TenantProvisioningStatus> ProvisionAsync(string jobId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(jobId);

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync("TENANT_PROVISIONING_" + jobId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            // Another node holds this job. Reporting it as still running is honest and leaves the sweep to
            // pick it up later if that node dies.
            return TenantProvisioningStatus.Running;
        }

        await using var _ = locker;

        // Re-read inside the lock: the holder may have just finished this very job.
        var job = await _jobStore.FindByIdAsync(jobId, cancellationToken);

        if (job is null || job.IsTerminal)
        {
            return job?.Status ?? TenantProvisioningStatus.Abandoned;
        }

        job.Status = TenantProvisioningStatus.Running;
        job.AttemptCount++;

        // The next attempt is scheduled before the work starts, so a process that dies mid-setup still leaves
        // a job that becomes due again rather than one stuck in Running forever.
        job.NextAttemptUtc = _clock.UtcNow.Add(GetBackOff(job.AttemptCount));

        await _jobStore.UpdateAsync(job, cancellationToken);

        // Committed now rather than at the end of the scope. Setting up a tenant takes long enough that the
        // process can die in the middle of it, and a claim that only existed in memory would leave the job
        // looking never-attempted, to be picked up again and again by every node.
        await _session.SaveChangesAsync(cancellationToken);

        string failure;

        try
        {
            failure = await SetupAsync(job);
        }
        catch (OperationCanceledException)
        {
            // Preserve cancellation semantics; the job stays claimable and is retried.
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to provision the tenant '{TenantName}' for job '{JobId}'.", job.TenantName, job.ItemId);

            failure = exception.Message;
        }

        var now = _clock.UtcNow;

        if (failure is null)
        {
            job.Status = TenantProvisioningStatus.Succeeded;
            job.CompletedUtc = now;
            job.NextAttemptUtc = null;
            job.LastError = null;
            job.SiteUrl = BuildSiteUrl(job);

            // The password was only needed to create the administrator. Keeping it afterwards means holding
            // a customer's secret for the life of the record for no reason.
            job.ProtectedAdminPassword = null;
        }
        else
        {
            // Logged here, not only recorded on the job. A setup that reports its failure as a message
            // rather than an exception would otherwise leave nothing in the log at all, and the job keeps
            // only the newest message — so the first attempt's reason, which is usually the real one, was
            // overwritten by whatever the retry happened to hit.
            _logger.LogError(
                "Attempt {Attempt} to provision the tenant '{TenantName}' for job '{JobId}' failed: {Error}",
                job.AttemptCount, job.TenantName, job.ItemId, failure);

            job.LastError = failure;

            if (job.AttemptCount >= MaxAttempts)
            {
                // The customer has paid, so this is not a failure the site can shrug off. It is escalated to
                // a state a person has to resolve, and the secret is dropped rather than retained.
                job.Status = TenantProvisioningStatus.Abandoned;
                job.NextAttemptUtc = null;
                job.ProtectedAdminPassword = null;
            }
            else
            {
                job.Status = TenantProvisioningStatus.Failed;
            }
        }

        await _jobStore.UpdateAsync(job, cancellationToken);

        await RaiseAsync(job, failure);

        return job.Status;
    }

    private async Task<string> SetupAsync(TenantProvisioningJob job)
    {
        // A name taken between checkout and provisioning is the one collision that survives the pre-payment
        // check. It cannot be resolved automatically without renaming the customer's site behind their back.
        //
        // An *uninitialized* shell of the same name is a different thing entirely: it is what a failed setup
        // leaves behind in this process, since setup registers the tenant before it creates anything. Reading
        // that as "taken" made every retry fail for a reason invented by the previous attempt, so a job that
        // failed once could never succeed and burned its attempts until it was abandoned — with the customer
        // already charged.
        if (_shellHost.TryGetSettings(job.TenantName, out var existing) && !existing.IsUninitialized())
        {
            return $"The site name '{job.TenantName}' is already in use.";
        }

        var adminPassword = UnprotectAdminPassword(job);
        var recipes = await _setupService.GetSetupRecipesAsync();

        if (!recipes.Any())
        {
            return "No setup recipes are available.";
        }

        var onboarding = await _siteService.GetSettingsAsync<SubscriptionOnboardingSettings>();

        using var shellSettings = CreateShellSettings(job, onboarding);

        var setupContext = new SetupContext
        {
            ShellSettings = shellSettings,
            EnabledFeatures = [],
            Errors = new Dictionary<string, string>(),
            Recipe = recipes.FirstOrDefault(recipe => recipe.Name == job.RecipeName) ?? recipes.First(),
            Properties = new Dictionary<string, object>
            {
                { SetupConstants.SiteName, string.IsNullOrEmpty(job.TenantTitle) ? job.TenantName : job.TenantTitle },
                { SetupConstants.AdminUsername, job.AdminUsername },
                { SetupConstants.AdminEmail, job.AdminEmail },
                { SetupConstants.AdminPassword, adminPassword },
                { SetupConstants.SiteTimeZone, _clock.GetSystemTimeZone() },
                { SetupConstants.DatabaseProvider, shellSettings["DatabaseProvider"] },
                { SetupConstants.DatabaseConnectionString, shellSettings["ConnectionString"] },
                { SetupConstants.DatabaseTablePrefix, shellSettings["TablePrefix"] },
                { SetupConstants.DatabaseSchema, shellSettings["Schema"] },
            },
        };

        await _setupService.SetupAsync(setupContext);

        return setupContext.Errors.Count == 0
            ? null
            : string.Join("; ", setupContext.Errors.Select(error => $"{error.Key}: {error.Value}"));
    }

    private ShellSettings CreateShellSettings(TenantProvisioningJob job, SubscriptionOnboardingSettings onboarding)
    {
        var settings = _shellSettingsManager
            .CreateDefaultSettings()
            .AsUninitialized()
            .AsDisposable();

        settings.Name = job.TenantName;
        settings.RequestUrlPrefix = job.Prefix;
        settings.RequestUrlHost = job.Domains is { Length: > 0 } ? string.Join(',', job.Domains) : null;

        if (!string.IsNullOrEmpty(job.FeatureProfile))
        {
            settings["FeatureProfile"] = job.FeatureProfile;
        }

        // Setup refuses outright without a database provider, and the host's defaults do not necessarily
        // carry one, so the operator's onboarding settings decide it — falling back to the provider that
        // needs nothing configured. Without this a site could be sold and paid for but never created.
        if (string.IsNullOrEmpty(settings["DatabaseProvider"]))
        {
            settings["DatabaseProvider"] = onboarding.GetDatabaseProviderOrDefault();

            if (!string.IsNullOrWhiteSpace(onboarding.ConnectionString))
            {
                settings["ConnectionString"] = onboarding.ConnectionString.Trim();
            }

            if (!string.IsNullOrWhiteSpace(onboarding.Schema))
            {
                settings["Schema"] = onboarding.Schema.Trim();
            }
        }

        // Sites that share one database need to be kept apart, and nobody is around to invent a prefix for
        // a self-service purchase. A file-per-site provider needs none, and giving it one only makes the
        // table names odd.
        if (string.IsNullOrEmpty(settings["TablePrefix"]) &&
            !string.Equals(settings["DatabaseProvider"], "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            settings["TablePrefix"] = BuildTablePrefix(job.TenantName);
        }

        return settings;
    }

    // A prefix derived from the site's own name, reduced to what every supported database accepts as an
    // identifier. The tenant name is already unique across the installation, so the prefix is too.
    private static string BuildTablePrefix(string tenantName)
    {
        var prefix = new string([.. tenantName.Where(char.IsLetterOrDigit)]);

        if (prefix.Length == 0 || !char.IsLetter(prefix[0]))
        {
            prefix = "t" + prefix;
        }

        return prefix.Length > 32 ? prefix[..32] : prefix;
    }

    // The password reaches the job data-protected. A failure to unprotect it (a rotated key ring, for
    // example) must stop provisioning: creating the site with the protected value produces an administrator
    // who can never sign in, which is worse than not creating it at all.
    private string UnprotectAdminPassword(TenantProvisioningJob job)
    {
        if (string.IsNullOrEmpty(job.ProtectedAdminPassword))
        {
            throw new InvalidOperationException("The provisioning job did not capture an administrator password.");
        }

        var protector = _dataProtectionProvider.CreateProtector(SubscriptionConstants.ProtectorPurposes.TenantOnboardingStep);

        try
        {
            return protector.Unprotect(job.ProtectedAdminPassword);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("The administrator password could not be unprotected. The site was not created.", exception);
        }
    }

    private static TimeSpan GetBackOff(int attemptCount)
        => attemptCount switch
        {
            <= 1 => TimeSpan.FromMinutes(2),
            2 => TimeSpan.FromMinutes(10),
            3 => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromHours(2),
        };

    private static string BuildSiteUrl(TenantProvisioningJob job)
    {
        if (job.Domains is { Length: > 0 })
        {
            return $"https://{job.Domains[0]}/{job.Prefix}".TrimEnd('/');
        }

        return string.IsNullOrEmpty(job.Prefix) ? null : "/" + job.Prefix;
    }

    private async Task RaiseAsync(TenantProvisioningJob job, string failure)
    {
        // The workflow manager is optional; a site without Workflows still provisions.
        var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

        if (workflowManager is null)
        {
            return;
        }

        var input = new Dictionary<string, object>
        {
            ["TenantName"] = job.TenantName,
            ["JobId"] = job.ItemId,
            ["OwnerId"] = job.OwnerId,
            ["ContactEmail"] = job.ContactEmail,
            ["SiteUrl"] = job.SiteUrl,
            ["AttemptCount"] = job.AttemptCount,
        };

        if (failure is not null)
        {
            input["Errors"] = new Dictionary<string, string> { ["Provisioning"] = failure };
        }

        // A terminal failure and a retryable one are different messages: the first needs somebody to act,
        // the second is noise if it is announced every time.
        var eventName = failure is null
            ? SubscribedTenantSetupSucceededEvent.EventName
            : job.Status == TenantProvisioningStatus.Abandoned ? SubscribedTenantFailedSetupEvent.EventName : null;

        if (eventName is null)
        {
            return;
        }

        try
        {
            await workflowManager.TriggerEventAsync(eventName, input, correlationId: "TenantAutoSetup_" + job.TenantName);
        }
        catch (Exception exception)
        {
            // A workflow failure must not undo a site that was created.
            _logger.LogError(exception, "Failed to raise the '{EventName}' workflow event for tenant '{TenantName}'.", eventName, job.TenantName);
        }
    }
}
