using System.Text;

namespace CrestApps.OrchardCore.Checks.Models;

/// <summary>
/// The check line (MICR) of a check: the characters and the positions they print at.
/// </summary>
public sealed class MicrLine
{
    /// <summary>
    /// The width of one position, in points: 1/8 inch.
    /// </summary>
    public const double PositionWidth = 9;

    /// <summary>
    /// The distance from the right edge of the check to the right side of position 1, in points: 5/16 inch.
    /// </summary>
    public const double RightMargin = 22.5;

    /// <summary>
    /// Initializes a new instance of the <see cref="MicrLine"/> class.
    /// </summary>
    /// <param name="characters">The characters and their positions.</param>
    public MicrLine(IEnumerable<MicrCharacter> characters)
    {
        ArgumentNullException.ThrowIfNull(characters);

        Characters = characters.OrderByDescending(character => character.Position).ToArray();
    }

    /// <summary>
    /// Gets the characters, from the leftmost.
    /// </summary>
    public IReadOnlyList<MicrCharacter> Characters { get; }

    /// <summary>
    /// Returns the character at a position, or a space.
    /// </summary>
    /// <param name="position">The position, from 1 at the right.</param>
    /// <returns>The character.</returns>
    public char At(int position)
        => Characters.FirstOrDefault(character => character.Position == position) is { Character: not '\0' } found ? found.Character : ' ';

    /// <summary>
    /// Returns the left edge of a position, in points from the left edge of a check of a width.
    /// </summary>
    /// <param name="position">The position, from 1 at the right.</param>
    /// <param name="checkWidth">The width of the check, in points.</param>
    /// <returns>The distance from the left edge of the check.</returns>
    public static double LeftOf(int position, double checkWidth)
        => checkWidth - RightMargin - (position * PositionWidth);

    /// <summary>
    /// Returns the line as text from its leftmost character, with a space for each empty position.
    /// </summary>
    /// <returns>The text.</returns>
    public override string ToString()
    {
        if (Characters.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        for (var position = Characters[0].Position; position >= 1; position--)
        {
            builder.Append(At(position));
        }

        return builder.ToString().TrimEnd();
    }
}
