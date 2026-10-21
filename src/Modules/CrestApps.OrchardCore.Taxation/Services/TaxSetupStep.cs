using CrestApps.Core.Services;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// The setup guide's taxes: the rules that say what tax is charged where. A store that doesn't charge tax yet skips it.
/// </summary>
internal sealed class TaxSetupStep : IStoreSetupStep
{
    private readonly INamedCatalog<TaxRule> _rules;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public TaxSetupStep(
        INamedCatalog<TaxRule> rules,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<TaxSetupStep> stringLocalizer)
    {
        _rules = rules;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public string Name
        => "taxes";

    public int Order
        => 50;

    public async Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
    {
        var count = (await _rules.GetAllAsync(cancellationToken)).Count;

        return new StoreSetupStepDescription
        {
            Title = S["Charge tax"],
            Description = S["The tax added to orders, or included in prices, and where. Add a rule for each rate you charge, such as a sales tax for your state or VAT for your country. Skip it if you don't charge tax yet, or if an accountant should set it up."],
            IsDone = count > 0,
            Status = count == 0 ? null : S.Plural(count, "1 tax rule is set up.", "{0} tax rules are set up."),
            Url = _linkGenerator.GetPathByName(_httpContextAccessor.HttpContext, "TaxationRulesIndex"),
            UrlText = S["Tax rules"],
        };
    }
}
