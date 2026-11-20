using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// Moves the service agreements Work Orders kept onto the Agreements module: each becomes an agreement with the same id
/// and number, its visits, address, contact, assets and pricing kept as its Work Orders terms. Work orders already
/// name the agreement by that id, so nothing else changes.
/// </summary>
public sealed class ServiceAgreementUpgrade
{
    // The name the agreements were stored under before the Work Orders model became the legacy one.
    internal const string StoredTypeName = "CrestApps.OrchardCore.WorkOrders.Models.ServiceAgreement, CrestApps.OrchardCore.WorkOrders.Abstractions";

    private readonly ISession _session;
    private readonly IAgreementManager _agreements;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAgreementUpgrade"/> class.
    /// </summary>
    public ServiceAgreementUpgrade(ISession session, IAgreementManager agreements, ILogger<ServiceAgreementUpgrade> logger)
    {
        _session = session;
        _agreements = agreements;
        _logger = logger;
    }

    /// <summary>
    /// Copies every service agreement that hasn't moved yet, as it was stored: the move doesn't check it again, since a
    /// rule added since it was saved must not lose it. Running it again does nothing more. An agreement whose copy fails
    /// is logged and left for the next run.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many moved now, how many there were to move, and how many failed.</returns>
    public async Task<(int Moved, int Total, int Failed)> RunAsync(CancellationToken cancellationToken = default)
    {
        var legacies = await ListLegacyAsync(cancellationToken);
        var moved = 0;
        var failed = 0;

        foreach (var legacy in legacies)
        {
            try
            {
                if (await _agreements.FindByIdAsync(legacy.ItemId, cancellationToken) is null)
                {
                    await _agreements.CreateAsync(await ToAgreementAsync(legacy, cancellationToken), cancellationToken);
                    moved++;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                _logger.LogError(exception, "The service agreement {AgreementId} couldn't move to the Agreements module; it is tried again on the next run.", legacy.ItemId);
            }
        }

        return (moved, legacies.Count, failed);
    }

    /// <summary>
    /// Turns an older recipe's service agreement (the <c>Agreements</c> section of the WorkOrders step) into an
    /// agreement, to create or update by id.
    /// </summary>
    /// <param name="node">The recipe's agreement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The agreement, new or changed, and whether it is new; <see langword="null"/> when the node isn't one.</returns>
    public async Task<(Agreement Agreement, bool IsNew)?> FromRecipeAsync(JsonObject node, CancellationToken cancellationToken = default)
    {
        var legacy = node?.Deserialize<LegacyServiceAgreement>(JOptions.Default);

        if (legacy is null || string.IsNullOrWhiteSpace(legacy.Name))
        {
            return null;
        }

        var existing = string.IsNullOrEmpty(legacy.ItemId) ? null : await _agreements.FindByIdAsync(legacy.ItemId, cancellationToken);

        return (await ToAgreementAsync(legacy, cancellationToken, existing), existing is null);
    }

    private async Task<Agreement> ToAgreementAsync(LegacyServiceAgreement legacy, CancellationToken cancellationToken, Agreement agreement = null)
    {
        if (agreement is null)
        {
            agreement = await _agreements.NewAsync(cancellationToken: cancellationToken);

            if (!string.IsNullOrEmpty(legacy.ItemId) && legacy.ItemId.Length <= 26)
            {
                agreement.ItemId = legacy.ItemId;
            }
        }

        agreement.Number = string.IsNullOrWhiteSpace(legacy.Number) ? agreement.Number : legacy.Number;
        agreement.Name = legacy.Name;
        agreement.Status = Enum.TryParse<AgreementStatus>(legacy.Status, ignoreCase: true, out var status) ? status : AgreementStatus.Draft;
        agreement.AccountId = legacy.AccountId;
        agreement.StartDate = legacy.StartDate == default ? agreement.StartDate : legacy.StartDate;
        agreement.EndDate = legacy.EndDate;
        agreement.TimeZoneId = legacy.TimeZoneId;
        agreement.ExpiringNoticeDays = legacy.ExpiringNoticeDays;
        agreement.ExpiringNotifiedUtc = legacy.ExpiringNotifiedUtc;
        agreement.Notes = legacy.Notes;

        if (legacy.CreatedUtc != default)
        {
            agreement.CreatedUtc = legacy.CreatedUtc;
        }

        agreement.Put(new WorkOrderAgreementTerms
        {
            CustomerId = legacy.CustomerId,
            Contact = legacy.Contact ?? new WorkOrderContact(),
            ServiceAddress = legacy.ServiceAddress,
            AccessNotes = legacy.AccessNotes,
            AssetIds = [.. legacy.AssetIds ?? []],
            Schedules = [.. legacy.Schedules ?? []],
            Pricing = legacy.Pricing,
            PricePerVisit = legacy.PricePerVisit,
        });

        return agreement;
    }

    private async Task<IReadOnlyList<LegacyServiceAgreement>> ListLegacyAsync(CancellationToken cancellationToken)
    {
        // The agreements are documents of the Work Orders collection stored under the old class name, which no longer
        // exists: that name is mapped to the legacy model, which only this upgrade reads, and the documents are read by
        // it. The stored rows are left as they are.
        _session.Store.TypeNames[typeof(LegacyServiceAgreement)] = StoredTypeName;

        var agreements = await _session.Query<LegacyServiceAgreement>(WorkOrdersConstants.CollectionName).ListAsync(cancellationToken);

        return [.. agreements.Where(agreement => agreement is not null && !string.IsNullOrEmpty(agreement.ItemId))];
    }
}
