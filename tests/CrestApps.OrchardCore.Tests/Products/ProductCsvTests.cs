using CrestApps.OrchardCore.Products.Core.Transfer;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// Product exports from Shopify and WooCommerce read as the same products, with their variants, prices and stock.
/// </summary>
public sealed class ProductCsvTests
{
    [Fact]
    public void AShopifyExport_ReadsAsProductsWithVariants()
    {
        const string csv = "Handle,Title,Body (HTML),Vendor,Type,Published,Option1 Name,Option1 Value,Option2 Name,Option2 Value,Variant SKU,Variant Price,Variant Compare At Price,Variant Inventory Qty,Image Src\r\n"
            + "classic-tee,Classic Tee,\"<p>Soft, cotton</p>\",Contoso,Shirts,TRUE,Size,S,Color,Red,TEE-S-R,20.00,25.00,4,https://cdn.example/tee.jpg\r\n"
            + "classic-tee,,,,,,,M,,Red,TEE-M-R,22.00,,0,\r\n"
            + "classic-tee,,,,,,,,,,,,,,https://cdn.example/tee-2.jpg\r\n"
            + "mug,Mug,,Fabrikam,Kitchen,FALSE,Title,Default Title,,,MUG-1,12.50,,7,\r\n";

        var read = ProductCsv.Read(csv);

        Assert.Equal(ProductCsv.Shopify, read.Format);
        Assert.Empty(read.Problems);
        Assert.Equal(2, read.Products.Count);

        var tee = read.Products[0];
        Assert.Equal("Classic Tee", tee.Title);
        Assert.Equal("<p>Soft, cotton</p>", tee.DescriptionHtml);
        Assert.Equal(("Contoso", "Shirts"), (tee.Brand, tee.Category));
        Assert.Equal(["Size", "Color"], tee.Options.Select(option => option.Name));
        Assert.Equal(["S", "M"], tee.Options[0].Values);
        Assert.Equal(2, tee.Variants.Count);
        Assert.Equal(("TEE-M-R", (decimal?)22m, (int?)0), (tee.Variants[1].Sku, tee.Variants[1].Price, tee.Variants[1].Quantity));
        Assert.Equal("Red", tee.Variants[1].Options["Color"]);
        Assert.Equal(25m, tee.Variants[0].CompareAtPrice);
        Assert.Equal(20m, tee.Price);
        Assert.Equal(2, tee.ImageUrls.Count);

        var mug = read.Products[1];
        Assert.Empty(mug.Variants);
        Assert.Empty(mug.Options);
        Assert.Equal(("MUG-1", (decimal?)12.5m, (int?)7), (mug.Sku, mug.Price, mug.Quantity));
        Assert.False(mug.Published);
    }

    [Fact]
    public void AWooCommerceExport_ReadsVariableProductsAndTheirVariations()
    {
        const string csv = "ID,Type,SKU,Name,Published,Description,Sale price,Regular price,Categories,Stock,Parent,Attribute 1 name,Attribute 1 value(s),Images\r\n"
            + "10,variable,HOOD,Hoodie,1,Warm,,,\"Clothing > Hoodies\",,,Size,\"S, M\",https://cdn.example/h.jpg\r\n"
            + "11,variation,HOOD-S,Hoodie - S,1,,35,45,,3,id:10,Size,S,\r\n"
            + "12,variation,HOOD-M,Hoodie - M,1,,,45,,5,HOOD,Size,M,\r\n"
            + "13,simple,CAP,Cap,0,,,18,Accessories,9,,,,\r\n"
            + "14,grouped,SET,Set,1,,,,,,,,,\r\n"
            + "15,variation,X-1,Orphan,1,,,5,,,id:99,Size,L,\r\n";

        var read = ProductCsv.Read(csv);

        Assert.Equal(ProductCsv.WooCommerce, read.Format);
        Assert.Equal(2, read.Problems.Count);

        var hoodie = read.Products.Single(product => product.Title == "Hoodie");
        Assert.Equal("Hoodies", hoodie.Category);
        Assert.Equal(["S", "M"], Assert.Single(hoodie.Options).Values);
        Assert.Equal(2, hoodie.Variants.Count);
        Assert.Equal(((decimal?)35m, (decimal?)45m, (int?)3), (hoodie.Variants[0].Price, hoodie.Variants[0].CompareAtPrice, hoodie.Variants[0].Quantity));
        Assert.Equal(((decimal?)45m, (decimal?)null), (hoodie.Variants[1].Price, hoodie.Variants[1].CompareAtPrice));

        var cap = read.Products.Single(product => product.Title == "Cap");
        Assert.Equal(("CAP", (decimal?)18m, (int?)9), (cap.Sku, cap.Price, cap.Quantity));
        Assert.False(cap.Published);
    }

    [Fact]
    public void AFileOfAnotherKind_IsRefused_AndAFormulaIsNeverWrittenAsOne()
    {
        var read = ProductCsv.Read("Name,Email\r\nSam,sam@example.com\r\n");

        Assert.Null(read.Format);
        Assert.Single(read.Problems);
        Assert.Equal("'=HYPERLINK(1)", CsvTable.Escape("=HYPERLINK(1)"));
        Assert.Equal("-5.00", CsvTable.Escape("-5.00"));
        Assert.Equal("\"a,b\"", CsvTable.Escape("a,b"));
    }
}
