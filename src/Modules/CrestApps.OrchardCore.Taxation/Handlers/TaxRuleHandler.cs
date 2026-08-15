using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Taxation.Handlers;

internal sealed class TaxRuleHandler : CatalogEntryHandlerBase<TaxRule>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TaxRuleHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<TaxRuleHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<TaxRule> context, CancellationToken cancellationToken = default)
    {
        TaxationDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<TaxRule> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        TaxationDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    public override Task InitializedAsync(InitializedContext<TaxRule> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user is not null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity?.Name;
        }

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<TaxRule> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(TaxRule.Name)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.TaxType))
        {
            context.Result.Fail(new ValidationResult(S["Tax type is required."], [nameof(TaxRule.TaxType)]));
        }

        if (string.IsNullOrWhiteSpace(context.Model.CalculationMethod))
        {
            context.Result.Fail(new ValidationResult(S["Calculation method is required."], [nameof(TaxRule.CalculationMethod)]));
        }

        if (context.Model.MinimumAmount.HasValue &&
            context.Model.MaximumAmount.HasValue &&
            context.Model.MaximumAmount.Value < context.Model.MinimumAmount.Value)
        {
            context.Result.Fail(new ValidationResult(S["The maximum amount cannot be smaller than the minimum amount."], [nameof(TaxRule.MaximumAmount)]));
        }

        if (context.Model.EffectiveFromUtc.HasValue &&
            context.Model.EffectiveToUtc.HasValue &&
            context.Model.EffectiveToUtc.Value < context.Model.EffectiveFromUtc.Value)
        {
            context.Result.Fail(new ValidationResult(S["The effective end date cannot be earlier than the effective start date."], [nameof(TaxRule.EffectiveToUtc)]));
        }

        return Task.CompletedTask;
    }
}
