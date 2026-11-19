using CrestApps.OrchardCore.Accounts.Indexes;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Accounts.Drivers;

/// <summary>
/// Edits an account's e-mail domains, one per line.
/// </summary>
internal sealed class AccountEmailDomainsPartDisplayDriver : ContentPartDisplayDriver<AccountEmailDomainsPart>
{
    public override IDisplayResult Edit(AccountEmailDomainsPart part, BuildPartEditorContext context)
        => Initialize<AccountEmailDomainsPartViewModel>("AccountEmailDomainsPart_Edit", model => model.Domains = string.Join(Environment.NewLine, part.Domains ?? []));

    public override async Task<IDisplayResult> UpdateAsync(AccountEmailDomainsPart part, UpdatePartEditorContext context)
    {
        var model = new AccountEmailDomainsPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        part.Domains = [.. (model.Domains ?? string.Empty)
            .Split(['\n', '\r', ',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(AccountEmailDomainsNormalizer.Normalize)
            .Where(domain => domain is not null)
            .Distinct(StringComparer.Ordinal)];

        return Edit(part, context);
    }
}
