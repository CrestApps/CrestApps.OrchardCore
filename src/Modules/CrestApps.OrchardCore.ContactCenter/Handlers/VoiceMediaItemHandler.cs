using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Deployments;
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
    public override Task InitializingAsync(InitializingContext<VoiceMediaItem> context, CancellationToken cancellationToken = default)
    {
        // A recipe or deployment plan hands the entry's JSON to the manager; the editor hands none.
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

        return Task.CompletedTask;
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
        ContactCenterDeploymentSerializer.Populate(context.Model, context.Data);

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
        // Best-effort, deferred cleanup so a deleted clip leaves neither its master copy nor a provider's copy behind.
        // It runs after the request's unit of work and checks the entry is gone first, so a delete that did not
        // commit never removes audio an entry still relies on.
        VoiceMediaItemCompatibility.FoldLegacyReference(context.Model);

        var copies = new VoiceMediaAsset
        {
            MasterStorageReference = context.Model.MasterStorageReference,
            ContentType = context.Model.ContentType,
            Format = context.Model.Format,
            ProviderReferences = new Dictionary<string, string>(context.Model.ProviderReferences),
        };

        if (!string.IsNullOrWhiteSpace(copies.MasterStorageReference) || copies.ProviderReferences.Count > 0)
        {
            var itemId = context.Model.ItemId;

            ShellScope.AddDeferredTask(scope => DeleteCopiesAsync(scope, itemId, copies));
        }

        return Task.CompletedTask;
    }

    private static async Task DeleteCopiesAsync(ShellScope scope, string itemId, VoiceMediaAsset copies)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VoiceMediaItemHandler>>();

        try
        {
            if (!string.IsNullOrEmpty(itemId))
            {
                var store = scope.ServiceProvider.GetRequiredService<IVoiceMediaItemStore>();

                if (await store.FindByIdAsync(itemId) is not null)
                {
                    logger.LogWarning("The voice media entry {ItemId} is still stored, so its audio was not deleted.", itemId.SanitizeLogValue());

                    return;
                }
            }

            var provisioning = scope.ServiceProvider.GetRequiredService<IVoiceMediaProvisioningService>();

            await provisioning.DeleteEverywhereAsync(copies);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete the audio of the removed voice media entry {ItemId}.", itemId.SanitizeLogValue());
        }
    }
}
