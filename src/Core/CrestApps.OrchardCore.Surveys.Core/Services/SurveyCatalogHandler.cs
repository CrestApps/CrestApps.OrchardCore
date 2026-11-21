using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Stamps who created and changed a survey, and when.
/// </summary>
public sealed class SurveyCatalogHandler : AuditedCatalogEntryHandlerBase<Survey>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyCatalogHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="clock">The clock.</param>
    public SurveyCatalogHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock)
        : base(httpContextAccessor, clock)
    {
    }
}
