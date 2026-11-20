using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Handlers;

namespace CrestApps.OrchardCore.Accounts.Handlers;

/// <summary>
/// Refuses an account's e-mail domain that is a public mail provider's (gmail.com: everyone shares it) or that another
/// account already lists, in the editor and in imports alike, so a person's address files their requests under one
/// account only.
/// </summary>
internal sealed class AccountEmailDomainsPartHandler : ContentPartHandler<AccountEmailDomainsPart>
{
    private readonly IAccountEmailDomains _domains;
    private readonly IOptions<AccountEmailDomainsOptions> _options;

    internal readonly IStringLocalizer S;

    public AccountEmailDomainsPartHandler(IAccountEmailDomains domains, IOptions<AccountEmailDomainsOptions> options, IStringLocalizer<AccountEmailDomainsPartHandler> stringLocalizer)
    {
        _domains = domains;
        _options = options;
        S = stringLocalizer;
    }

    public override async Task ValidatingAsync(ValidateContentContext context, AccountEmailDomainsPart part)
    {
        var domains = part.Domains ?? [];

        foreach (var domain in domains.Where(_options.Value.IsPublic))
        {
            context.Fail(S["{0} is a public mail provider's domain, which everyone shares; an account can't claim it.", domain], nameof(AccountEmailDomainsPart.Domains));
        }

        foreach (var (domain, _) in await _domains.FindClaimsAsync(domains, context.ContentItem.ContentItemId))
        {
            context.Fail(S["Another account already lists {0}.", domain], nameof(AccountEmailDomainsPart.Domains));
        }
    }
}
