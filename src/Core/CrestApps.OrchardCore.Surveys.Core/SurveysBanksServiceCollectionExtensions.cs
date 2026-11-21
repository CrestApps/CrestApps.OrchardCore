using CrestApps.Core.Services;
using CrestApps.OrchardCore.Surveys.Core.Banks;
using CrestApps.OrchardCore.Surveys.Core.Imports;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CrestApps.OrchardCore.Surveys.Core;

/// <summary>
/// Registers question banks and the spreadsheet import format.
/// </summary>
public static class SurveysBanksServiceCollectionExtensions
{
    /// <summary>
    /// Registers the bank manager, queries, change detector, audit handlers and the CSV and Excel import formats.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddSurveysBanks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.TryAddScoped<QuestionBankCatalogManager>();
        services.TryAddScoped<BankQuestionCatalogManager>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<QuestionBank>, QuestionBankAuditHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ICatalogEntryHandler<BankQuestion>, BankQuestionAuditHandler>());
        services.TryAddScoped<IQuestionBankManager, QuestionBankManager>();
        services.TryAddScoped<IQuestionBankQueries, QuestionBankQueries>();
        services.TryAddScoped<IQuestionBankChangeDetector, QuestionBankChangeDetector>();
        services.TryAddScoped<SpreadsheetQuestionReader>();
        services.AddSurveyImportFormat<CsvQuestionImportFormat>();
        services.AddSurveyImportFormat<ExcelQuestionImportFormat>();

        return services;
    }
}
