using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Handlers;

internal sealed class OmnichannelDispositionHandler : ModelHandlerBase<OmnichannelDisposition>
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

    public override Task InitializingAsync(InitializingContext<OmnichannelDisposition> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task UpdatingAsync(UpdatingContext<OmnichannelDisposition> context)
        => PopulateAsync(context.Model, context.Data);

    public override Task ValidatingAsync(ValidatingContext<OmnichannelDisposition> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(OmnichannelDisposition.DisplayText)]));
        }

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<OmnichannelDisposition> context)
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

        var descriptionText = data[nameof(OmnichannelDisposition.Descriptions)]?.GetValue<string>()?.Trim();

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
            disposition.Properties ??= [];
            disposition.Properties.Merge(properties);
        }

        return Task.CompletedTask;
    }
}
