using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Service.Api.Data.Entities;
using Service.Api.Models;
using Service.Api.Services;
using Service.Api.Tests.Fakes;

namespace Service.Api.Tests;

/// <summary>
/// Tests for <see cref="OrderService.ExportAsync"/> — the simulated hand-off of an order to a production
/// line, and the one place the application's data becomes an interoperable JSON document.
///
/// The service is exercised through <see cref="IOrderRepository"/> only, with no database: that is the point
/// of the repository abstraction. The board repository handed in throws on every member, so these tests also
/// prove the export never issues a second query for the boards.
/// </summary>
public sealed class OrderServiceExportTests
{
    /// <summary>Fixed instant so the exported <c>GeneratedAt</c> stamp is deterministic.</summary>
    private static readonly DateTimeOffset ExportedAt = new(2026, 9, 8, 14, 32, 7, TimeSpan.Zero);

    /// <summary>Matches the writer's <see cref="JsonSerializerDefaults.Web"/> options, so the assertions read
    /// the file the way a production-line consumer would.</summary>
    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    private static OrderService CreateService(Order? exportable, out StubOrderRepository orders)
    {
        orders = new StubOrderRepository(exportable);
        return new OrderService(
            orders,
            new ThrowingBoardRepository(),
            new FakeTimeProvider(ExportedAt),
            NullLogger<OrderService>.Instance);
    }

    private static OrderExportDto Read(OrderExportFile file)
        => JsonSerializer.Deserialize<OrderExportDto>(file.Content, ReadOptions)
           ?? throw new InvalidOperationException("The exported file did not deserialize into an order.");

    [Fact]
    public async Task ExportAsync_ReturnsNull_WhenTheOrderDoesNotExist()
    {
        var service = CreateService(exportable: null, out var orders);
        var missingId = Guid.NewGuid();

        var file = await service.ExportAsync(missingId);

        // Null is the contract the controller turns into a 404 rather than an empty download.
        Assert.Null(file);
        Assert.Equal(missingId, orders.RequestedExportId);
    }

    [Fact]
    public async Task ExportAsync_WritesTheOrderHeaderFields()
    {
        var order = TestOrders.WithSharedComponent();
        var service = CreateService(order, out _);

        var export = Read((await service.ExportAsync(order.Id))!);

        Assert.Equal(order.Id, export.Id);
        Assert.Equal("Alpha Run 7", export.Name);
        Assert.Equal("First panel run", export.Description);
        Assert.Equal(new DateOnly(2026, 3, 14), export.OrderDate);
    }

    [Fact]
    public async Task ExportAsync_EmbedsEachBoardWithItsComponents_RatherThanReferences()
    {
        var order = TestOrders.WithSharedComponent(out _, out var zebraPanel, out _);
        var service = CreateService(order, out _);

        var export = Read((await service.ExportAsync(order.Id))!);

        // The file has to stand on its own on the production line: the whole graph, not ids to look up.
        var zebra = Assert.Single(export.Boards, b => b.Id == zebraPanel.Id);
        Assert.Equal("Zebra Panel", zebra.Name);
        Assert.Equal("Main panel", zebra.Description);
        Assert.Equal(120.5m, zebra.Length);
        Assert.Equal(80.25m, zebra.Width);

        var resistor = Assert.Single(zebra.Components, c => c.Name == "Resistor 10k");
        Assert.Null(resistor.Description);
        Assert.Equal(40, resistor.Quantity);
    }

    [Fact]
    public async Task ExportAsync_RepeatsAComponentThatIsPlacedOnMoreThanOneBoard()
    {
        var order = TestOrders.WithSharedComponent(out var alphaPanel, out var zebraPanel, out var sharedCapacitor);
        var service = CreateService(order, out _);

        var export = Read((await service.ExportAsync(order.Id))!);

        // The Board <-> Component relationship is many-to-many, so one component instance belongs under every
        // board that carries it. Flattening it to a single occurrence would under-report what the line builds.
        var alpha = Assert.Single(export.Boards, b => b.Id == alphaPanel.Id);
        var zebra = Assert.Single(export.Boards, b => b.Id == zebraPanel.Id);

        var onAlpha = Assert.Single(alpha.Components, c => c.Id == sharedCapacitor.Id);
        var onZebra = Assert.Single(zebra.Components, c => c.Id == sharedCapacitor.Id);
        Assert.Equal(sharedCapacitor.Quantity, onAlpha.Quantity);
        Assert.Equal(sharedCapacitor.Quantity, onZebra.Quantity);
    }

    [Fact]
    public async Task ExportAsync_OrdersBoardsAndComponentsByNameIgnoringCase()
    {
        var order = TestOrders.WithSharedComponent();
        var service = CreateService(order, out _);

        var export = Read((await service.ExportAsync(order.Id))!);

        // "alpha panel" was added second and sorts *after* "Zebra Panel" under an ordinal comparison (every
        // upper-case letter precedes every lower-case one), so these assertions fail if the comparer is
        // dropped or swapped for the default. Stable output also keeps two exports of an unchanged order
        // byte-identical.
        Assert.Equal(["alpha panel", "Zebra Panel"], export.Boards.Select(b => b.Name));
        Assert.Equal(
            ["capacitor 100nF", "Resistor 10k"],
            export.Boards.Single(b => b.Name == "Zebra Panel").Components.Select(c => c.Name));
    }

    [Fact]
    public async Task ExportAsync_StampsGeneratedAtFromTheInjectedClock()
    {
        var order = TestOrders.WithSharedComponent();
        var service = CreateService(order, out _);

        var export = Read((await service.ExportAsync(order.Id))!);

        // Reads the injected TimeProvider, not DateTime.UtcNow — which is what makes this assertion possible.
        Assert.Equal(ExportedAt, export.GeneratedAt);
    }

    [Fact]
    public async Task ExportAsync_NamesTheFileAfterTheOrder()
    {
        var order = TestOrders.WithSharedComponent();
        var service = CreateService(order, out _);

        var file = await service.ExportAsync(order.Id);

        Assert.Equal("order-alpha-run-7.json", file!.FileName);
    }

    [Theory]
    [InlineData("///")]          // every character is invalid in a file name
    [InlineData("   ")]          // trimmed away to nothing
    public async Task ExportAsync_FallsBackToTheOrderId_WhenTheNameLeavesNoUsableCharacters(string name)
    {
        var order = TestOrders.WithSharedComponent();
        order.Name = name;
        var service = CreateService(order, out _);

        var file = await service.ExportAsync(order.Id);

        // A blank file name would be rejected by the browser, so the id stands in.
        Assert.Equal($"order-{order.Id}.json", file!.FileName);
    }

    [Fact]
    public async Task ExportAsync_WritesCamelCasedIndentedJson()
    {
        var order = TestOrders.WithSharedComponent();
        var service = CreateService(order, out _);

        var json = Encoding.UTF8.GetString((await service.ExportAsync(order.Id))!.Content);

        // The requirement is interoperability, so the wire format is part of the contract, not an
        // implementation detail: camelCase names (JsonSerializerDefaults.Web) and a readable layout.
        Assert.Contains("\"orderDate\":", json);
        Assert.Contains("\"generatedAt\":", json);
        Assert.DoesNotContain("\"OrderDate\":", json);
        Assert.Contains('\n', json);
        Assert.Equal("2026-03-14", Read(new OrderExportFile("x", Encoding.UTF8.GetBytes(json))).OrderDate.ToString("yyyy-MM-dd"));
    }
}
