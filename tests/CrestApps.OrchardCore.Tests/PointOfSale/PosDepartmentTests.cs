using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.PointOfSale;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.PointOfSale;

/// <summary>
/// Department sales at the register: an amount keyed in and counted toward a department, taxed by its tax category,
/// with no item behind it, and the keys a register shows for them.
/// </summary>
public sealed class PosDepartmentTests
{
    private static readonly IStringLocalizer S = new PassThroughStringLocalizer<PosDepartmentTests>();

    [Fact]
    public void ADepartment_IsTidiedAsItsSaved()
    {
        var department = new PosDepartment
        {
            Name = "  Candy ",
            Number = " ",
            TaxCategoryCode = " Candy ",
            Color = " #c2185b ",
            MinimumAmount = 0m,
            MaximumAmount = 49.999m,
        };

        PosDepartments.Normalize(department);

        Assert.Equal("Candy", department.Name);
        Assert.Null(department.Number);
        Assert.Equal("Candy", department.TaxCategoryCode);
        Assert.Equal("#C2185B", department.Color);
        Assert.Null(department.MinimumAmount);
        Assert.Equal(50m, department.MaximumAmount);
    }

    [Fact]
    public void ADepartment_NeedsAName_AUniqueNumber_AHexColor_AndLimitsInOrder()
    {
        var existing = new[] { new PosDepartment { ItemId = "grocery", Name = "Grocery", Number = "1" } };

        Assert.Empty(PosDepartments.Validate(new PosDepartment { ItemId = "candy", Name = "Candy", Number = "2", Color = "#C2185B", MinimumAmount = 1m, MaximumAmount = 50m }, existing, S));
        Assert.Single(PosDepartments.Validate(new PosDepartment { ItemId = "candy" }, existing, S));
        Assert.Single(PosDepartments.Validate(new PosDepartment { ItemId = "candy", Name = "Candy", Number = "1" }, existing, S));
        Assert.Single(PosDepartments.Validate(new PosDepartment { ItemId = "candy", Name = "Candy", Color = "red" }, existing, S));
        Assert.Single(PosDepartments.Validate(new PosDepartment { ItemId = "candy", Name = "Candy", MinimumAmount = 10m, MaximumAmount = 5m }, existing, S));

        // Saving a department again doesn't clash with its own number.
        Assert.Empty(PosDepartments.Validate(new PosDepartment { ItemId = "grocery", Name = "Grocery", Number = "1" }, existing, S));
    }

    [Theory]
    [InlineData(10, true, null)]
    [InlineData(0, true, "Key in the amount to ring up in Candy.")]
    [InlineData(0.005, true, "Ring up Candy in whole cents.")]
    [InlineData(0.5, true, "Candy rings up at least 1.00.")]
    [InlineData(1000, true, "Candy rings up at most 50.00: check the amount.")]
    [InlineData(10, false, "Candy isn't rung up in any more.")]
    public void AnAmount_IsRungUpOnlyWithinTheDepartmentsLimits(decimal amount, bool enabled, string problem)
    {
        var department = new PosDepartment { Name = "Candy", MinimumAmount = 1m, MaximumAmount = 50m, IsEnabled = enabled };

        var actual = PosDepartments.CheckAmount(department, amount, currency: null, S);

        Assert.Equal(problem?.Replace("1.00", 1m.ToString("0.00")).Replace("50.00", 50m.ToString("0.00")), actual);
    }

    [Fact]
    public void ARegisterWithoutKeys_ShowsEveryDepartmentThatsOn_InTheirOrder()
    {
        var departments = new[]
        {
            new PosDepartment { ItemId = "grocery", Name = "Grocery", Position = 1 },
            new PosDepartment { ItemId = "tobacco", Name = "Tobacco", Position = 2, IsEnabled = false },
            new PosDepartment { ItemId = "candy", Name = "Candy", Position = 3 },
        };

        var keys = PosDepartments.GetKeys(new Register(), departments);

        Assert.Equal(["Grocery", "Candy"], keys.Select(key => key.Label));
        Assert.All(keys, key => Assert.Null(key.PresetAmount));
    }

