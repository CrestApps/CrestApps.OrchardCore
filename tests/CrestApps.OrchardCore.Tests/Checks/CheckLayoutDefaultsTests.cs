using CrestApps.OrchardCore.Banking.Checks.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;

namespace CrestApps.OrchardCore.Tests.Checks;

public sealed class CheckLayoutDefaultsTests
{
    // Regression: the signature caption first printed inside the clear band the check line needs.
    [Fact]
    public void CreateFields_KeepsEveryFieldAboveTheCheckLineClearBand()
    {
        // Arrange
        var fields = CheckLayoutDefaults.CreateFields();

        // Act
        var bottoms = fields.Select(pair => (pair.Key, Bottom: pair.Key is CheckFieldNames.Signature or CheckFieldNames.SecondSignature
            ? pair.Value.Y + 32 + pair.Value.FontSize
            : pair.Key is CheckFieldNames.Payer or CheckFieldNames.PayeeAddress
                ? pair.Value.Y + (pair.Value.FontSize * 1.25 * 4)
                : pair.Value.Y + pair.Value.FontSize));

        // Assert
        Assert.All(bottoms, field => Assert.True(field.Bottom <= CheckLayoutDefaults.MicrClearBandTop, $"{field.Key} ends at {field.Bottom}"));
    }

    [Fact]
    public void GetAreas_ThreePerPage_HasThreeChecksAndNoVoucher()
    {
        // Act
        var (checks, vouchers) = CheckLayoutDefaults.GetAreas(CheckForm.ThreePerPage);

        // Assert
        Assert.Equal([0d, 264d, 528d], checks);
        Assert.Empty(vouchers);
    }
}
