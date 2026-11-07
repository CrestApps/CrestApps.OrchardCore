using System.Collections.Frozen;

namespace CrestApps.OrchardCore.StateMachines;

/// <summary>
/// The moves a status can make, declared once as a table: which statuses each status may move to, whether a record
/// may "move" to the status it is already in, and which statuses are final.
/// </summary>
/// <remarks>
/// A status the table does not list, or lists with no moves, is terminal. The moves of a status keep the order they
/// are declared in, so a list of next steps reads the way the table was written.
/// </remarks>
/// <typeparam name="TEnum">The status type.</typeparam>
public sealed class TransitionTable<TEnum>
    where TEnum : struct, Enum
{
    private readonly FrozenDictionary<TEnum, TEnum[]> _moves;
    private readonly FrozenDictionary<TEnum, FrozenSet<TEnum>> _lookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransitionTable{TEnum}"/> class.
    /// </summary>
    /// <param name="moves">The statuses each status may move to.</param>
    /// <param name="allowStaying">
    /// Whether a move to the status a record is already in is admitted, as a repeated event that changes nothing.
    /// </param>
    public TransitionTable(IReadOnlyDictionary<TEnum, TEnum[]> moves, bool allowStaying = false)
    {
        ArgumentNullException.ThrowIfNull(moves);

        _moves = moves.ToFrozenDictionary(pair => pair.Key, pair => pair.Value?.Distinct().ToArray() ?? []);
        _lookup = _moves.ToFrozenDictionary(pair => pair.Key, pair => pair.Value.ToFrozenSet());
        AllowStaying = allowStaying;
    }

    /// <summary>
    /// Gets whether a move to the status a record is already in is admitted.
    /// </summary>
    public bool AllowStaying { get; }

    /// <summary>
    /// Gets the statuses a record may move to from a status, in the order they were declared.
    /// </summary>
    /// <param name="from">The status the record is in.</param>
    /// <returns>The statuses, empty for a terminal status.</returns>
    public IReadOnlyList<TEnum> Next(TEnum from)
        => _moves.TryGetValue(from, out var next) ? next : [];

    /// <summary>
    /// Tells whether a record may move from one status to another.
    /// </summary>
    /// <param name="from">The status the record is in.</param>
    /// <param name="to">The status it would move to.</param>
    /// <returns><see langword="true"/> when the move is admitted; otherwise, <see langword="false"/>.</returns>
    public bool CanMove(TEnum from, TEnum to)
    {
        if (EqualityComparer<TEnum>.Default.Equals(from, to))
        {
            return AllowStaying;
        }

        return _lookup.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    /// <summary>
    /// Tells whether a status is final: a record in it can move nowhere else.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns><see langword="true"/> when the status is terminal; otherwise, <see langword="false"/>.</returns>
    public bool IsTerminal(TEnum status)
        => !_moves.TryGetValue(status, out var next) || next.Length == 0;
}
