using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Service.Api.Data.Entities;
using Service.Api.Repositories;

namespace Service.Api.Tests.Fakes;

/// <summary>
/// Base for the hand-written repository stubs. Every member throws, so a test only overrides the calls the
/// method under test is actually supposed to make — an unexpected call then fails the test loudly instead of
/// quietly returning <c>null</c> or an empty list and turning a real defect into a passing assertion.
/// </summary>
internal abstract class StubEntityRepository<TEntity> : IEntityRepository<TEntity>
    where TEntity : class, IEntity
{
    public virtual Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        => throw Unexpected();

    public virtual Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => throw Unexpected();

    public virtual Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => throw Unexpected();

    public virtual Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => throw Unexpected();

    public virtual void Update(TEntity entity) => throw Unexpected();

    public virtual void Remove(TEntity entity) => throw Unexpected();

    public virtual Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => throw Unexpected();

    public virtual Task<IReadOnlyList<TEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => throw Unexpected();

    public virtual Task<int> DeleteByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => throw Unexpected();

    protected NotSupportedException Unexpected([CallerMemberName] string? member = null)
        => new($"{GetType().Name}.{member} was not expected to be called by this test.");
}

/// <summary>
/// <see cref="IOrderRepository"/> stub serving a single canned order to <see cref="GetForExportAsync"/>.
/// Records the id it was asked for so a test can assert the service passes the caller's id through.
/// </summary>
internal sealed class StubOrderRepository(Order? exportable = null) : StubEntityRepository<Order>, IOrderRepository
{
    /// <summary>The id passed to the last <see cref="GetForExportAsync"/> call, or null if never called.</summary>
    public Guid? RequestedExportId { get; private set; }

    public Task<IReadOnlyList<Order>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        => throw Unexpected();

    public Task<Order?> GetWithBoardsAsync(Guid id, CancellationToken cancellationToken = default)
        => throw Unexpected();

    public Task<Order?> GetForExportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RequestedExportId = id;
        return Task.FromResult(exportable?.Id == id ? exportable : null);
    }
}

/// <summary>
/// <see cref="IBoardRepository"/> stub on which every member throws. Passing it to the service under test is
/// itself an assertion: exporting an order must not go back to the board repository, because
/// <c>GetForExportAsync</c> already returns the whole graph in one query.
/// </summary>
internal sealed class ThrowingBoardRepository : StubEntityRepository<Board>, IBoardRepository
{
    public Task<IReadOnlyList<Board>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        => throw Unexpected();

    public Task<Board?> GetWithRelationsAsync(Guid id, CancellationToken cancellationToken = default)
        => throw Unexpected();
}

/// <summary>
/// <see cref="IEntityRepository{TEntity}"/> stub backed by an in-memory list, for the id-resolution tests.
/// Only <see cref="GetByIdsAsync"/> is implemented; it records the ids it was handed.
/// </summary>
internal sealed class LookupRepository<TEntity>(params TEntity[] entities) : StubEntityRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>The ids passed to the last <see cref="GetByIdsAsync"/> call, in order.</summary>
    public IReadOnlyCollection<Guid> RequestedIds { get; private set; } = [];

    public override Task<IReadOnlyList<TEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        RequestedIds = ids;
        return Task.FromResult<IReadOnlyList<TEntity>>(entities.Where(e => ids.Contains(e.Id)).ToList());
    }
}
