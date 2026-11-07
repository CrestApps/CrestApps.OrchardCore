using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.PointOfSale.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.PointOfSale.Indexes;

public sealed class RegisterIndex : CatalogItemIndex
{
    public string Name { get; set; }

    public string LocationId { get; set; }

    public bool IsEnabled { get; set; }
}

public sealed class RegisterIndexProvider : IndexProvider<Register>
{
    public RegisterIndexProvider()
    {
        CollectionName = PosConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Register> context)
        => context.For<RegisterIndex>()
            .Map(register => new RegisterIndex
            {
                ItemId = register.ItemId,
                Name = register.Name,
                LocationId = register.LocationId,
                IsEnabled = register.IsEnabled,
            });
}

public sealed class RegisterShiftIndex : CatalogItemIndex
{
    public string RegisterId { get; set; }

    public string LocationId { get; set; }

    public int Status { get; set; }

    public DateTime OpenedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user who opened the shift, so a privacy request finds it.
    /// </summary>
    public string OpenedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the user who closed the shift, so a privacy request finds it.
    /// </summary>
    public string ClosedByUserId { get; set; }
}

public sealed class RegisterShiftIndexProvider : IndexProvider<RegisterShift>
{
    public RegisterShiftIndexProvider()
    {
        CollectionName = PosConstants.CollectionName;
    }

    public override void Describe(DescribeContext<RegisterShift> context)
        => context.For<RegisterShiftIndex>()
            .Map(shift => new RegisterShiftIndex
            {
                ItemId = shift.ItemId,
                RegisterId = shift.RegisterId,
                LocationId = shift.LocationId,
                Status = (int)shift.Status,
                OpenedUtc = shift.OpenedUtc,
                OpenedByUserId = Truncate(shift.OpenedByUserId),
                ClosedByUserId = Truncate(shift.ClosedByUserId),
            });

    private static string Truncate(string userId)
        => userId?.Length > 26 ? userId[..26] : userId;
}
