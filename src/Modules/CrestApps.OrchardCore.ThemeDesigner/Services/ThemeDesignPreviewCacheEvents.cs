using CrestApps.OrchardCore.ThemeDesigner.Storage;
using OrchardCore.DisplayManagement.Implementation;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// Keeps the shapes a designer renders while previewing their draft out of everyone else's cache. A cached shape gets
/// the viewer as a cache context, so it is cached for the designer alone, and the draft's tag, so it is dropped
/// whenever the draft changes. It runs before Dynamic Cache looks a shape up.
/// </summary>
internal sealed class ThemeDesignPreviewCacheEvents : IShapeDisplayEvents
{
    private readonly ThemeDesignPreview _preview;

    public ThemeDesignPreviewCacheEvents(ThemeDesignPreview preview)
    {
        _preview = preview;
    }

    public async Task DisplayingAsync(ShapeDisplayContext context)
    {
        if (!context.Shape.Metadata.IsCached)
        {
            return;
        }

        var draft = await _preview.GetDraftAsync();

        if (draft is null)
        {
            return;
        }

        context.Shape.Metadata.Cache()
            .AddContext("user")
            .AddTag(ThemeDesignDraftStore.GetCacheTag(draft.UserId));
    }

    public Task DisplayedAsync(ShapeDisplayContext context)
        => Task.CompletedTask;

    public Task DisplayingFinalizedAsync(ShapeDisplayContext context)
        => Task.CompletedTask;
}
