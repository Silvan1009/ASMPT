using Service.Api.Data.Entities;

namespace Service.Api.Tests;

/// <summary>Order graphs used by the export tests.</summary>
internal static class TestOrders
{
    /// <summary>
    /// An order shaped to exercise every branch of the export projection at once:
    /// <list type="bullet">
    /// <item>two boards, so board ordering is observable;</item>
    /// <item>one component instance placed on <em>both</em> boards — the many-to-many relationship the
    /// challenge describes, which must appear under each board in the exported file;</item>
    /// <item>names whose ordinal and case-insensitive orderings differ ("Zebra" sorts before "alpha"
    /// ordinally, because every upper-case letter does, but after it case-insensitively), so a test can tell
    /// the two comparers apart.</item>
    /// </list>
    /// </summary>
    public static Order WithSharedComponent(
        out Board alphaPanel,
        out Board zebraPanel,
        out Component sharedCapacitor)
    {
        sharedCapacitor = new Component { Id = Guid.NewGuid(), Name = "capacitor 100nF", Description = "Shared decoupling cap", Quantity = 12 };
        var resistor = new Component { Id = Guid.NewGuid(), Name = "Resistor 10k", Description = null, Quantity = 40 };

        zebraPanel = new Board { Id = Guid.NewGuid(), Name = "Zebra Panel", Description = "Main panel", Length = 120.5m, Width = 80.25m };
        zebraPanel.Components.Add(resistor);
        zebraPanel.Components.Add(sharedCapacitor);

        alphaPanel = new Board { Id = Guid.NewGuid(), Name = "alpha panel", Description = null, Length = 60m, Width = 40m };
        alphaPanel.Components.Add(sharedCapacitor);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            Name = "Alpha Run 7",
            Description = "First panel run",
            OrderDate = new DateOnly(2026, 3, 14),
        };
        order.Boards.Add(zebraPanel);
        order.Boards.Add(alphaPanel);
        return order;
    }

    /// <summary>The same graph when the individual entities do not need to be inspected.</summary>
    public static Order WithSharedComponent() => WithSharedComponent(out _, out _, out _);
}
