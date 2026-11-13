namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// One character of a check line and its position, counted from the right edge of the check as the standard counts it:
/// position 1 is the rightmost, and each position is 1/8 inch wide.
/// </summary>
/// <param name="Character">The digit or <see cref="MicrSymbols"/> symbol.</param>
/// <param name="Position">The position, from 1 at the right.</param>
public readonly record struct MicrCharacter(char Character, int Position);
