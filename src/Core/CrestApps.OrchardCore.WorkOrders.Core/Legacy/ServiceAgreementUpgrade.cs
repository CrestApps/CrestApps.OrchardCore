using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAgreementUpgrade"/> class.
    /// </summary>
    public ServiceAgreementUpgrade(ISession session, IAgreementManager agreements)
    {
        _session = session;
        _agreements = agreements;
    }

    /// <summary>
    /// Copies every service agreement that hasn't moved yet. Running it again does nothing more.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many moved.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var moved = 0;

        foreach (var legacy in await ListLegacyAsync(cancellationToken))
        {
            if (await _agreements.FindByIdAsync(legacy.ItemId, cancellationToken) is null)
            {
                await _agreements.CreateAsync(await ToAgreementAsync(legacy, cancellationToken), cancellationToken);
                moved++;
            }
        }

        return moved;
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
        IEnumerable<ServiceAgreementIndex> rows;

        try
        {
            rows = await _session.QueryIndex<ServiceAgreementIndex>(collection: WorkOrdersConstants.CollectionName).ListAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A tenant that never had service agreements has no table to read.
            return [];
        }

        var documentIds = rows.Select(row => row.DocumentId).Distinct().ToArray();

        if (documentIds.Length == 0)
        {
            return [];
        }

        // The agreements were stored under the old class name, which no longer exists: read that name as the legacy
        // model. Only the reading is mapped; the stored rows are left as they are.
        _session.Store.TypeNames[typeof(LegacyServiceAgreement)] = StoredTypeName;

        var agreements = await _session.GetAsync<LegacyServiceAgreement>(documentIds, WorkOrdersConstants.CollectionName, cancellationToken);

        return [.. agreements.Where(agreement => agreement is not null)];
    }
}
