using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PointOfSale.AI;

/// <summary>
/// Lists register shifts as Commerce › Point of sale › Shifts does (<see cref="IRegisterShiftStore.PageAsync"/>,
/// optionally for one register), or returns one shift as its page shows it: the cash movements, the takings
/// (<see cref="PosShiftService.SummarizeAsync"/> while it is open, the summary kept at closing otherwise) and the
/// expected against the counted cash, and the tips the cashier declared at the close, with whether payroll took them (so
/// they can no longer change). It needs <see cref="PosPermissions.ManageRegisters"/>, as those pages, and refuses
/// a caller who is not signed in. It changes nothing.
/// </summary>
internal sealed class ListRegisterShiftsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listRegisterShifts";

    /// <summary>
    /// The answer to a caller who is not signed in or lacks the permission.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    private const int DefaultPageSize = 20;

    private const int MaxPageSize = 50;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly HashSet<string> _argumentNames = new(StringComparer.Ordinal) { "registerId", "shiftId", "page", "pageSize" };

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "registerId": {
          "type": "string",
          "description": "Only the shifts of this register, from listRecords with the posRegister kind."
        },
        "shiftId": {
          "type": "string",
          "description": "One shift to return in full, with its cash movements, takings and cash difference."
        },
        "page": {
          "type": "integer",
          "description": "The page, from 1."
        },
        "pageSize": {
          "type": "integer",
          "description": "Shifts per page, 20 by default, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists register shifts, optionally for one register, with who opened and closed them, when, and the opening float; or returns one shift in full with its cash movements, sales count and totals by cash, card and other, refunds, tax, totals by department, expected against counted cash, and the card and cash tips the cashier declared (and whether payroll took them). Use it to check a till's takings or a cash difference.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var user = services.GetService<IUserAccessor>()?.User ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;
        var authorizationService = services.GetService<IAuthorizationService>();

        if (user?.Identity?.IsAuthenticated != true || authorizationService is null || !await authorizationService.AuthorizeAsync(user, PosPermissions.ManageRegisters))
        {
            return NotAllowedMessage;
        }

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return $"Nothing was done: '{unknown}' is not an argument of {TheName}. Use only the arguments the tool declares.";
        }

        var store = services.GetRequiredService<IRegisterShiftStore>();

        if (ReadString(arguments, "shiftId") is { } shiftId)
        {
            if (await store.FindByIdAsync(shiftId, cancellationToken) is not { } shift)
            {
                return $"Nothing was found: no shift has the id '{shiftId}'. List the shifts first.";
            }

            return await DescribeInFullAsync(services, shift, cancellationToken);
        }

        var page = Math.Max(1, ReadInt(arguments, "page") ?? 1);
        var pageSize = Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize);
        var (count, shifts) = await store.PageAsync(ReadString(arguments, "registerId"), (page - 1) * pageSize, pageSize, cancellationToken);
        var items = new List<object>();

        foreach (var shift in shifts)
        {
            items.Add(new
            {
                id = shift.ItemId,
                registerId = shift.RegisterId,
                registerName = shift.RegisterName,
                status = shift.Status == RegisterShiftStatus.Open ? "open" : "closed",
                openedBy = shift.OpenedByName,
                openedAt = await LocalTimeAsync(services, shift.OpenedUtc),
                openingFloat = shift.OpeningFloat,
                closedBy = shift.ClosedByName,
                closedAt = await LocalTimeAsync(services, shift.ClosedUtc),
                countedCash = shift.CountedCash,
                expectedCash = shift.ExpectedCash,
                currency = shift.Currency,
            });
        }

        return JsonSerializer.Serialize(new
        {
            items,
            total = count,
            page,
            pageSize,
            truncated = page * pageSize < count,
        }, _jsonOptions);
    }

    private static async Task<string> DescribeInFullAsync(IServiceProvider services, RegisterShift shift, CancellationToken cancellationToken)
    {
        var summary = shift.Summary ?? await services.GetRequiredService<PosShiftService>().SummarizeAsync(shift, cancellationToken);
        var expected = shift.ExpectedCash ?? shift.GetExpectedCash(summary.Cash);
        var movements = new List<object>();

        foreach (var movement in shift.CashMovements ?? [])
        {
            movements.Add(new
            {
                at = await LocalTimeAsync(services, movement.Utc),
                amount = movement.Amount,
                reason = movement.Reason,
                by = movement.ByName,
            });
        }

        return JsonSerializer.Serialize(new
        {
            id = shift.ItemId,
            registerId = shift.RegisterId,
            registerName = shift.RegisterName,
            locationId = shift.LocationId,
            status = shift.Status == RegisterShiftStatus.Open ? "open" : "closed",
            currency = shift.Currency,
            openedBy = shift.OpenedByName,
            openedAt = await LocalTimeAsync(services, shift.OpenedUtc),
            openingFloat = shift.OpeningFloat,
            cashMovements = movements,
            sales = summary.Sales,
            total = summary.Total,
            cash = summary.Cash,
            card = summary.Card,
            other = summary.Other,
            refunded = summary.Refunded,
            tax = summary.Tax,
            departments = summary.Departments.Select(department => new { departmentId = department.DepartmentId, name = department.Name, quantity = department.Quantity, total = department.Total }),
            expectedCash = expected,
            countedCash = shift.CountedCash,
            difference = shift.CountedCash is { } counted ? counted - expected : (decimal?)null,
            cardTips = shift.CardTips,
            cashTips = shift.CashTips,
            tipsTakenByPayroll = PosShiftService.TipsTaken(shift),
            closedBy = shift.ClosedByName,
            closedAt = await LocalTimeAsync(services, shift.ClosedUtc),
            closeNote = shift.CloseNote,
        }, _jsonOptions);
    }

    private static async Task<string> LocalTimeAsync(IServiceProvider services, DateTime? utc)
    {
        if (utc is null || utc.Value == default || services.GetService<ILocalClock>() is not { } localClock)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

        return local.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    private static string ReadString(AIFunctionArguments arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement { ValueKind: JsonValueKind.String } element ? element.GetString() : value.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static int? ReadInt(AIFunctionArguments arguments, string name)
        => ReadString(arguments, name) is { } text && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
}
