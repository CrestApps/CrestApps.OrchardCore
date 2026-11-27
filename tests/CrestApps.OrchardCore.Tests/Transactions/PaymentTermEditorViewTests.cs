using Xunit;

namespace CrestApps.OrchardCore.Tests.Transactions;

/// <summary>
/// Found in the browser: a new payment term could not be saved. The editor hid the fields the chosen kind does not use,
/// but a hidden day of the month of 0 failed the browser's own check (min 1), so the form was never sent and nothing
/// said why. The fields of a hidden row must be disabled, and the day of the month must start at a valid value.
/// </summary>
public sealed class PaymentTermEditorViewTests
{
    [Fact]
    public void Editor_DisablesTheFieldsOfHiddenRows_AndStartsTheDayOfTheMonthAtOne()
    {
        // Arrange
        var view = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Transactions", "Views", "PaymentTermDue.Edit.cshtml"));

        // Assert
        Assert.Contains("input.disabled = !used", view, StringComparison.Ordinal);
        Assert.Contains("Model.DayOfMonth > 0 ? Model.DayOfMonth : 1", view, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
