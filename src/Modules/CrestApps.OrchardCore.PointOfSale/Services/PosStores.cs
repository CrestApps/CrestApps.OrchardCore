using CrestApps.Core.Services;
using CrestApps.OrchardCore.PointOfSale.Indexes;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.PointOfSale.Services;

public interface IRegisterStore : ICatalog<Register>
{
    Task<(int Count, IReadOnlyList<Register> Registers)> PageAsync(int skip, int take, CancellationToken cancellationToken = default);
}

public sealed class RegisterStore : DocumentCatalog<Register, RegisterIndex>, IRegisterStore
{
    public RegisterStore(ISession session)
        : base(session)
    {
        CollectionName = PosConstants.CollectionName;
    }

    public async Task<(int Count, IReadOnlyList<Register> Registers)> PageAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<Register, RegisterIndex>(collection: CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderBy(index => index.Name).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }
}

public interface IRegisterShiftStore : ICatalog<RegisterShift>
{
    /// <summary>
    /// Returns the register's open shift, or <see langword="null"/>.
    /// </summary>
    Task<RegisterShift> FindOpenAsync(string registerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a page of shifts, newest first: a register's, or every register's.
    /// </summary>
    Task<(int Count, IReadOnlyList<RegisterShift> Shifts)> PageAsync(string registerId, int skip, int take, CancellationToken cancellationToken = default);
}

public sealed class RegisterShiftStore : DocumentCatalog<RegisterShift, RegisterShiftIndex>, IRegisterShiftStore
{
    // Two staff closing a shift, or a sale and the close, mustn't both write a stale tally.
    protected override bool CheckConcurrency => true;

    public RegisterShiftStore(ISession session)
        : base(session)
    {
        CollectionName = PosConstants.CollectionName;
    }

    public Task<RegisterShift> FindOpenAsync(string registerId, CancellationToken cancellationToken = default)
    {
        var open = (int)RegisterShiftStatus.Open;

        return Session.Query<RegisterShift, RegisterShiftIndex>(index => index.RegisterId == registerId && index.Status == open, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(int Count, IReadOnlyList<RegisterShift> Shifts)> PageAsync(string registerId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrEmpty(registerId)
            ? Session.Query<RegisterShift, RegisterShiftIndex>(collection: CollectionName)
            : Session.Query<RegisterShift, RegisterShiftIndex>(index => index.RegisterId == registerId, collection: CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.OpenedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }
}
