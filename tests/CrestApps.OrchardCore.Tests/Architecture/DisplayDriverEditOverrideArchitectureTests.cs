using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A display driver that builds its editor in <c>EditAsync</c> and ends <c>UpdateAsync</c> with <c>return Edit(model,
/// context)</c> calls the base <c>DisplayDriver.Edit</c>, which builds nothing. Admin controllers show the shape
/// <c>UpdateEditorAsync</c> returns again when a post is not valid, so that editor came back without the driver's
/// fields (the business-hours calendar did, after its <c>Edit</c> override became <c>EditAsync</c>).
/// </summary>
public sealed partial class DisplayDriverEditOverrideArchitectureTests
{
    [Fact]
    public void ADriverThatBuildsItsEditorInEditAsync_ReturnsEditAsyncFromItsUpdate()
    {
        // Arrange
        var offenders = new List<string>();

        // Act
        foreach (var file in Ratchet.EnumerateFiles("src"))
        {
            var text = Ratchet.ReadAllText(file);

            if (!EditAsyncOverride().IsMatch(text) || EditOverride().IsMatch(text))
            {
                continue;
            }

            foreach (Match match in ReturnEdit().Matches(text))
            {
                offenders.Add($"{file.RelativePath}:{Ratchet.LineAt(text, match.Index)}");
            }
        }

        // Assert
        Assert.True(
            offenders.Count == 0,
            Ratchet.BuildMessage(
                "These drivers override EditAsync but return the base Edit, which builds nothing, so the editor shown again after an invalid post is empty. Return await EditAsync(model, context) instead:",
                offenders));
    }

    [GeneratedRegex(@"\boverride\s+(?:async\s+)?Task<IDisplayResult>\s+EditAsync\s*\(")]
    private static partial Regex EditAsyncOverride();

    [GeneratedRegex(@"\boverride\s+IDisplayResult\s+Edit\s*\(")]
    private static partial Regex EditOverride();

    [GeneratedRegex(@"\breturn\s+Edit\s*\(")]
    private static partial Regex ReturnEdit();
}
