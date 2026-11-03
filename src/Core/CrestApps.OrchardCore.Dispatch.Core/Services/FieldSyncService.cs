using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using YesSql;
using YesSql.Services;
using IClock = OrchardCore.Modules.IClock;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Dispatch.Core.Services;

/// <summary>
/// Serves the technician app: a pack of the technician's visits to keep offline, and the commands it sends back,
/// each applied once, in order, with the schedule on the server winning.
/// </summary>
public sealed partial class FieldSyncService : IFieldSyncService
{
    // The statuses a technician moves a visit to; scheduling and canceling stay with the dispatcher.
    private static readonly VisitStatus[] _technicianStatuses = [VisitStatus.EnRoute, VisitStatus.OnSite, VisitStatus.Paused, VisitStatus.Completed, VisitStatus.Incomplete];

    private static readonly Dictionary<string, string> _imageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    private readonly IDispatchService _dispatchService;
    private readonly WorkOrderService _workOrderService;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly IWorkerManager _workerManager;
    private readonly IDispatchSettingsProvider _settingsProvider;
    private readonly IWorkerLocationService _locationService;
    private readonly FieldCommandReceiptStore _receipts;
    private readonly IEnumerable<IFieldStockSource> _stockSources;
    private readonly IEnumerable<IFieldAssetSource> _assetSources;
    private readonly IEnumerable<IFieldFileStore> _fileStores;
    private readonly ISession _session;
    private readonly DispatchMobileOptions _options;
    private readonly IClock _clock;

