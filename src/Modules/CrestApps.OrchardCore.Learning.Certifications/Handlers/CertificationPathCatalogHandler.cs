using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Skills.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Learning.Certifications.Handlers;

/// <summary>
/// Saves the certification path the editor posted once its certification is created or updated.
/// </summary>
internal sealed class CertificationPathCatalogHandler : CatalogEntryHandlerBase<Certification>
{
    private readonly PendingCertificationPath _pending;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    // The path service is resolved when needed: it reads certifications, so it depends on the certification manager,
    // whose handlers include this one.
    public CertificationPathCatalogHandler(PendingCertificationPath pending, IServiceProvider services, ILogger<CertificationPathCatalogHandler> logger)
    {
        _pending = pending;
        _services = services;
        _logger = logger;
    }

    public override Task CreatedAsync(CreatedContext<Certification> context, CancellationToken cancellationToken = default)
        => SaveAsync(context.Model, cancellationToken);

    public override Task UpdatedAsync(UpdatedContext<Certification> context, CancellationToken cancellationToken = default)
        => SaveAsync(context.Model, cancellationToken);

    private async Task SaveAsync(Certification certification, CancellationToken cancellationToken)
    {
        var path = _pending.Path;

        if (path is null)
        {
            return;
        }

        _pending.Path = null;
        path.CertificationId = certification.ItemId;

        var result = await _services.GetRequiredService<ICertificationPathService>().SaveAsync(path, cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("The certification path of '{CertificationId}' was not saved: {Errors}", certification.ItemId, string.Join(" ", result.Errors));
        }
    }
}
