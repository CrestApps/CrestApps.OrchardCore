using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Drivers;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers question banks, their admin pages and the spreadsheet import formats.
/// </summary>
public sealed class BanksStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSurveysBanks();
        services.AddDisplayDriver<QuestionBank, QuestionBankDisplayDriver>();
        services.AddDisplayDriver<BankQuestion, BankQuestionDisplayDriver>();
    }
}