    // The most earlier work orders shown for a work order's assets.
    private const int MaxHistory = 5;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldSyncService"/> class.
    /// </summary>
    /// <param name="dispatchService">The dispatch service.</param>
    /// <param name="workOrderService">The work order service.</param>
    /// <param name="types">The work order types.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="settingsProvider">The dispatch settings.</param>
    /// <param name="locationService">The location service.</param>
    /// <param name="receipts">The records of commands.</param>
    /// <param name="stockSources">The stock locations, when Inventory is on.</param>
    /// <param name="fileStores">Where photos and signatures are kept.</param>
    /// <param name="options">The technician app's settings.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FieldSyncService(
        IDispatchService dispatchService,
        WorkOrderService workOrderService,
        WorkOrderSettingManager<WorkOrderType> types,
        IWorkerManager workerManager,
        IDispatchSettingsProvider settingsProvider,
        IWorkerLocationService locationService,
        FieldCommandReceiptStore receipts,
        IEnumerable<IFieldStockSource> stockSources,
        IEnumerable<IFieldAssetSource> assetSources,
        IEnumerable<IFieldFileStore> fileStores,
        ISession session,
        IOptions<DispatchMobileOptions> options,
        IClock clock,
        IStringLocalizer<FieldSyncService> stringLocalizer)
    {
        _dispatchService = dispatchService;
        _workOrderService = workOrderService;
        _types = types;
        _workerManager = workerManager;
        _settingsProvider = settingsProvider;
        _locationService = locationService;
        _receipts = receipts;
        _stockSources = stockSources;
        _assetSources = assetSources;
        _fileStores = fileStores;
        _session = session;
        _options = options.Value;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Tells whether a technician may move a visit from one status to another.
    /// </summary>
    /// <param name="from">The status it is in.</param>
    /// <param name="to">The status to move to.</param>
    /// <returns>Whether they may.</returns>
    public static bool TechnicianCanMove(VisitStatus from, VisitStatus to)
        => _technicianStatuses.Contains(to) && VisitStatusRules.CanMove(from, to);

    /// <inheritdoc/>
    public async Task<FieldPack> GetPackAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var worker = string.IsNullOrEmpty(workerId) ? null : await _workerManager.FindByIdAsync(workerId, cancellationToken);

        if (worker is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var timeZoneId = await _settingsProvider.GetTimeZoneIdAsync();

        timeZoneId = CalendarTime.IsValidZone(timeZoneId) ? timeZoneId : "UTC";

        var today = CalendarTime.ToUtc(CalendarTime.ToLocal(now, timeZoneId).Date, timeZoneId);
        var end = CalendarTime.ToUtc(CalendarTime.ToLocal(now, timeZoneId).Date.AddDays(Math.Max(0, _options.OfflineDays) + 1), timeZoneId);

        // Yesterday's visits still under way come too, such as one paused overnight.
        var visits = (await _dispatchService.ListScheduledAsync(today.AddDays(-1), end, [workerId], cancellationToken))
            .Where(visit => visit.WorkerIds.Contains(workerId) && visit.Status != VisitStatus.Canceled && (visit.StartUtc >= today || VisitStatusRules.IsOpen(visit.Status)))
            .OrderBy(visit => visit.StartUtc)
            .ToList();

        var pack = new FieldPack
        {
            WorkerId = worker.ItemId,
            WorkerName = worker.DisplayName,
            GeneratedUtc = now,
            TimeZoneId = timeZoneId,
            Location = new FieldLocationSettings
            {
                Consented = await _locationService.HasConsentAsync(workerId, cancellationToken),
                NoticeVersion = _options.ConsentNoticeVersion,
                IntervalSeconds = Math.Max(10, _options.PingIntervalSeconds),
                RetentionDays = _options.LocationRetentionDays,
            },
        };

        foreach (var status in Enum.GetValues<VisitStatus>())
        {
            pack.NextStatuses[status.ToString()] = [.. _technicianStatuses.Where(to => TechnicianCanMove(status, to)).Select(to => to.ToString())];
        }

        var types = new Dictionary<string, WorkOrderType>(StringComparer.Ordinal);

        foreach (var visit in visits)
        {
            var workOrder = await _workOrderService.FindAsync(visit.WorkOrderId, cancellationToken);

            if (workOrder is null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(workOrder.TypeId) && !types.ContainsKey(workOrder.TypeId))
            {
                types[workOrder.TypeId] = await _types.FindByIdAsync(workOrder.TypeId, cancellationToken);
            }

            pack.Visits.Add(new FieldVisit
            {
                Id = visit.ItemId,
                Number = visit.Number,
                Status = visit.Status,
                StartUtc = visit.StartUtc,
                EndUtc = visit.EndUtc,
                ArrivalStartUtc = visit.ArrivalStartUtc,
                ArrivalEndUtc = visit.ArrivalEndUtc,
                IsLead = visit.LeadWorkerId == workerId,
                WorkOrder = ToField(workOrder, types.GetValueOrDefault(workOrder.TypeId ?? string.Empty)),
            });
        }

        var stockItemIds = pack.Visits.SelectMany(visit => visit.WorkOrder.Lines).Select(line => line.StockItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        foreach (var source in _stockSources)
        {
            var locations = await source.ListAsync(workerId, cancellationToken);

            // How many of the parts on the pack's lines each location has free, so the technician picks one that has them.
            if (stockItemIds.Length > 0 && locations.Count > 0)
            {
                var available = await source.AvailableAsync([.. locations.Select(location => location.Id)], stockItemIds, cancellationToken);

                foreach (var location in locations)
                {
                    if (available.TryGetValue(location.Id, out var units))
                    {
                        location.Available = units;
                    }
                }
            }

            pack.StockLocations.AddRange(locations);
        }

        pack.StockLocations = [.. pack.StockLocations.OrderByDescending(location => location.IsOwn)];

        await AddAssetsAsync(pack, cancellationToken);

        return pack;
    }

    // The assets each work order is on, and the latest other work orders on them, so the technician sees what was
    // done to the equipment before.
    private async Task AddAssetsAsync(FieldPack pack, CancellationToken cancellationToken)
    {
        var workOrderIds = pack.Visits.Select(visit => visit.WorkOrder.Id).ToHashSet(StringComparer.Ordinal);
        var assetIds = pack.Visits.SelectMany(visit => visit.WorkOrder.AssetIds).Distinct(StringComparer.Ordinal).ToArray();

        if (assetIds.Length == 0)
        {
            return;
        }

        var assets = new Dictionary<string, FieldAsset>(StringComparer.Ordinal);

        foreach (var source in _assetSources)
        {
            foreach (var asset in await source.GetAsync(assetIds, cancellationToken))
            {
                assets.TryAdd(asset.Id, asset);
            }
        }

        var rows = await _session.QueryIndex<WorkOrderAssetIndex>(index => index.AssetId.IsIn(assetIds), collection: WorkOrdersConstants.CollectionName).ListAsync(cancellationToken);
        var otherIds = rows.Select(row => row.WorkOrderId).Where(id => !workOrderIds.Contains(id)).Distinct(StringComparer.Ordinal).ToArray();
        var others = otherIds.Length == 0
            ? []
            : (await _session.Query<WorkOrder, WorkOrderIndex>(index => index.ItemId.IsIn(otherIds), collection: WorkOrdersConstants.CollectionName)
                .OrderByDescending(index => index.CreatedUtc)
                .Take(MaxHistory * 20)
                .ListAsync(cancellationToken)).ToList();

        foreach (var visit in pack.Visits)
        {
            var mine = visit.WorkOrder.AssetIds;

            visit.WorkOrder.Assets = [.. mine.Select(id => assets.GetValueOrDefault(id)).Where(asset => asset is not null)];
            visit.WorkOrder.History = [.. others
                .Where(other => other.AssetIds.Any(mine.Contains))
                .Take(MaxHistory)
                .Select(other => new FieldHistoryItem
                {
                    Number = other.Number,
                    Summary = other.Summary,
                    Status = other.Status,
                    CreatedUtc = other.CreatedUtc,
                    CompletedUtc = other.CompletedUtc,
                })];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<FieldCommandResult>> ApplyAsync(string workerId, IReadOnlyList<FieldCommand> commands, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commands);

        // The rest wait for the next sync; the app keeps them until it has their results.
        var batch = commands.Where(command => command is not null).Take(Math.Max(1, _options.MaxCommandsPerSync)).ToList();
        var keys = batch.Select(command => command.Key).Where(IsKey).Distinct(StringComparer.Ordinal).ToArray();
        var earlier = (await _receipts.FindAsync(workerId, keys, cancellationToken)).ToDictionary(receipt => receipt.Key, StringComparer.Ordinal);
        var results = new List<FieldCommandResult>(batch.Count);
        var now = _clock.UtcNow;

        foreach (var command in batch)
        {
            if (!IsKey(command.Key))
            {
                results.Add(Rejected(command, FieldRejections.Invalid, S["The change has no key, so it can't be sent safely."]));

                continue;
            }

            if (earlier.TryGetValue(command.Key, out var receipt))
            {
                results.Add(new FieldCommandResult { Key = command.Key, VisitId = receipt.VisitId, Outcome = FieldCommandOutcome.Duplicate, Code = receipt.Code, Reason = receipt.Reason });

                continue;
            }

            var result = await ApplyOneAsync(workerId, command, actor, cancellationToken);

            receipt = new FieldCommandReceipt
            {
                Key = command.Key,
                WorkerId = workerId,
                VisitId = command.VisitId,
                Kind = command.Kind,
                Outcome = result.Outcome,
                Code = result.Code,
                Reason = result.Reason,
                ClientUtc = command.Utc,
                ReceivedUtc = now,
            };

            await _receipts.CreateAsync(receipt, cancellationToken);
            earlier[command.Key] = receipt;
            results.Add(result);
        }

        return results;
    }

    private async Task<FieldCommandResult> ApplyOneAsync(string workerId, FieldCommand command, string actor, CancellationToken cancellationToken)
    {
        var visit = string.IsNullOrEmpty(command.VisitId) ? null : await _dispatchService.FindAsync(command.VisitId, cancellationToken);

        if (visit is null)
        {
            return Rejected(command, FieldRejections.Missing, S["The visit no longer exists."]);
        }

        // The schedule on the server wins: a visit given to someone else, or canceled, takes no more changes.
        if (!visit.WorkerIds.Contains(workerId))
        {
            return Rejected(command, FieldRejections.Moved, S["{0} was given to someone else.", visit.Number]);
        }

        if (visit.Status == VisitStatus.Canceled)
        {
            return Rejected(command, FieldRejections.Canceled, S["{0} was canceled.", visit.Number]);
        }

        if (command.Kind == FieldCommandKind.Answer && await StaleAnswerAsync(visit, command, cancellationToken) is { } stale)
        {
            return Rejected(command, FieldRejections.Stale, stale);
        }

        IReadOnlyList<string> errors = command.Kind switch
        {
            FieldCommandKind.Status => await StatusAsync(visit, command, actor, cancellationToken),
            FieldCommandKind.Answer => (await _workOrderService.AnswerAsync(visit.WorkOrderId, command.ChecklistIndex, command.QuestionId, command.Value, actor, command.Utc, cancellationToken)).Errors,
            FieldCommandKind.Note => (await _workOrderService.AddNoteAsync(visit.WorkOrderId, command.Text, actor, cancellationToken)).Errors,
            FieldCommandKind.Part => (await _workOrderService.UsePartAsync(visit.WorkOrderId, command.LineId, command.Quantity, command.LocationId, actor, cancellationToken)).Errors,
            FieldCommandKind.Signature or FieldCommandKind.Photo => await FileAsync(visit, command, actor, cancellationToken),
            FieldCommandKind.FollowUp => await FollowUpAsync(visit, command, actor, cancellationToken),
            _ => [S["The change isn't known."]],
        };

        return errors.Count > 0
            ? Rejected(command, FieldRejections.Invalid, string.Join(" ", errors))
            : new FieldCommandResult { Key = command.Key, VisitId = command.VisitId, Outcome = FieldCommandOutcome.Applied };
    }

    // A follow-up is a new work order for the same customer, place and assets; the note on this one says so.
    private async Task<IReadOnlyList<string>> FollowUpAsync(ServiceVisit visit, FieldCommand command, string actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Text))
        {
            return [S["Say what the follow-up is for."]];
        }

        var result = await _workOrderService.FollowUpAsync(visit.WorkOrderId, command.Text, actor, cancellationToken);

        if (result.Errors.Count > 0)
        {
            return result.Errors;
        }

        return (await _workOrderService.AddNoteAsync(visit.WorkOrderId, S["Follow-up {0}: {1}", result.WorkOrder.Number, result.WorkOrder.Summary], actor, cancellationToken)).Errors;
    }

    private async Task<IReadOnlyList<string>> StatusAsync(ServiceVisit visit, FieldCommand command, string actor, CancellationToken cancellationToken)
    {
        if (command.Status is not { } status)
        {
            return [S["Choose the status."]];
        }

        // Tapping the same status twice, such as offline, changes nothing.
        if (status == visit.Status)
        {
            return [];
        }

        if (!TechnicianCanMove(visit.Status, status))
        {
            return [S["{0} can't move from {1} to {2}.", visit.Number, visit.Status.Text(), status.Text()]];
        }

        // Finishing the visit completes the work order, once its required answers, signature and photos are in.
        if (status == VisitStatus.Completed)
        {
            var workOrder = await _workOrderService.FindAsync(visit.WorkOrderId, cancellationToken);

            if (workOrder is not null && WorkOrderStatusRules.CanMove(workOrder.Status, WorkOrderStatus.Completed))
            {
                var completed = await _workOrderService.CompleteAsync(workOrder.ItemId, actor, cancellationToken);

                if (completed.Errors.Count > 0)
                {
                    return completed.Errors;
                }
            }
        }

        return (await _dispatchService.ChangeStatusAsync(visit.ItemId, status, command.Text, actor, cancellationToken)).Errors;
    }

    private async Task<string> StaleAnswerAsync(ServiceVisit visit, FieldCommand command, CancellationToken cancellationToken)
    {
        var workOrder = await _workOrderService.FindAsync(visit.WorkOrderId, cancellationToken);
        var checklist = workOrder is not null && command.ChecklistIndex >= 0 && command.ChecklistIndex < workOrder.Checklists.Count ? workOrder.Checklists[command.ChecklistIndex] : null;
        var answer = checklist?.Answers.FirstOrDefault(item => item.QuestionId == command.QuestionId);

        // Answers merge per question: the later one wins, so an answer given after this one stays.
        if (answer is null || answer.Utc <= command.Utc)
        {
            return null;
        }

        var question = checklist.Questions.FirstOrDefault(item => item.Id == command.QuestionId);

        return S["\"{0}\" was answered again after this, so the later answer is kept.", question?.Text ?? command.QuestionId];
    }

    private async Task<IReadOnlyList<string>> FileAsync(ServiceVisit visit, FieldCommand command, string actor, CancellationToken cancellationToken)
    {
        var store = _fileStores.FirstOrDefault();

        if (store is null)
        {
            return [S["Photos and signatures can't be kept on this site."]];
        }

        var match = string.IsNullOrEmpty(command.Data) ? null : DataUrl().Match(command.Data);

        if (match is not { Success: true } || !_imageTypes.TryGetValue(match.Groups["type"].Value, out var extension))
        {
            if (command.Kind == FieldCommandKind.Signature && string.IsNullOrEmpty(command.Data))
            {
                // A typed name with no drawing is still a signature.
                return (await _workOrderService.SignAsync(visit.WorkOrderId, command.Name, null, actor, cancellationToken)).Errors;
            }

            return [S["The picture can't be read; take it again."]];
        }

        byte[] bytes;

        try
        {
            bytes = Convert.FromBase64String(match.Groups["data"].Value);
        }
        catch (FormatException)
        {
            return [S["The picture can't be read; take it again."]];
        }

        if (bytes.Length > _options.MaxFileKilobytes * 1024L)
        {
            return [S["The picture is larger than {0} KB.", _options.MaxFileKilobytes]];
        }

        var workOrder = await _workOrderService.FindAsync(visit.WorkOrderId, cancellationToken);

        if (workOrder is null)
        {
            return [S["The work order doesn't exist."]];
        }

        // The command's key names the file, so sending it again writes the same file.
        var kind = command.Kind == FieldCommandKind.Signature ? "signature" : "photo";
        var name = string.Concat(command.Key.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
        var folder = string.Concat((workOrder.Number ?? workOrder.ItemId).Where(c => char.IsAsciiLetterOrDigit(c) || c == '-'));

        await using var content = new MemoryStream(bytes);

        var path = await store.SaveAsync($"work-orders/{folder}/{kind}-{name}{extension}", content, cancellationToken);

        return command.Kind == FieldCommandKind.Signature
            ? (await _workOrderService.SignAsync(workOrder.ItemId, command.Name, path, actor, cancellationToken)).Errors
            : (await _workOrderService.AttachAsync(workOrder.ItemId, path, actor, cancellationToken)).Errors;
    }

    private static FieldWorkOrder ToField(WorkOrder workOrder, WorkOrderType type)
    {
        var address = workOrder.ServiceAddress;

        return new FieldWorkOrder
        {
            Id = workOrder.ItemId,
            Number = workOrder.Number,
            Summary = workOrder.Summary,
            Description = workOrder.Description,
            Priority = workOrder.Priority,
            Status = workOrder.Status,
            TypeName = type?.Name,
            Address = address is null ? null : string.Join(", ", new[] { address.AddressLine1, address.AddressLine2, address.City, string.Join(' ', new[] { address.Region, address.PostalCode }.Where(part => !string.IsNullOrWhiteSpace(part))) }.Where(part => !string.IsNullOrWhiteSpace(part))),
            Latitude = address?.Latitude,
            Longitude = address?.Longitude,
            AccessNotes = workOrder.AccessNotes,
            Contact = workOrder.Contact ?? new(),
            Lines = [.. workOrder.Lines.Select(line => new FieldLine
            {
                Id = line.Id,
                Kind = line.Kind,
                Description = line.Description,
                QuantityEstimated = line.QuantityEstimated,
                QuantityUsed = line.QuantityUsed,
                IsStocked = line.Kind == WorkOrderLineKind.Product && !string.IsNullOrEmpty(line.StockItemId),
                StockItemId = line.Kind == WorkOrderLineKind.Product ? line.StockItemId : null,
            })],
            Checklists = workOrder.Checklists,
            Notes = workOrder.Notes,
            SignedBy = [.. workOrder.Signatures.Select(signature => signature.Name)],
            Photos = workOrder.Attachments.Count,
            RequiresSignature = type?.RequiresSignature == true,
            RequiredPhotos = type?.RequiredPhotos ?? 0,
            AssetIds = [.. workOrder.AssetIds],
        };
    }

    private static bool IsKey(string key) => !string.IsNullOrWhiteSpace(key) && key.Length <= 64;

    private static FieldCommandResult Rejected(FieldCommand command, string code, string reason)
        => new() { Key = command.Key, VisitId = command.VisitId, Outcome = FieldCommandOutcome.Rejected, Code = code, Reason = reason };

    [GeneratedRegex(@"^data:(?<type>[\w/+.-]+);base64,(?<data>.+)$", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex DataUrl();
}
