using CrestApps.Core.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.SiteResources.Models;
using CrestApps.OrchardCore.SiteResources.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.SiteResources.Handlers;

/// <summary>
/// Validates a <see cref="SiteResource"/>, stamps it, and minifies and versions it every time it is saved, whether
/// from the admin, a recipe or another module.
/// </summary>
internal sealed class SiteResourceHandler : AuditedCatalogEntryHandlerBase<SiteResource>
{
    private readonly SiteResourceValidator _validator;
    private readonly ISiteResourceService _siteResourceService;
    private readonly ILogger _logger;

    public SiteResourceHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        SiteResourceValidator validator,
        ISiteResourceService siteResourceService,
        ILogger<SiteResourceHandler> logger)
        : base(httpContextAccessor, clock)
    {
        _validator = validator;
        _siteResourceService = siteResourceService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task ValidatingAsync(ValidatingContext<SiteResource> context, CancellationToken cancellationToken = default)
    {
        foreach (var error in await _validator.ValidateAsync(context.Model))
        {
            context.Result.Fail(error);
        }
    }

    /// <inheritdoc/>
    public override Task CreatingAsync(CreatingContext<SiteResource> context, CancellationToken cancellationToken = default)
    {
        Prepare(context.Model);

        return base.CreatingAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task UpdatingAsync(UpdatingContext<SiteResource> context, CancellationToken cancellationToken = default)
    {
        await base.UpdatingAsync(context, cancellationToken);

        Prepare(context.Model);
    }

    private void Prepare(SiteResource resource)
    {
        resource.Name = resource.Name?.Trim();
        resource.Dependencies = (resource.Dependencies ?? [])
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency))
            .Select(dependency => dependency.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        resource.Themes = (resource.Themes ?? [])
            .Where(theme => !string.IsNullOrWhiteSpace(theme))
            .Select(theme => theme.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var errors = _siteResourceService.Prepare(resource);

        if (errors.Count > 0)
        {
            _logger.LogWarning("The site resource '{Name}' could not be minified and is served as it was written: {Errors}", resource.Name.SanitizeLogValue(), string.Join("; ", errors));
        }
    }
}
