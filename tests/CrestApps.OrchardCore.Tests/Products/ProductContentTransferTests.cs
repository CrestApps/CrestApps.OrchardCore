using CrestApps.OrchardCore.Inventory.ContentTransfer;
using CrestApps.OrchardCore.Products.ContentTransfer;
using CrestApps.OrchardCore.Products.Core.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// The text forms of a product's options and its variants' stock in a Content Transfer file read back as written.
/// </summary>
public sealed class ProductContentTransferTests
{
    [Fact]
    public void Options_ReadBackAsWritten()
    {
        var written = ProductVariantsPartContentImportHandler.WriteOptions(
        [
            new ProductOption { Name = "Size", Values = ["S", "M"] },
            new ProductOption { Name = "Color", Values = ["Red", "Blue"] },
            new ProductOption { Name = "Empty", Values = [] },
        ]);

        Assert.Equal("Size: S, M; Color: Red, Blue", written);

        var read = ProductVariantsPartContentImportHandler.ParseOptions(written + "; nonsense; : x");

        Assert.Equal(["Size", "Color"], read.Select(option => option.Name));
        Assert.Equal(["Red", "Blue"], read[1].Values);
    }

    [Fact]
    public void VariantStock_ReadsSkusAndOptions()
    {
        var stock = InventoryPartContentImportHandler.ParseVariantStock("TEE-S=4; M / Red = 2; bad; X=-3; Y=lots");

        Assert.Equal(new Dictionary<string, int> { ["TEE-S"] = 4, ["M / Red"] = 2, ["X"] = 0 }, stock);
        Assert.True(stock.ContainsKey("tee-s"));
    }
}
