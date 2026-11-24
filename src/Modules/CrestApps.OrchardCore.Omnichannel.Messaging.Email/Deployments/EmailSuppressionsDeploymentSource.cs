using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deployments;

/// <summary>
/// Writes every suppressed address, with its reason and detail, into an <c>EmailSuppressions</c> recipe step. When it
/// was suppressed and by whom belong to this site, so they stay here.
/// </summary>
internal sealed class EmailSuppressionsDeploymentSource : DeploymentSourceBase<EmailSuppressionsDeploymentStep>
{
    // The most the list gives in one page; a long list is read a page at a time.
    private const int PageSize = 200;

    private readonly IEmailSuppressionList _suppressions;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSuppressionsDeploymentSource"/> class.
    /// </summary>
    /// <param name="suppressions">The suppression list.</param>
    public EmailSuppressionsDeploymentSource(IEmailSuppressionList suppressions)
    {
        _suppressions = suppressions;
    }

    /// <inheritdoc/>
    protected override async Task ProcessAsync(EmailSuppressionsDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var page = 1; ; page++)
        {
            var (items, total) = await _suppressions.PageAsync(search: null, page, PageSize);

            foreach (var suppression in items)
            {
                if (string.IsNullOrEmpty(suppression.Address) || !seen.Add(suppression.Address))
                {
                    continue;
                }

                entries.Add(new JsonObject
                {
                    [nameof(EmailSuppression.Address)] = suppression.Address,
                    [nameof(EmailSuppression.Reason)] = suppression.Reason.ToString(),
                    [nameof(EmailSuppression.Detail)] = suppression.Detail,
                });
            }

            if (items.Count < PageSize || page * PageSize >= total)
            {
                break;
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            [EmailSuppressionsStep.EntriesName] = entries,
        });
    }
}
