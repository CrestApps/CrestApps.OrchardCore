using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Stamps audit times and enforces the name rules for voice media library entries. It depends on the store rather
/// than the manager so it cannot recurse into the manager that owns it while a name is being checked for uniqueness.
/// </summary>
internal sealed class VoiceMediaItemHandler : CatalogEntryHandlerBase<VoiceMediaItem>
{
    private readonly IVoiceMediaItemStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceMediaItemHandler"/> class.
    /// </summary>
    /// <param name="store">The media library store used to check name uniqueness.</param>
    /// <param name="clock">The clock used to stamp audit times.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public VoiceMediaItemHandler(
        IVoiceMediaItemStore store,
        IClock clock,
        IStringLocalizer<VoiceMediaItemHandler> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializedAsync(InitializedContext<VoiceMediaItem> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<VoiceMediaItem> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<VoiceMediaItem> context, CancellationToken cancellationToken = default)
    {
        var name = context.Model.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(VoiceMediaItem.Name)]));

            return;
        }

        var existing = await _store.FindByNameAsync(name, cancellationToken);

        if (existing is not null && !string.Equals(existing.ItemId, context.Model.ItemId, StringComparison.Ordinal))
        {
            context.Result.Fail(new ValidationResult(S["A voice media clip named '{0}' already exists.", name], [nameof(VoiceMediaItem.Name)]));
        }
    }

    /// <inheritdoc/>
    public override Task DeletingAsync(DeletingContext<VoiceMediaItem> context, CancellationToken cancellationToken = default)
    {
        // Best-effort, deferred cleanup so a deleted clip does not leave orphaned media in the provider's storage.
        // Running after the entry is deleted keeps a failed delete from removing media a still-present clip relies on.
        var reference = context.Model.MediaReference;

        if (!string.IsNullOrWhiteSpace(reference))
        {
            ShellScope.AddDeferredTask(scope => DeleteProviderMediaAsync(scope, reference));
        }

        return Task.CompletedTask;
    }

    private static async Task DeleteProviderMediaAsync(ShellScope scope, string mediaReference)
    {
        var provisioner = scope.ServiceProvider.GetService<IVoiceMediaProvisioner>();

        if (provisioner is null)
        {
            return;
        }

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VoiceMediaItemHandler>>();

        try
        {
            await provisioner.DeleteAsync(mediaReference);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete voice media {MediaReference} from the provider after the clip was removed.", mediaReference.SanitizeLogValue());
        }
    }
}
