using System.Globalization;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.PointOfSale.Services;

/// <summary>
/// The rules a department and an amount rung up in it follow, the same for the admin screen, a recipe and a sale.
/// </summary>
public static partial class PosDepartments
{
    public const int MaxNameLength = 255;

    public const int MaxNumberLength = 50;

    /// <summary>
    /// Tidies a department as it's saved: trimmed text, blanks as none, an upper-case color.
    /// </summary>
    public static void Normalize(PosDepartment department)
    {
        ArgumentNullException.ThrowIfNull(department);

        department.Name = department.Name?.Trim();
        department.Number = string.IsNullOrWhiteSpace(department.Number) ? null : department.Number.Trim();
        department.TaxCategoryCode = string.IsNullOrWhiteSpace(department.TaxCategoryCode) ? null : department.TaxCategoryCode.Trim();
        department.Color = string.IsNullOrWhiteSpace(department.Color) ? null : department.Color.Trim().ToUpperInvariant();
        department.MinimumAmount = department.MinimumAmount is > 0m and var minimum ? Math.Round(minimum, 2, MidpointRounding.AwayFromZero) : null;
        department.MaximumAmount = department.MaximumAmount is > 0m and var maximum ? Math.Round(maximum, 2, MidpointRounding.AwayFromZero) : null;
    }

    /// <summary>
    /// Returns what's wrong with a department, against the others, or nothing.
    /// </summary>
    public static IEnumerable<string> Validate(PosDepartment department, IEnumerable<PosDepartment> existing, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(department);
        ArgumentNullException.ThrowIfNull(S);

        if (string.IsNullOrEmpty(department.Name))
        {
            yield return S["Name the department, such as Candy or Grocery."];
        }
        else if (department.Name.Length > MaxNameLength)
        {
            yield return S["Keep the department's name to {0} characters.", MaxNameLength];
        }

        if (department.Number?.Length > MaxNumberLength)
        {
            yield return S["Keep the department's number to {0} characters.", MaxNumberLength];
        }
        else if (department.Number is not null &&
            (existing ?? []).Any(other => other.ItemId != department.ItemId && string.Equals(other.Number, department.Number, StringComparison.OrdinalIgnoreCase)))
        {
            yield return S["Another department has the number {0}.", department.Number];
        }

        if (department.Color is not null && !ColorPattern().IsMatch(department.Color))
        {
            yield return S["Enter the key's color as a hex color, such as #2E7D32, or leave it blank."];
        }

        if (department.MinimumAmount is { } minimum && department.MaximumAmount is { } maximum && minimum > maximum)
        {
            yield return S["The least amount can't be more than the most."];
        }
    }

    /// <summary>
    /// Returns why an amount can't be rung up in a department, or <see langword="null"/> when it can.
    /// </summary>
    public static string CheckAmount(PosDepartment department, decimal amount, string currency, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(department);
        ArgumentNullException.ThrowIfNull(S);

        if (!department.IsEnabled)
        {
            return S["{0} isn't rung up in any more.", department.Name];
        }

        if (amount <= 0m)
        {
            return S["Key in the amount to ring up in {0}.", department.Name];
        }

        if (amount != Math.Round(amount, 2))
        {
            return S["Ring up {0} in whole cents.", department.Name];
        }

        if (department.MinimumAmount is { } minimum && amount < minimum)
        {
            return S["{0} rings up at least {1}.", department.Name, Format(minimum, currency)];
        }

        if (department.MaximumAmount is { } maximum && amount > maximum)
        {
            return S["{0} rings up at most {1}: check the amount.", department.Name, Format(maximum, currency)];
        }

        return null;
    }

    /// <summary>
    /// Returns the keys a register shows: those it was programmed with, or a key for every department that's on, in
    /// their order. A key whose department is gone or off isn't shown.
    /// </summary>
    /// <param name="register">The register.</param>
    /// <param name="departments">Every department, in their order.</param>
    public static IReadOnlyList<PosRegisterKey> GetKeys(Register register, IEnumerable<PosDepartment> departments)
    {
        ArgumentNullException.ThrowIfNull(register);

        var enabled = (departments ?? []).Where(department => department.IsEnabled).ToList();

        if (register.Keys is not { Count: > 0 } keys)
        {
            return [.. enabled.Select(department => new PosRegisterKey(department, department.Name, null))];
        }

        var byId = enabled.ToDictionary(department => department.ItemId, StringComparer.Ordinal);

        return [.. keys
            .Where(key => !string.IsNullOrEmpty(key?.DepartmentId) && byId.ContainsKey(key.DepartmentId))
            .Select(key => new PosRegisterKey(byId[key.DepartmentId], key.Label ?? byId[key.DepartmentId].Name, key.PresetAmount))];
    }

    private static string Format(decimal amount, string currency)
        => string.IsNullOrEmpty(currency)
            ? amount.ToString("0.00", CultureInfo.CurrentCulture)
            : ProductPriceFormatter.Format(amount, currency);

    [GeneratedRegex("^#[0-9A-F]{6}$")]
    private static partial Regex ColorPattern();
}

/// <summary>
/// A department key as a register shows it.
/// </summary>
/// <param name="Department">The department it rings up in.</param>
/// <param name="Label">What it reads.</param>
/// <param name="PresetAmount">What it rings up with nothing keyed in, or <see langword="null"/>.</param>
public sealed record PosRegisterKey(PosDepartment Department, string Label, decimal? PresetAmount);
