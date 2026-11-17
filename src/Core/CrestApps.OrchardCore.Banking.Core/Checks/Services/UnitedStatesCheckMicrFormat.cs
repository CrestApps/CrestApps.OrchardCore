using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;

namespace CrestApps.OrchardCore.Banking.Checks.Core.Services;

/// <summary>
/// Lays out the United States check line (ANSI X9.100-160), counted in 1/8 inch positions from the right edge of the
/// check: the amount field (positions 1 to 12) is left blank for the bank to encode; the on-us field (14 to 32) holds
/// the account number and the on-us symbol at 14, and on a personal check the check number too; the routing number
/// sits between transit symbols at 43 and 33; on a business check the auxiliary on-us field, left of position 44, holds
/// the check number between on-us symbols.
/// </summary>
public sealed class UnitedStatesCheckMicrFormat : ICheckMicrFormat
{
    /// <summary>
    /// The position of the on-us symbol that closes the on-us field.
    /// </summary>
    public const int OnUsSymbolPosition = 14;

    /// <summary>
    /// The leftmost position of the on-us field.
    /// </summary>
    public const int OnUsFieldEnd = 32;

    /// <summary>
    /// The position of the transit symbol right of the routing number.
    /// </summary>
    public const int TransitRight = 33;

    /// <summary>
    /// The position of the transit symbol left of the routing number.
    /// </summary>
    public const int TransitLeft = 43;

    /// <summary>
    /// The rightmost position of the auxiliary on-us field (position 44 is the external processing code, left blank).
    /// </summary>
    public const int AuxiliaryOnUsStart = 45;

    /// <summary>
    /// The leftmost position of the auxiliary on-us field.
    /// </summary>
    public const int AuxiliaryOnUsEnd = 56;

    /// <summary>
    /// The fewest digits the check number prints with in the auxiliary on-us field.
    /// </summary>
    public const int AuxiliarySerialDigits = 6;

    /// <inheritdoc />
    public string Country => "US";

    /// <inheritdoc />
    public MicrLine Format(MicrLineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var routing = Digits(request.RoutingNumber);
        var account = Digits(request.AccountNumber);

        if (routing.Length != 9)
        {
            throw new ArgumentException("A United States routing number has nine digits.", nameof(request));
        }

        if (account.Length == 0)
        {
            throw new ArgumentException("The account number is required.", nameof(request));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.CheckNumber);

        var characters = new List<MicrCharacter>();

        // The routing number between its transit symbols.
        characters.Add(new MicrCharacter(MicrSymbols.Transit, TransitLeft));
        PlaceRight(characters, routing, TransitLeft - 1);
        characters.Add(new MicrCharacter(MicrSymbols.Transit, TransitRight));

        var serial = request.CheckNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (request.UseAuxiliaryOnUs)
        {
            // The on-us field: the account number, closed by the on-us symbol.
            characters.Add(new MicrCharacter(MicrSymbols.OnUs, OnUsSymbolPosition));
            Fit(account.Length, OnUsSymbolPosition + 1, OnUsFieldEnd, "account number");
            PlaceRight(characters, account, OnUsSymbolPosition + account.Length);

            // The auxiliary on-us field: the check number between on-us symbols.
            var padded = serial.PadLeft(AuxiliarySerialDigits, '0');

            Fit(padded.Length + 2, AuxiliaryOnUsStart, AuxiliaryOnUsEnd, "check number");
            characters.Add(new MicrCharacter(MicrSymbols.OnUs, AuxiliaryOnUsStart));
            PlaceRight(characters, padded, AuxiliaryOnUsStart + padded.Length);
            characters.Add(new MicrCharacter(MicrSymbols.OnUs, AuxiliaryOnUsStart + padded.Length + 1));
        }
        else
        {
            // The on-us field of a personal check: the account number, the on-us symbol, then the check number.
            Fit(account.Length + 1 + serial.Length, OnUsSymbolPosition, OnUsFieldEnd, "account and check numbers");
            PlaceRight(characters, serial, OnUsSymbolPosition + serial.Length - 1);
            characters.Add(new MicrCharacter(MicrSymbols.OnUs, OnUsSymbolPosition + serial.Length));
            PlaceRight(characters, account, OnUsSymbolPosition + serial.Length + account.Length);
        }

        return new MicrLine(characters);
    }

    // Writes text so its leftmost character is at a position and the others follow to the right.
    private static void PlaceRight(List<MicrCharacter> characters, string text, int leftmostPosition)
    {
        for (var i = 0; i < text.Length; i++)
        {
            characters.Add(new MicrCharacter(text[i], leftmostPosition - i));
        }
    }

    private static void Fit(int length, int from, int to, string what)
    {
        if (length > to - from + 1)
        {
            throw new ArgumentException($"The {what} does not fit in the check line.");
        }
    }

    private static string Digits(string value)
        => new((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
}
