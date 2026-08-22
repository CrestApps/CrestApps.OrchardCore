using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Core.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;
using OrchardCore.DisplayManagement.ModelBinding;
namespace CrestApps.OrchardCore.Tests.Validation;

public class CatalogEntryValidationTests
{
    [Fact]
    public async Task ValidateAsync_WhenTheEntrySatisfiesEveryRule_LeavesTheEditorUntouched()
    {
        // Arrange
        var manager = CreateManager();
        var updater = CreateUpdater();

        // Act
        var isValid = await CatalogEntryValidation.ValidateAsync(manager, new StubValidatedEntry(), updater, "StubValidatedEntry", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(isValid);
        Assert.True(updater.ModelState.IsValid);
        Assert.Empty(updater.ModelState);
    }

    [Fact]
    public async Task ValidateAsync_WhenARuleNamesAMember_ReportsItAgainstThePrefixedMemberKey()
    {
        // Arrange
        var manager = CreateManager(errors: new ValidationResult("Name is required.", [nameof(StubValidatedEntry.Name)]));
        var updater = CreateUpdater();

        // Act
        var isValid = await CatalogEntryValidation.ValidateAsync(manager, new StubValidatedEntry(), updater, "StubValidatedEntry", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(isValid);
        Assert.False(updater.ModelState.IsValid);

        var entry = Assert.Single(updater.ModelState);

        Assert.Equal("StubValidatedEntry.Name", entry.Key);
        Assert.Equal("Name is required.", Assert.Single(entry.Value.Errors).ErrorMessage);
    }

    [Fact]
    public async Task ValidateAsync_WhenARuleNamesSeveralMembers_ReportsItAgainstEveryOne()
    {
        // Arrange
        var manager = CreateManager(errors: new ValidationResult("Pick one.", [nameof(StubValidatedEntry.Name), nameof(StubValidatedEntry.Description)]));
        var updater = CreateUpdater();

        // Act
        await CatalogEntryValidation.ValidateAsync(manager, new StubValidatedEntry(), updater, "StubValidatedEntry", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, updater.ModelState.Count);
        Assert.Contains("StubValidatedEntry.Name", updater.ModelState.Keys);
        Assert.Contains("StubValidatedEntry.Description", updater.ModelState.Keys);
    }

    [Fact]
    public async Task ValidateAsync_WhenARuleNamesNoMember_ReportsItAgainstThePrefixAlone()
    {
        // Arrange
        var manager = CreateManager(errors: new ValidationResult("Something is wrong."));
        var updater = CreateUpdater();

        // Act
        await CatalogEntryValidation.ValidateAsync(manager, new StubValidatedEntry(), updater, "StubValidatedEntry", TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(updater.ModelState);

        Assert.Equal("StubValidatedEntry", entry.Key);
    }

    [Fact]
    public async Task ValidateAsync_WhenARuleNamesAnEmptyMember_DoesNotProduceATrailingSeparator()
    {
        // Arrange
        var manager = CreateManager(errors: new ValidationResult("Something is wrong.", [string.Empty]));
        var updater = CreateUpdater();

        // Act
        await CatalogEntryValidation.ValidateAsync(manager, new StubValidatedEntry(), updater, "StubValidatedEntry", TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(updater.ModelState);

        Assert.Equal("StubValidatedEntry", entry.Key);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoPrefixIsSupplied_ReportsAgainstTheBareMemberName()
    {
        // Arrange
        var manager = CreateManager(errors: new ValidationResult("Name is required.", [nameof(StubValidatedEntry.Name)]));
        var updater = CreateUpdater();

        // Act
        await CatalogEntryValidation.ValidateAsync(manager, new StubValidatedEntry(), updater, prefix: string.Empty, TestContext.Current.CancellationToken);

        // Assert
        var entry = Assert.Single(updater.ModelState);

        Assert.Equal("Name", entry.Key);
    }

    [Fact]
    public async Task ValidateAsync_ForwardsTheCancellationTokenToTheManager()
    {
        // Arrange
        var observed = CancellationToken.None;
        var manager = CreateManager(token => observed = token);
        var updater = CreateUpdater();

        using var cancellation = new CancellationTokenSource();

        // Act
        await CatalogEntryValidation.ValidateAsync(manager, new StubValidatedEntry(), updater, "StubValidatedEntry", cancellation.Token);

        // Assert
        Assert.Equal(cancellation.Token, observed);
    }

    [Fact]
    public async Task ValidateAsync_WhenAnArgumentIsMissing_Throws()
    {
        // Arrange
        var manager = CreateManager();
        var updater = CreateUpdater();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CatalogEntryValidation.ValidateAsync<StubValidatedEntry>(null, new StubValidatedEntry(), updater, "StubValidatedEntry", TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CatalogEntryValidation.ValidateAsync(manager, null, updater, "StubValidatedEntry", TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            CatalogEntryValidation.ValidateAsync(manager, new StubValidatedEntry(), null, "StubValidatedEntry", TestContext.Current.CancellationToken));
    }

    private static IUpdateModel CreateUpdater()
    {
        var updater = new Mock<IUpdateModel>();

        updater.SetupGet(x => x.ModelState).Returns(new ModelStateDictionary());

        return updater.Object;
    }

    private static ICatalogManager<StubValidatedEntry> CreateManager(
        Action<CancellationToken> observeToken = null,
        params ValidationResult[] errors)
    {
        var manager = new Mock<ICatalogManager<StubValidatedEntry>>();

        manager
            .Setup(x => x.ValidateAsync(It.IsAny<StubValidatedEntry>(), It.IsAny<CancellationToken>()))
            .Returns((StubValidatedEntry _, CancellationToken token) =>
            {
                observeToken?.Invoke(token);

                var details = new ValidationResultDetails();

                foreach (var error in errors)
                {
                    details.Fail(error);
                }

                return ValueTask.FromResult(details);
            });

        return manager.Object;
    }
}
