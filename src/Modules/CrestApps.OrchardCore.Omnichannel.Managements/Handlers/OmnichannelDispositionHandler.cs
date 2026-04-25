using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class OmnichannelDispositionHandler : CatalogEntryHandlerBase<OmnichannelDisposition>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public OmnichannelDispositionHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<OmnichannelDispositionHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<OmnichannelDisposition> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<OmnichannelDisposition> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<OmnichannelDisposition> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(OmnichannelDisposition.DisplayText)]));
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<OmnichannelDisposition> context, CancellationToken cancellationToken = default)
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

    private static Task PopulateAsync(OmnichannelDisposition disposition, JsonNode data)
    {
        var displayText = data[nameof(OmnichannelDisposition.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            disposition.DisplayText = displayText;
        }

        var descriptionText = data[nameof(OmnichannelDisposition.Description)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(descriptionText))
        {
            disposition.DisplayText = descriptionText;
        }

        var captureDate = data[nameof(OmnichannelDisposition.CaptureDate)]?.GetValue<bool>();

        if (captureDate.HasValue)
        {
            disposition.CaptureDate = captureDate.Value;
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

        return Task.CompletedTask;
    }
}
