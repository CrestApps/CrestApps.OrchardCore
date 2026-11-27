using System.Collections.Concurrent;
using CrestApps.OrchardCore.ThemeDesigner.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Implementation;
using OrchardCore.DisplayManagement.Theming;
using OrchardCore.Documents;
using OrchardCore.Templates.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// Records, for a page the previewer requests with a trace id, which template rendered each shape, so the designer
/// learns the name a template must take to replace a part of the page. Other requests are left alone.
/// </summary>
internal sealed class ThemeDesignShapeTraceEvents : IShapeDisplayEvents
{
    /// <summary>
    /// The request header that carries the trace id.
    /// </summary>
    public const string TraceHeader = "X-Theme-Designer-Trace";

    /// <summary>
    /// The most templates recorded for one page.
    /// </summary>
    public const int MaxEntries = 80;

    private static readonly TimeSpan _traceLifetime = TimeSpan.FromMinutes(1);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _memoryCache;
    private readonly ThemeDesignPreview _preview;
    private readonly IThemeManager _themeManager;
    private readonly IShapeTableManager _shapeTableManager;
    private readonly IDocumentManager<TemplatesDocument> _templatesManager;
    private readonly IDocumentManager<AdminTemplatesDocument> _adminTemplatesManager;

    private bool _resolved;
    private string _traceKey;
    private ShapeTable _shapeTable;
    private TemplatesDocument _published;
    private ThemeDesignDraft _draft;
    private ThemeDesignScope _scope;

    public ThemeDesignShapeTraceEvents(
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache memoryCache,
        ThemeDesignPreview preview,
        IThemeManager themeManager,
        IShapeTableManager shapeTableManager,
        IDocumentManager<TemplatesDocument> templatesManager,
        IDocumentManager<AdminTemplatesDocument> adminTemplatesManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _memoryCache = memoryCache;
        _preview = preview;
        _themeManager = themeManager;
        _shapeTableManager = shapeTableManager;
        _templatesManager = templatesManager;
        _adminTemplatesManager = adminTemplatesManager;
    }

    /// <summary>
    /// Gets the cache key the templates of a traced page are kept under.
    /// </summary>
    /// <param name="traceId">The trace id.</param>
    public static string GetCacheKey(string traceId)
        => "theme-designer-trace:" + traceId;

    public Task DisplayingAsync(ShapeDisplayContext context)
        => Task.CompletedTask;

    public async Task DisplayedAsync(ShapeDisplayContext context)
    {
        if (!await EnsureTracingAsync())
        {
            return;
        }

        var metadata = context.Shape.Metadata;

        if (string.IsNullOrEmpty(metadata.Type))
        {
            return;
        }

        // Orchard Core tries the most specific alternate first: the last one added, down to the shape type.
        var candidates = metadata.Alternates.Reverse().Append(metadata.Type).ToArray();

        for (var i = 0; i < candidates.Length; i++)
        {
            var source = GetTemplateSource(candidates[i]);

            if (source is null)
            {
                continue;
            }

            var entries = _memoryCache.GetOrCreate(_traceKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _traceLifetime;

                return new ConcurrentQueue<ThemeDraftRenderedTemplate>();
            });

            if (entries.Count < MaxEntries && !entries.Any(existing => string.Equals(existing.Name, candidates[i], StringComparison.OrdinalIgnoreCase)))
            {
                entries.Enqueue(new ThemeDraftRenderedTemplate
                {
                    Name = candidates[i],
                    Source = source,
                    MoreSpecificNames = candidates[..i].Take(4).ToArray(),
                });
            }

            return;
        }
    }

    public Task DisplayingFinalizedAsync(ShapeDisplayContext context)
        => Task.CompletedTask;

    // Only templates (files, published templates and the draft) are reported; shapes rendered by code, such as zones,
    // can't be replaced by a template of the same kind and would only add noise.
    private string GetTemplateSource(string name)
    {
        if (_draft?.FindTemplate(_scope, name) is { Removed: false })
        {
            return "theme draft";
        }

        if (_published.Templates.ContainsKey(name))
        {
            return "published template";
        }

        if (_shapeTable.Bindings.TryGetValue(name, out var binding) &&
            binding.BindingSource is { } bindingSource &&
            (bindingSource.EndsWith(".liquid", StringComparison.OrdinalIgnoreCase) || bindingSource.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)))
        {
            return bindingSource;
        }

        return null;
    }

    private async Task<bool> EnsureTracingAsync()
    {
        if (_resolved)
        {
            return _traceKey is not null;
        }

        _resolved = true;

        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.User.Identity?.IsAuthenticated != true ||
            !httpContext.Request.Headers.TryGetValue(TraceHeader, out var traceId) ||
            !Guid.TryParse(traceId, out var parsedId))
        {
            return false;
        }

        _scope = AdminAttribute.IsApplied(httpContext) ? ThemeDesignScope.Admin : ThemeDesignScope.Site;
        _draft = await _preview.GetDraftAsync();
        _published = _scope == ThemeDesignScope.Admin
            ? await _adminTemplatesManager.GetOrCreateImmutableAsync()
            : await _templatesManager.GetOrCreateImmutableAsync();
        _shapeTable = await _shapeTableManager.GetShapeTableAsync((await _themeManager.GetThemeAsync())?.Id);
        _traceKey = GetCacheKey(parsedId.ToString("N"));

        return true;
    }
}
