using System.Security.Cryptography;
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
using OrchardCore.Setup.Services;
using OrchardCore.Workflows.Services;

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
    private readonly IShellHost _shellHost;
    private readonly IShellSettingsManager _shellSettingsManager;
    private readonly ISetupService _setupService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IDistributedLock _distributedLock;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantProvisioningService"/> class.
    /// </summary>
    /// <param name="jobStore">The durable provisioning job store.</param>
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
        IShellHost shellHost,
        IShellSettingsManager shellSettingsManager,
        ISetupService setupService,
        IDataProtectionProvider dataProtectionProvider,
        IDistributedLock distributedLock,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<TenantProvisioningService> logger)
    {
        _jobStore = jobStore;
        _shellHost = shellHost;
        _shellSettingsManager = shellSettingsManager;
        _setupService = setupService;
        _dataProtectionProvider = dataProtectionProvider;
        _distributedLock = distributedLock;
        _serviceProvider = serviceProvider;
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
        if (_shellHost.TryGetSettings(job.TenantName, out _))
        {
            return $"The site name '{job.TenantName}' is already in use.";
        }

        var adminPassword = UnprotectAdminPassword(job);
        var recipes = await _setupService.GetSetupRecipesAsync();

        if (!recipes.Any())
        {
            return "No setup recipes are available.";
        }

        using var shellSettings = CreateShellSettings(job);

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

    private ShellSettings CreateShellSettings(TenantProvisioningJob job)
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

        return settings;
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
