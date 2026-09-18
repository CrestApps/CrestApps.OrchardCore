namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Fails when the suite loses tests.
/// </summary>
/// <remarks>
/// Phase 1 of the Contact Center framework extraction relocates most of the suite into new projects,
/// and the tests move with it. A relocation that quietly drops a test file leaves a green build and a
/// smaller suite, which reads as success. Nothing else notices, because every remaining test passes.
/// <para>
/// This is deliberately a floor rather than a per-class coverage audit. A 500-row audit table can be
/// satisfied by writing "not covered" in every row; a count that may not go down cannot. When tests
/// are legitimately consolidated, lower the floor in the same commit and say why in the message.
/// </para>
/// </remarks>
public sealed class MovingSetTestCountRatchetTests
{
    /// <summary>
    /// The number of test methods in this assembly at the time the ratchet was last moved.
    /// </summary>
    /// <remarks>
    /// Methods, not cases: a theory counts once here but reports once per data row, so this number is
    /// lower than the total the runner prints. Raise it when the suite grows, so a later deletion is
    /// caught against the new high-water mark rather than the original one.
    /// </remarks>
    private const int MinimumTestMethods = 3815;

    [Fact]
    public void TheSuite_DoesNotShrink()
    {
        // Arrange
        var assembly = typeof(MovingSetTestCountRatchetTests).Assembly;

        // Act
        var testMethods = assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods())
            .Count(method =>
                method.GetCustomAttributes(inherit: false)
                    .Any(attribute =>
                        attribute is FactAttribute ||
                        attribute is TheoryAttribute));

        // Assert
        Assert.True(
            testMethods >= MinimumTestMethods,
            $"This assembly declares {testMethods} test methods and the floor is {MinimumTestMethods}. " +
            "Tests were removed. If that was deliberate - a consolidation, or a move into another project - " +
            $"lower {nameof(MinimumTestMethods)} in the same commit and say why. If it was not, a relocation " +
            "has dropped test files and the coverage they represented is gone.");
    }
}
