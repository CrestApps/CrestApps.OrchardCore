using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class LeadStatusHandler : AuditedCatalogEntryHandlerBase<LeadStatus>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadStatusHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<LeadStatusHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidatingContext<LeadStatus> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(LeadStatus.Name)]));
        }

        // A new lead cannot start out finished, so only an open status can be the default.
        if (context.Model.IsDefault && (context.Model.IsClosed || context.Model.IsConverted))
        {
            context.Result.Fail(new ValidationResult(S["Only an open status can be the one a new lead starts in."], [nameof(LeadStatus.IsDefault)]));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void Populate(LeadStatus model, JsonNode data)
    {
        base.Populate(model, data);

        if (data is null)
        {
            return;
        }

        var name = data[nameof(LeadStatus.Name)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(name))
        {
            model.Name = name;
        }

        // The converted status is always closed: a converted lead is never worked again.
        if (model.IsConverted)
        {
            model.IsClosed = true;
        }

        var properties = data[nameof(LeadStatus.Properties)]?.AsObject();

        if (properties != null)
        {
            model.Properties ??= new Dictionary<string, object>();

            foreach (var (key, value) in properties)
            {
                model.Properties[key] = value;
            }
        }
    }
}
