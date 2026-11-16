using CrestApps.OrchardCore.Banking.Checks.Models;

namespace CrestApps.OrchardCore.Banking.Checks.Core.Services;

/// <summary>
/// The size of a check on a letter sheet, and where each field prints when the layout does not say.
/// </summary>
public static class CheckLayoutDefaults
{
    /// <summary>
    /// The width of the check, in points: the width of a letter sheet.
    /// </summary>
    public const double CheckWidth = 612;

    /// <summary>
    /// The height of the check, in points: a third of a letter sheet (3.5 inches and a bit).
    /// </summary>
    public const double CheckHeight = 264;

    /// <summary>
    /// The distance from the bottom of the check to the baseline of the check line, in points (about 1/4 inch), inside
    /// the clear band of 5/8 inch the standard keeps free.
    /// </summary>
    public const double MicrBaselineFromBottom = 18;

    /// <summary>
    /// The top of the clear band the standard keeps free for the check line: 5/8 inch above the bottom of the check.
    /// Nothing else prints below it.
    /// </summary>
    public const double MicrClearBandTop = CheckHeight - 45;

    /// <summary>
    /// Returns the default places of every field.
    /// </summary>
    /// <returns>The places, by field name.</returns>
    public static Dictionary<string, CheckFieldPosition> CreateFields()
        => new(StringComparer.Ordinal)
        {
            [CheckFieldNames.Payer] = new() { X = 36, Y = 24, Width = 240, FontSize = 9 },
            [CheckFieldNames.Bank] = new() { X = 300, Y = 24, Width = 160, FontSize = 8 },
            [CheckFieldNames.Number] = new() { X = 500, Y = 24, Width = 76, FontSize = 11 },
            [CheckFieldNames.Date] = new() { X = 470, Y = 66, Width = 106, FontSize = 10 },
            [CheckFieldNames.Payee] = new() { X = 100, Y = 100, Width = 360, FontSize = 11 },
            [CheckFieldNames.Amount] = new() { X = 480, Y = 100, Width = 96, FontSize = 11 },
            [CheckFieldNames.AmountInWords] = new() { X = 36, Y = 126, Width = 500, FontSize = 10 },
            [CheckFieldNames.PayeeAddress] = new() { X = 72, Y = 150, Width = 240, FontSize = 9 },
            [CheckFieldNames.Memo] = new() { X = 36, Y = 204, Width = 240, FontSize = 9 },
            [CheckFieldNames.Signature] = new() { X = 360, Y = 176, Width = 216, FontSize = 8 },
            [CheckFieldNames.SecondSignature] = new() { X = 360, Y = 140, Width = 216, FontSize = 8 },
        };

    /// <summary>
    /// Returns the place of a field: the layout's, else the default.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="field">The field name.</param>
    /// <returns>The place.</returns>
    public static CheckFieldPosition Get(CheckLayout layout, string field)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.Fields is not null && layout.Fields.TryGetValue(field, out var position) && position is not null)
        {
            return position;
        }

        return CreateFields().GetValueOrDefault(field) ?? new CheckFieldPosition();
    }

    /// <summary>
    /// Returns the tops of the checks and the vouchers on a sheet of a form, in points from the top of the sheet.
    /// </summary>
    /// <param name="form">The form.</param>
    /// <returns>The tops of the checks and of the vouchers.</returns>
    public static (double[] Checks, double[] Vouchers) GetAreas(CheckForm form)
        => form switch
        {
            CheckForm.CheckInMiddle => ([CheckHeight], [0, CheckHeight * 2]),
            CheckForm.CheckOnBottom => ([CheckHeight * 2], [0, CheckHeight]),
            CheckForm.ThreePerPage => ([0, CheckHeight, CheckHeight * 2], []),
            _ => ([0], [CheckHeight, CheckHeight * 2]),
        };
}