    [Fact]
    public void ARegistersKeys_ShowInTheirOrder_WithTheirLabelsAndPresets_SkippingDepartmentsGoneOrOff()
    {
        var departments = new[]
        {
            new PosDepartment { ItemId = "grocery", Name = "Grocery" },
            new PosDepartment { ItemId = "tobacco", Name = "Tobacco", IsEnabled = false },
            new PosDepartment { ItemId = "candy", Name = "Candy" },
        };
        var register = new Register
        {
            Keys =
            [
                new RegisterKey { DepartmentId = "candy", Label = "Coffee", PresetAmount = 1.50m },
                new RegisterKey { DepartmentId = "tobacco" },
                new RegisterKey { DepartmentId = "gone" },
                new RegisterKey { DepartmentId = "grocery" },
            ],
        };

        var keys = PosDepartments.GetKeys(register, departments);

        Assert.Collection(
            keys,
            key =>
            {
                Assert.Equal("candy", key.Department.ItemId);
                Assert.Equal("Coffee", key.Label);
                Assert.Equal(1.50m, key.PresetAmount);
            },
            key =>
            {
                Assert.Equal("grocery", key.Department.ItemId);
                Assert.Equal("Grocery", key.Label);
                Assert.Null(key.PresetAmount);
            });
    }

    [Fact]
    public void AnAmountInADepartment_BecomesAnOrderLineTaxedByItsCategory_WithNoItemBehindIt()
    {
        var department = new PosDepartment { ItemId = "candy", Name = "Candy", Number = "4", TaxCategoryCode = "Candy" };
        var priced = PosDepartmentLines.Price(department, 1.50m, 3, "USD", new DateTime(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc));

        var line = Assert.Single(OrderLines.FromPricing(new CartPricing { Cart = new Cart { Lines = [priced.Line] }, Currency = "USD", Lines = [priced] }));

        Assert.True(PosDepartmentLines.IsDepartmentLine(line));
        Assert.Equal("candy", line.ContentItemId);
        Assert.Equal(PosConstants.DepartmentLineType, line.ContentType);
        Assert.Null(line.ContentItemVersionId);
        Assert.Equal("Candy", line.Title);
        Assert.Equal("4", line.Sku);
        Assert.Equal("Candy", line.TaxCategoryCode);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(1.50m, line.UnitPrice);
        Assert.Equal(4.50m, line.LineTotal);
        Assert.Empty(line.CategoryIds);
    }

    [Fact]
    public void AProductLine_IsntADepartmentLine()
        => Assert.False(PosDepartmentLines.IsDepartmentLine(new OrderLine { ContentItemId = "hat", ContentType = "Product" }));

    [Fact]
    public async Task AShift_CountsWhatEachDepartmentRangUp_AndTheTaxCollected()
    {
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.PageAsync(It.Is<OrderQuery>(query => query.ShiftId == "shift"), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((2, (IReadOnlyList<Order>)
            [
                new Order
                {
                    Subtotal = 14.50m,
                    TaxAmount = 1.16m,
                    Total = 15.66m,
                    Tenders = [new OrderTender { ProviderKey = PosConstants.CashTenderKey, Amount = 15.66m }],
                    Lines =
                    [
                        Line("candy", "Candy", 2, 2.25m),
                        Line("grocery", "Grocery", 1, 10m),
                        new OrderLine { ContentItemId = "hat", ContentType = "Product", Title = "Hat", Quantity = 1, LineTotal = 20m },
                    ],
                },
                new Order
                {
                    Subtotal = 3m,
                    TaxAmount = 0.24m,
                    Total = 3.24m,
                    Lines = [Line("candy", "Candy", 3, 1m)],
                },
            ]));

        var service = new PosShiftService(null, null, orders.Object, null, null, null, null, null, []);

        var summary = await service.SummarizeAsync(new RegisterShift { ItemId = "shift" }, TestContext.Current.CancellationToken);

        Assert.Equal(2, summary.Sales);
        Assert.Equal(1.40m, summary.Tax);
        Assert.Collection(
            summary.Departments,
            grocery =>
            {
                Assert.Equal("Grocery", grocery.Name);
                Assert.Equal(1, grocery.Quantity);
                Assert.Equal(10m, grocery.Total);
            },
            candy =>
            {
                Assert.Equal("Candy", candy.Name);
                Assert.Equal(5, candy.Quantity);
                Assert.Equal(7.50m, candy.Total);
            });
    }

    private static OrderLine Line(string departmentId, string name, int quantity, decimal unitPrice)
        => new()
        {
            ContentItemId = departmentId,
            ContentType = PosConstants.DepartmentLineType,
            Title = name,
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineTotal = quantity * unitPrice,
        };
}
