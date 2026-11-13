using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Deployments;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Recipes;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CrestApps.OrchardCore.Tests.Banking;

public sealed class BankAccountsRecipeAndDeploymentTests
{
    [Fact]
    public async Task Deployment_WithoutSensitiveData_WritesNoNumberAndNoStatus()
    {
        // Arrange
        var context = new BankingTestContext();
        var account = await context.CreateAsync("123456789", new PartyRef("Staff", "s1"));
        await context.Service.ChangeStatusAsync(account, BankAccountStatus.Verified, cancellationToken: TestContext.Current.CancellationToken);
        context.Observer.Changes.Clear();

        // Act
        var step = await RecipeRoundTrip.ExportAsync(Source(context, mayReveal: true), new BankAccountsDeploymentStep());

        // Assert
        var json = step.ToJsonString();
        var entry = Assert.Single(step[BankAccountsRecipeStep.AccountsProperty]!.AsArray())!.AsObject();

        Assert.Equal("Main checking", entry["Name"]!.GetValue<string>());
        Assert.Equal("Staff", entry["OwnerKind"]!.GetValue<string>());
        Assert.False(entry.ContainsKey(nameof(BankAccount.AccountNumber)));
        Assert.False(entry.ContainsKey(nameof(BankAccount.RoutingNumber)));
        Assert.False(entry.ContainsKey(nameof(BankAccount.Status)));
        Assert.DoesNotContain("123456789", json, StringComparison.Ordinal);
        Assert.DoesNotContain(account.AccountNumber.Cipher, json, StringComparison.Ordinal);
        Assert.Empty(context.Observer.Changes);
    }

    [Fact]
    public async Task Deployment_WithSensitiveData_WritesTheNumbersInClearText_AndRecordsEachOne()
    {
        // Arrange
        var context = new BankingTestContext();
        await context.CreateAsync("123456789");
        context.Observer.Changes.Clear();

        // Act
        var step = await RecipeRoundTrip.ExportAsync(Source(context, mayReveal: true), new BankAccountsDeploymentStep { IncludeSensitiveData = true });

        // Assert
        var entry = Assert.Single(step[BankAccountsRecipeStep.AccountsProperty]!.AsArray())!.AsObject();
        Assert.Equal("123456789", entry[nameof(BankAccount.AccountNumber)]!.GetValue<string>());
        Assert.Equal(BankingTestContext.ValidRoutingNumber, entry[nameof(BankAccount.RoutingNumber)]!.GetValue<string>());
        Assert.Equal([BankAccountChangeKind.NumberRevealed], Assert.Single(context.Observer.Changes).Kinds);
    }

    [Fact]
    public async Task Deployment_WithSensitiveData_RunByAUserWhoMayNotReveal_WritesNoNumber()
    {
        // Arrange
        var context = new BankingTestContext();
        await context.CreateAsync("123456789");

        // Act
        var step = await RecipeRoundTrip.ExportAsync(Source(context, mayReveal: false), new BankAccountsDeploymentStep { IncludeSensitiveData = true });

        // Assert
        Assert.DoesNotContain("123456789", step.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RoundTrip_WithSensitiveData_RecreatesTheAccountProtectedWithTheImportingSitesKeys()
    {
        // Arrange
        var source = new BankingTestContext();
        var account = await source.CreateAsync("123456789", new PartyRef("Staff", "s1"));
        await source.Service.ChangeStatusAsync(account, BankAccountStatus.Verified, "Called the bank", cancellationToken: TestContext.Current.CancellationToken);
        var step = await RecipeRoundTrip.ExportAsync(Source(source, mayReveal: true), new BankAccountsDeploymentStep { IncludeSensitiveData = true });
        var target = new BankingTestContext();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(new BankAccountsRecipeStep(target.Manager, target.Service), step);

        // Assert
        Assert.Empty(errors);
        var imported = Assert.Single(target.Store.Entries);
        Assert.Equal(account.ItemId, imported.ItemId);
        Assert.Equal(new PartyRef("Staff", "s1"), imported.Owner);
        Assert.Equal(BankAccountStatus.Verified, imported.Status);
        Assert.NotEqual(account.AccountNumber.Cipher, imported.AccountNumber.Cipher);
        Assert.Equal("123456789", target.Protector.Unprotect(BankingConstants.AccountNumberPurpose, imported.AccountNumber));
    }

    [Fact]
    public async Task RoundTrip_WithoutSensitiveData_ImportsAnUnverifiedAccountWithoutNumbers()
    {
        // Arrange
        var source = new BankingTestContext();
        var account = await source.CreateAsync();
        await source.Service.ChangeStatusAsync(account, BankAccountStatus.Verified, cancellationToken: TestContext.Current.CancellationToken);
        var step = await RecipeRoundTrip.ExportAsync(Source(source, mayReveal: true), new BankAccountsDeploymentStep());
        var target = new BankingTestContext();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(new BankAccountsRecipeStep(target.Manager, target.Service), step);

        // Assert
        Assert.Empty(errors);
        var imported = Assert.Single(target.Store.Entries);
        Assert.Equal(BankAccountStatus.Unverified, imported.Status);
        Assert.False(BankAccountNumbers.HasNumbers(imported));
    }

    [Fact]
    public async Task Recipe_AWrongRoutingNumber_IsReportedAndTheAccountIsNotImported()
    {
        // Arrange
        var target = new BankingTestContext();
        var step = new JsonObject
        {
            ["name"] = BankingConstants.RecipeStepName,
            [BankAccountsRecipeStep.AccountsProperty] = new JsonArray(new JsonObject
            {
                ["Name"] = "Bad",
                ["HolderName"] = "Pat",
                ["RoutingNumber"] = "021000022",
                ["AccountNumber"] = "123456789",
            }),
        };

        // Act
        var errors = await RecipeRoundTrip.RunAsync(new BankAccountsRecipeStep(target.Manager, target.Service), step);

        // Assert
        Assert.Single(errors);
        Assert.Empty(target.Store.Entries);
    }

    private static BankAccountsDeploymentSource Source(BankingTestContext context, bool mayReveal)
    {
        var authorization = new Mock<IAuthorizationService>();

        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(mayReveal ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "test")),
            },
        };

        return new BankAccountsDeploymentSource(context.Manager, context.Service, accessor, authorization.Object);
    }
}
