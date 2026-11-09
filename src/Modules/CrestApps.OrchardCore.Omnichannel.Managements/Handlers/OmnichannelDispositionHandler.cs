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

internal sealed class OmnichannelDispositionHandler : AuditedCatalogEntryHandlerBase<OmnichannelDisposition>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelDispositionHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The http context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelDispositionHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<OmnichannelDispositionHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidatingContext<OmnichannelDisposition> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(OmnichannelDisposition.Name)]));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void Populate(OmnichannelDisposition disposition, JsonNode data)
    {
        base.Populate(disposition, data);

        if (data is null)
        {
            return;
        }

        var name = data[nameof(OmnichannelDisposition.Name)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(name))
        {
            disposition.Name = name;
        }

        var descriptionText = data[nameof(OmnichannelDisposition.Description)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(descriptionText))
        {
            disposition.Description = descriptionText;
        }

        var properties = data[nameof(OmnichannelDisposition.Properties)]?.AsObject();

        if (properties != null)
        {
            disposition.Properties ??= new Dictionary<string, object>();

            foreach (var (key, value) in properties)
            {
                disposition.Properties[key] = value;
            }
        }
    }
}
