using CrestApps.OrchardCore.Banking.Reports;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Moq;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Banking;

public sealed class BankAccountsReportDataSetTests
{
    [Fact]
    public void Fields_NeverIncludeTheRoutingOrTheAccountNumber()
    {
        // Arrange
        var dataSet = new BankAccountsReportDataSet(Mock.Of<ISession>(), Mock.Of<IAuthorizationService>(), RecipeRoundTrip.Localizer<BankAccountsReportDataSet>());

        // Act
        var names = dataSet.Fields.Select(field => field.Name).ToArray();

        // Assert
        Assert.Contains("AccountNumberLastDigits", names);
        Assert.DoesNotContain(names, name => name.Contains("RoutingNumber", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Equals("AccountNumber", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Cipher", StringComparison.OrdinalIgnoreCase));
    }
}
