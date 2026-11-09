using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Taxation.Deployments;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Recipes;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// The store's tax registrations and customers' exemption certificates travel between sites: each is exported,
/// imported into an empty store with the same ids and content, and matched on a second run instead of duplicated.
/// </summary>
public sealed class TaxationRecordsPortabilityTests
{
    [Fact]
    public async Task Registrations_ExportAndImportIntoAnEmptyStore_WithoutDuplicates()
    {
        // Arrange
        var source = new InMemoryRegistrationStore();

        source.Entries.Add(new MerchantTaxRegistration
        {
            ItemId = "4tr00000000000000000000001",
            Name = "California sales tax",
            JurisdictionId = "us-ca",
            TaxType = "SalesTax",
            RegistrationNumber = "123-456",
            ThresholdAmount = 100000m,
            EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        var target = new InMemoryRegistrationStore();
        var step = new MerchantTaxRegistrationStep(target, RecipeRoundTrip.Localizer<MerchantTaxRegistrationStep>());

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new MerchantTaxRegistrationDeploymentSource(source), new MerchantTaxRegistrationDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, exported);

        // Assert
        Assert.Equal("MerchantTaxRegistration", exported["name"].GetValue<string>());
        Assert.Empty(first);
        Assert.Empty(second);

        var registration = Assert.Single(target.Entries);

        Assert.Equal("4tr00000000000000000000001", registration.ItemId);
        Assert.Equal("us-ca", registration.JurisdictionId);
        Assert.Equal("123-456", registration.RegistrationNumber);
        Assert.Equal(100000m, registration.ThresholdAmount);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), registration.EffectiveFromUtc);
    }

    [Fact]
    public async Task Registrations_RefuseOneWithoutAJurisdiction()
    {
        // Arrange
        var target = new InMemoryRegistrationStore();
        var step = new MerchantTaxRegistrationStep(target, RecipeRoundTrip.Localizer<MerchantTaxRegistrationStep>());
        var recipe = JsonNode.Parse("""{ "name": "MerchantTaxRegistration", "MerchantTaxRegistrations": [ { "Name": "VAT" } ] }""").AsObject();

        // Act
        var errors = await RecipeRoundTrip.RunAsync(step, recipe);

        // Assert
        Assert.Equal(["The tax registration 'VAT' needs the jurisdiction it is registered in."], errors);
        Assert.Empty(target.Entries);
    }

    [Fact]
    public async Task Certificates_KeepTheirIds_AndMatchByCustomerAndNumber()
    {
        // Arrange
        var source = new InMemoryCertificateStore();

        source.Entries.Add(new ExemptionCertificate
        {
            ItemId = "4ec00000000000000000000001",
            Name = "Resale",
            CertificateNumber = "R-1001",
            CustomerId = "customer-1",
            ExemptionType = "Resale",
            Status = ExemptionStatus.Active,
            TaxTypes = ["SalesTax"],
            JurisdictionIds = ["us-ca"],
        });

        var target = new InMemoryCertificateStore();
        var step = new ExemptionCertificateStep(target, RecipeRoundTrip.Localizer<ExemptionCertificateStep>());
        var withoutId = JsonNode.Parse("""
            { "name": "ExemptionCertificate", "ExemptionCertificates": [ { "CustomerId": "customer-1", "CertificateNumber": "r-1001", "Name": "Resale", "Status": "Revoked" } ] }
            """).AsObject();

        // Act
        var exported = await RecipeRoundTrip.ExportAsync(new ExemptionCertificateDeploymentSource(source), new ExemptionCertificateDeploymentStep());
        var first = await RecipeRoundTrip.RunAsync(step, exported);
        var second = await RecipeRoundTrip.RunAsync(step, withoutId);

        // Assert
        Assert.Equal("ExemptionCertificate", exported["name"].GetValue<string>());
        Assert.Empty(first);
        Assert.Empty(second);

        var certificate = Assert.Single(target.Entries);

        Assert.Equal("4ec00000000000000000000001", certificate.ItemId);
        Assert.Equal(["SalesTax"], certificate.TaxTypes);
        Assert.Equal(["us-ca"], certificate.JurisdictionIds);
        Assert.Equal(ExemptionStatus.Revoked, certificate.Status);
    }

    private sealed class InMemoryRegistrationStore : InMemoryCatalog<MerchantTaxRegistration>, IMerchantTaxRegistrationStore;

    private sealed class InMemoryCertificateStore : InMemoryCatalog<ExemptionCertificate>, IExemptionCertificateStore;
}
