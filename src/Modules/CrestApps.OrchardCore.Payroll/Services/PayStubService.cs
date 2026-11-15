using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Writes a pay statement as a PDF stub in the reader's language, for the admin statement page and My Pay.
/// </summary>
public sealed class PayStubService
{
    private readonly IPayCodeManager _codes;
    private readonly IPayAccumulatorService _accumulators;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayStubService"/> class.
    /// </summary>
    /// <param name="codes">The pay codes.</param>
    /// <param name="accumulators">The accumulators, for the year's amounts.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayStubService(IPayCodeManager codes, IPayAccumulatorService accumulators, IStringLocalizer<PayStubService> stringLocalizer)
    {
        _codes = codes;
        _accumulators = accumulators;
        S = stringLocalizer;
    }

    /// <summary>
    /// Writes a statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The PDF file.</returns>
    public async Task<byte[]> WriteAsync(PayStatement statement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var codes = (await _codes.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, code => code.Code, StringComparer.Ordinal);

        // A draft is not in the accumulators yet: it shows no year-to-date column.
        var accumulators = statement.Status == PayStatementStatus.Draft
            ? null
            : await _accumulators.GetAsync(statement.StaffMemberId, statement.EmployerId, statement.PayDate, statement.PeriodStart, statement.PeriodEnd, cancellationToken);

        return PayStubDocument.Write(statement, codes, accumulators, new PayStubTexts
        {
            Title = S["Pay statement"],
            Period = S["Period"],
            PayDate = S["Pay date"],
            Number = S["Statement"],
            Current = S["Current"],
            YearToDate = S["Year to date"],
            Hours = S["Hours"],
            Gross = S["Gross pay"],
            Net = S["Net pay"],
            Payments = S["Paid by"],
            Deposit = S["Deposit"],
            Check = S["Check"],
            Voided = S["VOID"],
        });
    }

    /// <summary>
    /// Gets the file name of a statement's stub.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>The file name.</returns>
    public static string FileName(PayStatement statement)
        => $"pay-statement-{(string.IsNullOrEmpty(statement.Number) ? "draft" : statement.Number)}.pdf";
}
