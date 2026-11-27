using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Banking.Services;

/// <summary>
/// Links a bank to an account with a link provider, for the admin page and for My Bank Accounts alike: starts the
/// browser part, then applies what the provider read, saves the account and lets the provider keep or let go of what it
/// holds.
/// </summary>
public sealed class BankAccountLinkFlow
{
    private readonly IBankAccountService _bankAccountService;
    private readonly IBankAccountManager _manager;
    private readonly IShapeFactory _shapeFactory;
    private readonly IDisplayManager<BankAccountLinkViewModel> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;

    internal readonly IStringLocalizer S;

    public BankAccountLinkFlow(
        IBankAccountService bankAccountService,
        IBankAccountManager manager,
        IShapeFactory shapeFactory,
        IDisplayManager<BankAccountLinkViewModel> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IStringLocalizer<BankAccountLinkFlow> stringLocalizer)
    {
        _bankAccountService = bankAccountService;
        _manager = manager;
        _shapeFactory = shapeFactory;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns the link providers a person can use now.
    /// </summary>
    public async Task<IReadOnlyList<IBankAccountLinkProvider>> GetAvailableProvidersAsync(CancellationToken cancellationToken = default)
    {
        var available = new List<IBankAccountLinkProvider>();

        foreach (var provider in _bankAccountService.LinkProviders)
        {
            if (await provider.IsAvailableAsync(cancellationToken))
            {
                available.Add(provider);
            }
        }

        return available;
    }

    /// <summary>
    /// Returns an available provider by name, or <see langword="null"/>.
    /// </summary>
    public async Task<IBankAccountLinkProvider> FindProviderAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var provider = _bankAccountService.LinkProviders.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));

        return provider is not null && await provider.IsAvailableAsync(cancellationToken) ? provider : null;
    }

    /// <summary>
    /// Starts linking and builds the page's model, or returns why it can't start.
    /// </summary>
    public async Task<(BankAccountLinkViewModel Model, string Error)> StartAsync(IBankAccountLinkProvider provider, BankAccountLinkContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Account.Status == BankAccountStatus.Closed)
        {
            return (null, S["A closed account can't be changed. Add a new account instead."]);
        }

        var start = await provider.StartAsync(context, cancellationToken);

        if (!start.Succeeded)
        {
            return (null, start.Message ?? S["The bank can't be linked right now."]);
        }

        var shape = await _shapeFactory.CreateAsync(start.ShapeType, Arguments.From(new
        {
            start.ClientPayload,
            ProviderName = provider.Name,
        }));

        var model = new BankAccountLinkViewModel
        {
            ProviderName = provider.Name,
            ProviderDisplayName = provider.DisplayName,
            IsNew = context.IsNew,
            AccountName = context.IsNew ? null : context.Account.Name,
            MaskedNumber = context.IsNew ? null : BankAccountNumbers.Masked(context.Account),
            LinkShape = shape,
        };

        model.Editor = await _displayManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, model.IsNew);

        return (model, null);
    }

    /// <summary>
    /// Applies what the person linked and saves the account.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="context">The account and the person.</param>
    /// <param name="clientResult">What the browser part posted.</param>
    /// <param name="name">For a new account, the name it is listed by, or blank for one made from the bank.</param>
    /// <param name="holderName">For a new account, the holder's name as the bank knows it.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The errors; empty when the account was saved.</returns>
    public async Task<IReadOnlyList<string>> CompleteAsync(
        IBankAccountLinkProvider provider,
        BankAccountLinkContext context,
        string clientResult,
        string name,
        string holderName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(context);

        var account = context.Account;

        if (account.Status == BankAccountStatus.Closed)
        {
            return [S["A closed account can't be changed. Add a new account instead."]];
        }

        if (string.IsNullOrWhiteSpace(clientResult))
        {
            return [S["The bank was not linked. Try again."]];
        }

        var result = await provider.CompleteAsync(context, clientResult, cancellationToken);

        if (!result.Succeeded)
        {
            return [result.Message ?? S["The bank could not be linked."]];
        }

        var saved = false;

        try
        {
            if (context.IsNew)
            {
                account.HolderName = holderName?.Trim();
                account.Name = !string.IsNullOrWhiteSpace(name)
                    ? name.Trim()
                    : DefaultName(result);
            }

            var errors = _bankAccountService.ApplyLink(account, provider.Name, result);

            if (errors.Count > 0)
            {
                return errors.Select(error => error.Message).ToArray();
            }

            var validation = await _manager.ValidateAsync(account, cancellationToken);

            if (!validation.Succeeded)
            {
                return validation.Errors.Select(error => error.ErrorMessage).ToArray();
            }

            if (context.IsNew)
            {
                await _manager.CreateAsync(account, cancellationToken: cancellationToken);
            }
            else
            {
                await _manager.UpdateAsync(account, cancellationToken: cancellationToken);
            }

            saved = true;

            return [];
        }
        finally
        {
            await provider.FinishAsync(context, result, saved, cancellationToken);
        }
    }

    private string DefaultName(BankAccountLinkResult result)
    {
        var bank = string.IsNullOrWhiteSpace(result.BankName) ? S["Bank account"].Value : result.BankName.Trim();

        return string.IsNullOrWhiteSpace(result.Mask) ? bank : $"{bank} ••••{result.Mask.Trim()}";
    }
}
