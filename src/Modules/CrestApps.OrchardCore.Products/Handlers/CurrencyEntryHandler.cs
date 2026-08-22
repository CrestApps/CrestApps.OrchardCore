using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Products.Models;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Products.Handlers;

internal sealed class CurrencyEntryHandler : CatalogEntryHandlerBase<CurrencyEntry>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

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
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<CurrencyEntry> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity?.Name;
        }

        return PopulateAsync(context.Model, context.Data);
    }

    public override Task UpdatingAsync(UpdatingContext<CurrencyEntry> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return PopulateAsync(context.Model, context.Data);
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

    private static Task PopulateAsync(CurrencyEntry model, JsonNode data)
    {
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

        var ownerId = data[nameof(CurrencyEntry.OwnerId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(ownerId))
        {
            model.OwnerId = ownerId;
        }

        var author = data[nameof(CurrencyEntry.Author)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(author))
        {
            model.Author = author;
        }

        var createdUtc = data[nameof(CurrencyEntry.CreatedUtc)]?.GetValue<DateTime?>();

        if (createdUtc.HasValue)
        {
            model.CreatedUtc = createdUtc.Value;
        }

        var modifiedUtc = data[nameof(CurrencyEntry.ModifiedUtc)]?.GetValue<DateTime?>();

        if (modifiedUtc.HasValue)
        {
            model.ModifiedUtc = modifiedUtc.Value;
        }

        return Task.CompletedTask;
    }
}
