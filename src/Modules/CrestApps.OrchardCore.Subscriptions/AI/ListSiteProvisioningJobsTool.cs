using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// Lists the jobs that create sold sites as Subscriptions › Site provisioning does: every job from
/// <see cref="ITenantProvisioningJobStore"/>, newest first, optionally of one status. The administrator password the job
/// keeps, the administrator's user name and email, and the customer's contact email are never returned.
/// </summary>
internal sealed class ListSiteProvisioningJobsTool : SubscriptionsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listSiteProvisioningJobs";

    private const int MaxErrorLength = 500;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "status": {
          "type": "string",
          "enum": ["pending", "running", "succeeded", "failed", "abandoned"],
          "description": "Only jobs in this status. Abandoned jobs are paid sites the store stopped trying to create. Leave it out for every status."
        },
        "tenantName": {
          "type": "string",
          "description": "Only the job of the site with this tenant name."
        },
        "page": {
          "type": "integer",
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "How many jobs per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the jobs that create sold sites, with their status, attempts, last error, tenant name and site URL, optionally filtered by status. Use it to find paid customers whose site was not created (status Abandoned).";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => SubscriptionPermissions.ManageSubscriptions;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!TryReadEnum<TenantProvisioningStatus>(arguments, "status", out var status))
        {
            return "Nothing was listed: status must be pending, running, succeeded, failed or abandoned.";
        }

        var tenantName = ReadString(arguments, "tenantName");
        var (page, pageSize) = ReadPaging(arguments);

        var jobs = (await services.GetRequiredService<ITenantProvisioningJobStore>().GetAllAsync(cancellationToken))
            .Where(job => !status.HasValue || job.Status == status.Value)
            .Where(job => tenantName is null || string.Equals(job.TenantName, tenantName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(job => job.CreatedUtc)
            .ToList();

        var items = new List<object>();

        foreach (var job in jobs.Skip((page - 1) * pageSize).Take(pageSize))
        {
            items.Add(new
            {
                id = job.ItemId,
                tenantName = job.TenantName,
                siteTitle = job.TenantTitle,
                status = ToCamel(job.Status.ToString()),
                attempts = job.AttemptCount,
                nextAttempt = await LocalTimeAsync(services, job.NextAttemptUtc),
                lastError = Truncate(job.LastError),
                siteUrl = job.SiteUrl,
                subscriptionId = job.SubscriptionId,
                created = await LocalTimeAsync(services, job.CreatedUtc),
                updated = await LocalTimeAsync(services, job.UpdatedUtc),
                completed = await LocalTimeAsync(services, job.CompletedUtc),
                canRetry = TenantProvisioningJobActions.CanRetry(job),
                canMarkCreated = TenantProvisioningJobActions.CanMarkCreated(job),
            });
        }

        return ListResult(items, jobs.Count, page, pageSize);
    }

    private static string Truncate(string text)
        => string.IsNullOrEmpty(text) || text.Length <= MaxErrorLength
            ? text
            : string.Concat(text.AsSpan(0, MaxErrorLength), "…");
}
