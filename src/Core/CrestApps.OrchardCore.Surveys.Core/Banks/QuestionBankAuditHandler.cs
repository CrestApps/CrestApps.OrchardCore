using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys.Core.Banks;

/// <summary>
/// Stamps when and by whom a question bank was created and changed.
/// </summary>
public sealed class QuestionBankAuditHandler : AuditedCatalogEntryHandlerBase<QuestionBank>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionBankAuditHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="clock">The clock.</param>
    public QuestionBankAuditHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock)
        : base(httpContextAccessor, clock)
    {
    }
}
