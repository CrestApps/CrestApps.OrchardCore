using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Models;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products.Handlers;

internal sealed class CurrencyEntryHandler : AuditedCatalogEntryHandlerBase<CurrencyEntry>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyEntryHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CurrencyEntryHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<CurrencyEntryHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        S = stringLocalizer;
    }

    public override Task ValidatingAsync(ValidatingContext<CurrencyEntry> context, CancellationToken cancellationToken = default)
    {
        var normalizedCode = CurrencyCodeUtility.Normalize(context.Model.Name);

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            context.Result.Fail(new ValidationResult(S["Currency code is required."], [nameof(CurrencyEntry.Name)]));
        }
        else if (!CurrencyCodeUtility.IsValid(normalizedCode))
        {
            context.Result.Fail(new ValidationResult(S["Currency code must be a three-letter ISO-4217 code."], [nameof(CurrencyEntry.Name)]));
        }
        else
        {
            context.Model.Name = normalizedCode;
        }

        if (string.IsNullOrWhiteSpace(context.Model.DisplayName))
        {
            context.Result.Fail(new ValidationResult(S["Display name is required."], [nameof(CurrencyEntry.DisplayName)]));
        }
        else
        {
            context.Model.DisplayName = context.Model.DisplayName.Trim();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void Populate(CurrencyEntry model, JsonNode data)
    {
        if (data is null)
        {
            return;
        }

        var name = CurrencyCodeUtility.Normalize(data[nameof(CurrencyEntry.Name)]?.GetValue<string>());

        if (!string.IsNullOrEmpty(name))
        {
            model.Name = name;
        }

        var displayName = data[nameof(CurrencyEntry.DisplayName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayName))
        {
            model.DisplayName = displayName;
        }

    }
}
