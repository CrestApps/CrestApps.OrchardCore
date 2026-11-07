using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Tests.Core.StateMachines;

/// <summary>
/// The status tables of Contact Center, Dispatch and Work Orders are declared on <see cref="TransitionTable{TEnum}"/>.
/// </summary>
public sealed class TransitionTableTests
{
    private static readonly TransitionTable<Door> _door = new(new Dictionary<Door, Door[]>
    {
        [Door.Closed] = [Door.Open, Door.Locked],
        [Door.Open] = [Door.Closed],
        [Door.Locked] = [Door.Closed, Door.Broken],
        [Door.Broken] = [],
    });

    [Theory]
    [InlineData(Door.Closed, Door.Open, true)]
    [InlineData(Door.Closed, Door.Locked, true)]
    [InlineData(Door.Open, Door.Locked, false)]
    [InlineData(Door.Broken, Door.Closed, false)]
    [InlineData(Door.Lost, Door.Closed, false)]
    public void CanMove_FollowsTheTable(Door from, Door to, bool expected)
        => Assert.Equal(expected, _door.CanMove(from, to));

    [Fact]
    public void Staying_IsRefusedUnlessTheTableAdmitsIt()
    {
        // Arrange
        var lenient = new TransitionTable<Door>(new Dictionary<Door, Door[]> { [Door.Closed] = [Door.Open] }, allowStaying: true);

        // Act & Assert
        Assert.False(_door.CanMove(Door.Closed, Door.Closed));
        Assert.True(lenient.CanMove(Door.Closed, Door.Closed));
        Assert.True(lenient.CanMove(Door.Broken, Door.Broken));
    }

    [Theory]
    [InlineData(Door.Broken, true)]
    [InlineData(Door.Lost, true)]
    [InlineData(Door.Locked, false)]
    public void IsTerminal_IsAStatusThatMovesNowhere(Door status, bool expected)
        => Assert.Equal(expected, _door.IsTerminal(status));

    [Fact]
    public void Next_KeepsTheDeclaredOrder()
        => Assert.Equal([Door.Closed, Door.Broken], _door.Next(Door.Locked));

    public enum Door
    {
        Closed,
        Open,
        Locked,
        Broken,
        Lost,
    }
}
