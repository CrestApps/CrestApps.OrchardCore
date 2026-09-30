using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class LeadStatusHandler : CatalogEntryHandlerBase<LeadStatus>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

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
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<LeadStatus> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<LeadStatus> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return PopulateAsync(context.Model, context.Data);
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

    public override Task InitializedAsync(InitializedContext<LeadStatus> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(LeadStatus model, JsonNode data)
    {
        OmnichannelDeploymentSerializer.Populate(model, data);

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

        return Task.CompletedTask;
    }
}
