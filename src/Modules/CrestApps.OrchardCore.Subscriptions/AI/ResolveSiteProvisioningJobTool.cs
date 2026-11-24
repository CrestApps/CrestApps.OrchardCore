using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// Retries a site provisioning job, or closes an abandoned one whose site was created by hand, with the rules of
/// Subscriptions › Site provisioning (<see cref="TenantProvisioningJobActions"/>, which the page's Retry and Mark as
/// created actions now call too).
/// </summary>
/// <remarks>
/// A retry is queued, not run inline: the job is reset and saved, and the Tenant Provisioning background task, which runs
/// every minute, creates the site. The page runs the setup inside its request; a chat call shares one service scope for
/// the whole message and a tenant setup can take minutes, so the tool leaves it to the task and says so.
/// </remarks>
internal sealed class ResolveSiteProvisioningJobTool : SubscriptionsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "resolveSiteProvisioningJob";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "jobId": {
          "type": "string",
          "description": "The job's id, from listSiteProvisioningJobs."
        },
        "action": {
          "type": "string",
          "enum": ["retry", "markCreated"],
          "description": "retry to try creating the site again; markCreated to close an abandoned job whose site was created by hand."
        },
        "siteUrl": {
          "type": "string",
          "description": "For markCreated: the address of the site created by hand, such as https://bluebird.example.com. The customer sees it as their site."
        }
      },
      "required": ["jobId", "action"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Retries a site provisioning job now (action: retry), or closes an abandoned job whose site was created by hand, with its URL (action: markCreated). Retry is refused once the job no longer holds the administrator password; then create the site with the tenant tools and mark the job created.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => SubscriptionPermissions.ManageSubscriptions;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var action = ReadString(arguments, "action");

        if (action is not ("retry" or "markCreated"))
        {
            return "Nothing was done: action must be retry or markCreated.";
        }

        var jobId = ReadString(arguments, "jobId");

        if (jobId is null)
        {
            return "Nothing was done: give the job's jobId. Use listSiteProvisioningJobs to find it.";
        }

        var store = services.GetRequiredService<ITenantProvisioningJobStore>();
        var job = await store.FindByIdAsync(jobId, cancellationToken);

        if (job is null)
        {
            return $"Nothing was done: no site provisioning job has the id '{jobId}'. Use listSiteProvisioningJobs to find it.";
        }

        if (action == "retry")
        {
            if (job.Status == TenantProvisioningStatus.Succeeded)
            {
                return $"Nothing was done: the site '{job.TenantName}' has already been created.";
            }

            if (job.Status == TenantProvisioningStatus.Running)
            {
                return $"Nothing was done: the site '{job.TenantName}' is being created right now. Check again with listSiteProvisioningJobs in a minute.";
            }

            if (!TenantProvisioningJobActions.CanRetry(job))
            {
                return $"Nothing was done: the job for '{job.TenantName}' no longer holds the administrator password, so the site can't be created automatically. Create the site by hand with the tenant tools, then use action markCreated with its URL.";
            }

            TenantProvisioningJobActions.PrepareRetry(job);

            await store.UpdateAsync(job, cancellationToken);

            if (await CommitAsync(services, cancellationToken) is { } retryError)
            {
                return retryError;
            }

            return Json(new
            {
                id = job.ItemId,
                tenantName = job.TenantName,
                status = "queued",
                message = $"The job for '{job.TenantName}' was reset and queued. The Tenant Provisioning background task tries to create the site within about a minute; check the outcome with listSiteProvisioningJobs.",
            });
        }

        if (!TenantProvisioningJobActions.CanMarkCreated(job))
        {
            return $"Nothing was done: only an abandoned job can be marked as created, and the job for '{job.TenantName}' is {ToCamel(job.Status.ToString())}. Retry it instead.";
        }

        var siteUrl = ReadString(arguments, "siteUrl");

        if (siteUrl is null ||
            !Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return "Nothing was done: give the siteUrl of the site you created, a full http or https address. The customer sees it as their site.";
        }

        TenantProvisioningJobActions.MarkCreated(job, siteUrl, services.GetRequiredService<IClock>().UtcNow);

        await store.UpdateAsync(job, cancellationToken);

        if (await CommitAsync(services, cancellationToken) is { } markError)
        {
            return markError;
        }

        return Json(new
        {
            id = job.ItemId,
            tenantName = job.TenantName,
            status = ToCamel(job.Status.ToString()),
            siteUrl = job.SiteUrl,
            completed = await LocalTimeAsync(services, job.CompletedUtc),
            message = $"The job for '{job.TenantName}' was marked as created; the customer now sees the site at {job.SiteUrl}.",
        });
    }
}
