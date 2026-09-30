using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Seeds the lead statuses and opportunity stages a new CRM starts with. Each catalog is seeded only while it is
/// empty, so an administrator's own statuses and stages are never touched.
/// </summary>
internal sealed class CrmCatalogSeeder
{
    private readonly INamedCatalogManager<LeadStatus> _leadStatusManager;
    private readonly INamedCatalogManager<OpportunityStage> _opportunityStageManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrmCatalogSeeder"/> class.
    /// </summary>
    /// <param name="leadStatusManager">The lead status manager.</param>
    /// <param name="opportunityStageManager">The opportunity stage manager.</param>
    /// <param name="logger">The logger.</param>
    public CrmCatalogSeeder(
        INamedCatalogManager<LeadStatus> leadStatusManager,
        INamedCatalogManager<OpportunityStage> opportunityStageManager,
        ILogger<CrmCatalogSeeder> logger)
    {
        _leadStatusManager = leadStatusManager;
        _opportunityStageManager = opportunityStageManager;
        _logger = logger;
    }

    /// <summary>
    /// Seeds whichever catalog is still empty.
    /// </summary>
    public async Task SeedAsync()
    {
        if (!(await _leadStatusManager.GetAllAsync()).Any())
        {
            await CreateLeadStatusAsync("Open - Not Contacted", "A new lead nobody has reached yet.", 10, isDefault: true);
            await CreateLeadStatusAsync("Working - Contacted", "Someone has reached, or tried to reach, the lead.", 20);
            await CreateLeadStatusAsync("Nurturing", "The lead is not ready yet and is followed up over time.", 30);
            await CreateLeadStatusAsync("Closed - Not Converted", "The lead will not become a customer, for example a wrong number or no interest.", 40, isClosed: true);
            await CreateLeadStatusAsync("Converted", "The lead became a contact. A lead takes this status when it is converted.", 50, isClosed: true, isConverted: true);

            _logger.LogInformation("Seeded the default lead statuses.");
        }

        if (!(await _opportunityStageManager.GetAllAsync()).Any())
        {
            await CreateOpportunityStageAsync("Prospecting", 10, 10);
            await CreateOpportunityStageAsync("Qualification", 20, 20);
            await CreateOpportunityStageAsync("Proposal", 30, 50);
            await CreateOpportunityStageAsync("Negotiation", 40, 75);
            await CreateOpportunityStageAsync("Closed Won", 50, 100, isClosed: true, isWon: true);
            await CreateOpportunityStageAsync("Closed Lost", 60, 0, isClosed: true);

            _logger.LogInformation("Seeded the default opportunity stages.");
        }
    }

    private async Task CreateLeadStatusAsync(string name, string description, int order, bool isDefault = false, bool isClosed = false, bool isConverted = false)
    {
        var status = await _leadStatusManager.NewAsync();

        status.Name = name;
        status.Description = description;
        status.Order = order;
        status.IsDefault = isDefault;
        status.IsClosed = isClosed;
        status.IsConverted = isConverted;

        await _leadStatusManager.CreateAsync(status);
    }

    private async Task CreateOpportunityStageAsync(string name, int order, int probability, bool isClosed = false, bool isWon = false)
    {
        var stage = await _opportunityStageManager.NewAsync();

        stage.Name = name;
        stage.Order = order;
        stage.Probability = probability;
        stage.IsClosed = isClosed;
        stage.IsWon = isWon;

        await _opportunityStageManager.CreateAsync(stage);
    }
}
