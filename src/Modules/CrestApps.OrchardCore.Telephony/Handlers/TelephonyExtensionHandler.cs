using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Telephony.Handlers;

/// <summary>
/// Binds imported values onto, stamps, and enforces the rules of an internal <see cref="TelephonyExtension"/> on every
/// write path: the editor, a recipe, a deployment plan, or a service that writes through the manager.
/// </summary>
/// <remarks>
/// An extension rings a user, and a user's identifier is minted by the environment that created the account, so an
/// identifier carried by a plan means nothing on another tenant. Imported data is therefore matched to its user by user
/// name, and the entry is stored with the identifier of the user it resolved to. The handler depends on the store rather
/// than the manager so checking a number for uniqueness cannot recurse into the manager that owns it.
/// </remarks>
internal sealed class TelephonyExtensionHandler : CatalogEntryHandlerBase<TelephonyExtension>
{
    private readonly ITelephonyExtensionStore _store;
    private readonly UserManager<IUser> _userManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyExtensionHandler"/> class.
    /// </summary>
    /// <param name="store">The extension store used to check number uniqueness.</param>
    /// <param name="userManager">The user manager used to resolve the user an extension rings.</param>
    /// <param name="clock">The clock used to stamp modification times.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TelephonyExtensionHandler(
        ITelephonyExtensionStore store,
        UserManager<IUser> userManager,
        IClock clock,
        IStringLocalizer<TelephonyExtensionHandler> stringLocalizer)
    {
        _store = store;
        _userManager = userManager;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<TelephonyExtension> context, CancellationToken cancellationToken = default)
        => PopulateAsync(context.Model, context.Data);

    /// <inheritdoc/>
    public override async Task UpdatingAsync(UpdatingContext<TelephonyExtension> context, CancellationToken cancellationToken = default)
    {
        await PopulateAsync(context.Model, context.Data);

        context.Model.ModifiedUtc = _clock.UtcNow;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<TelephonyExtension> context, CancellationToken cancellationToken = default)
    {
        var number = context.Model.Number?.Trim();

        if (string.IsNullOrEmpty(number))
        {
            context.Result.Fail(new ValidationResult(S["The extension number is required."], [nameof(TelephonyExtension.Number)]));
        }
        else
        {
            // Numbers are unique per tenant so a dialed extension resolves to exactly one user.
            var existing = await _store.FindByNumberAsync(number, cancellationToken);

            if (existing is not null && !string.Equals(existing.ItemId, context.Model.ItemId, StringComparison.Ordinal))
            {
                context.Result.Fail(new ValidationResult(S["Extension {0} is already assigned.", number], [nameof(TelephonyExtension.Number)]));
            }
        }

        if (string.IsNullOrWhiteSpace(context.Model.UserId))
        {
            context.Result.Fail(new ValidationResult(
                string.IsNullOrWhiteSpace(context.Model.UserName)
                    ? S["A user is required."]
                    : S["The user '{0}' was not found.", context.Model.UserName],
                [nameof(TelephonyExtension.UserId)]));
        }
        else if (await _userManager.FindByIdAsync(context.Model.UserId) is null)
        {
            context.Result.Fail(new ValidationResult(S["The selected user was not found."], [nameof(TelephonyExtension.UserId)]));
        }
    }

    private async Task PopulateAsync(TelephonyExtension extension, JsonNode data)
    {
        // The editor hands no data; only a recipe or deployment plan does.
        if (data is not JsonObject json || json.Count == 0)
        {
            return;
        }

        CatalogDeploymentSerializer.Populate(extension, json);

        extension.Number = extension.Number?.Trim();

        if (!string.IsNullOrWhiteSpace(extension.UserName))
        {
            var user = await _userManager.FindByNameAsync(extension.UserName);

            // An unknown user name clears the identifier rather than keeping one from another environment, so the entry
            // fails validation instead of ringing whoever happens to hold that identifier here.
            extension.UserId = user is null ? null : await _userManager.GetUserIdAsync(user);

            if (user is not null)
            {
                extension.UserName = await _userManager.GetUserNameAsync(user);
            }
        }

        if (string.IsNullOrWhiteSpace(extension.DisplayName))
        {
            extension.DisplayName = extension.UserName;
        }

        // The catalog name carries the number and person so the list search can match either, as the editor does.
        if (string.IsNullOrWhiteSpace(extension.Name))
        {
            extension.Name = $"{extension.Number} {extension.DisplayName}".Trim();
        }
    }
}
