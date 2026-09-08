using Service.Api.Data.Entities;
using Service.Api.ErrorHandling;
using Service.Api.Services;
using Service.Api.Tests.Fakes;

namespace Service.Api.Tests;

/// <summary>
/// Tests for the shared handling of the related-entity id lists in requests — an order's boards, a board's
/// components. <see cref="RelatedEntities.SyncTo"/> in particular carries the update path's whole
/// correctness: its job is to leave already-present instances alone so EF Core deletes and inserts only the
/// join rows that actually changed, and nothing else in the codebase would notice if that stopped happening.
/// </summary>
public sealed class RelatedEntitiesTests
{
    private static Component Component(string name) => new() { Id = Guid.NewGuid(), Name = name, Quantity = 1 };

    [Fact]
    public void SyncTo_DropsEntitiesThatAreNoLongerWanted()
    {
        var keep = Component("keep");
        var drop = Component("drop");
        var target = new List<Component> { keep, drop };

        target.SyncTo([keep]);

        Assert.Equal([keep.Id], target.Select(c => c.Id));
    }

    [Fact]
    public void SyncTo_AddsEntitiesThatAreNewlyWanted()
    {
        var existing = Component("existing");
        var added = Component("added");
        var target = new List<Component> { existing };

        target.SyncTo([existing, added]);

        Assert.Equal([existing.Id, added.Id], target.Select(c => c.Id));
    }

    [Fact]
    public void SyncTo_KeepsTheInstanceAlreadyInTheList_SoUnchangedJoinRowsAreNotRewritten()
    {
        var tracked = Component("shared");
        var sameIdDifferentInstance = new Component { Id = tracked.Id, Name = tracked.Name, Quantity = tracked.Quantity };
        var target = new List<Component> { tracked };

        target.SyncTo([sameIdDifferentInstance]);

        // The distinction that matters: the tracked instance survives, so EF Core sees no change for this
        // relationship. Replacing it with the equal-but-different instance would delete and re-insert the
        // join row on every update. Assert.Same, not Assert.Equal — the ids are identical either way.
        Assert.Same(tracked, Assert.Single(target));
    }

    [Fact]
    public void SyncTo_EmptiesTheListWhenNothingIsWanted()
    {
        var target = new List<Component> { Component("a"), Component("b") };

        target.SyncTo([]);

        Assert.Empty(target);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsTheEntitiesForTheRequestedIds()
    {
        var first = Component("first");
        var second = Component("second");
        var repository = new LookupRepository<Component>(first, second);

        var resolved = await RelatedEntities.ResolveAsync(
            [first.Id, second.Id], repository, "ComponentIds", "component", CancellationToken.None);

        Assert.Equal([first.Id, second.Id], resolved.Select(c => c.Id));
    }

    [Fact]
    public async Task ResolveAsync_CollapsesDuplicateIdsBeforeQuerying()
    {
        var only = Component("only");
        var repository = new LookupRepository<Component>(only);

        var resolved = await RelatedEntities.ResolveAsync(
            [only.Id, only.Id, only.Id], repository, "ComponentIds", "component", CancellationToken.None);

        // Distinct() first, so a repeated id is neither queried twice nor counted as "missing" when the
        // repository returns a single row for it — and so the caller's SyncTo never sees a duplicate.
        Assert.Equal([only.Id], repository.RequestedIds);
        Assert.Equal([only.Id], resolved.Select(c => c.Id));
    }

    [Fact]
    public async Task ResolveAsync_ThrowsNamingTheMissingIdsAndTheRequestProperty()
    {
        var known = Component("known");
        var repository = new LookupRepository<Component>(known);
        var missing = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<RelatedEntitiesNotFoundException>(
            () => RelatedEntities.ResolveAsync(
                [known.Id, missing], repository, "ComponentIds", "component", CancellationToken.None));

        // The handler turns this into a 400 validation problem on the named property, so both parts are part
        // of the API contract rather than diagnostics.
        Assert.Equal("ComponentIds", exception.PropertyName);
        Assert.Contains(missing.ToString(), exception.Message);
        Assert.DoesNotContain(known.Id.ToString(), exception.Message);
    }
}
