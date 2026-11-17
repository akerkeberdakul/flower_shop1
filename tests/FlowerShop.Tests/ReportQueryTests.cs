using System.Collections.Immutable;
using FlowerShop;
using static FlowerShop.Tests.TestSupport;

namespace FlowerShop.Tests;

public class ReportQueryTests
{
    private static readonly Func<StockItem, int> VolumeFirst = Scoring.Urgency(50)(10)(0);
    private static readonly Func<StockItem, int> ValueFirst = Scoring.Urgency(50)(1)(5);

    private static readonly string[] OrderLines =
    {
        "O1,Aigerim,Rose,10,5,false",
        "O2,Dana,Tulip,20,0,true",
        "O3,Marat,Lily,5,20,false",
        "O4,Sara,Sunflower,4,3,false",
        "O5,Timur,Rose,8,25,false",
        "O6,Alina,Tulip,12,10,true"
    };

    private static ImmutableArray<StockItem> SampleStock() =>
        ImmutableArray.Create(
            new StockItem(FlowerType.Rose, 100),
            new StockItem(FlowerType.Lily, 8),
            new StockItem(FlowerType.Sunflower, 0));

    private static ImmutableArray<Quote> SampleQuotes()
    {
        var parsed = OrderParser.ParseAll(OrderLines);
        return OrderPipeline.Run(parsed.Orders, ShopRules.Default, DeliveryPolicies.Standard).Quotes;
    }

    private static Quote MakeQuote(string id, FlowerType flower, int quantity, decimal total) =>
        new(id, "Test", flower, quantity, total, 0m, 0m, total);

    [Fact]
    public void LowStockReport_NormalResult_FiltersProjectsAndRanks()
    {
        var candidates = ValueOf(StockReports.LowStock(SampleStock(), 10, 50, VolumeFirst));

        Assert.Equal(new[] { FlowerType.Sunflower, FlowerType.Lily }, candidates.Select(c => c.Flower).ToArray());
        Assert.Equal(new[] { 0, 8 }, candidates.Select(c => c.Stock).ToArray());
        Assert.Equal(new[] { 50, 42 }, candidates.Select(c => c.Shortage).ToArray());
        Assert.Equal(new[] { 500, 420 }, candidates.Select(c => c.Score).ToArray());
    }

    [Fact]
    public void LowStockReport_EmptyDataset_IsAnEmptySuccess()
    {
        var candidates = ValueOf(StockReports.LowStock(Array.Empty<StockItem>(), 10, 50, VolumeFirst));

        Assert.Empty(candidates);
    }

    [Fact]
    public void LowStockReport_NoMatchingItems_IsAnEmptySuccess()
    {
        var plentiful = new[] { new StockItem(FlowerType.Rose, 100), new StockItem(FlowerType.Lily, 11) };

        var candidates = ValueOf(StockReports.LowStock(plentiful, 10, 50, VolumeFirst));

        Assert.Empty(candidates);
    }

    [Fact]
    public void LowStockReport_BoundaryStockEqualToThreshold_IsIncluded()
    {
        var items = new[] { new StockItem(FlowerType.Rose, 10), new StockItem(FlowerType.Lily, 11) };

        var candidates = ValueOf(StockReports.LowStock(items, 10, 50, VolumeFirst));

        Assert.Equal(new[] { FlowerType.Rose }, candidates.Select(c => c.Flower).ToArray());
    }

    [Fact]
    public void LowStockReport_TiedScores_AreOrderedByNameWhateverTheInputOrder()
    {
        Func<StockItem, int> flat = _ => 7;
        var forward = new[]
        {
            new StockItem(FlowerType.Lily, 5),
            new StockItem(FlowerType.Rose, 5),
            new StockItem(FlowerType.Sunflower, 5)
        };
        var backward = Enumerable.Reverse(forward).ToArray();

        var first = ValueOf(StockReports.LowStock(forward, 10, 50, flat));
        var second = ValueOf(StockReports.LowStock(backward, 10, 50, flat));

        var expected = new[] { FlowerType.Lily, FlowerType.Rose, FlowerType.Sunflower };
        Assert.Equal(expected, first.Select(c => c.Flower).ToArray());
        Assert.Equal(expected, second.Select(c => c.Flower).ToArray());
    }

    [Fact]
    public void LowStockReport_InvalidDataAndSettings_AreExplicitFailures()
    {
        var negative = new[] { new StockItem(FlowerType.Rose, -1) };

        Assert.Equal("invalid_stock_data", ErrorOf(StockReports.LowStock(negative, 10, 50, VolumeFirst)).Code);
        Assert.Equal("invalid_report_settings", ErrorOf(StockReports.LowStock(SampleStock(), -1, 50, VolumeFirst)).Code);
        Assert.Equal("invalid_report_settings", ErrorOf(StockReports.LowStock(SampleStock(), 10, 10, VolumeFirst)).Code);
    }

    [Fact]
    public void RevenueReport_NormalResult_GroupsFlowersAndTotalsRevenue()
    {
        var rows = ValueOf(RevenueReport.ByFlower(SampleQuotes(), 0m));

        Assert.Equal(new[] { FlowerType.Tulip, FlowerType.Rose, FlowerType.Lily }, rows.Select(r => r.Flower).ToArray());
        Assert.Equal(new[] { 2, 1, 1 }, rows.Select(r => r.Orders).ToArray());
        Assert.Equal(new[] { 32, 10, 5 }, rows.Select(r => r.Stems).ToArray());
        Assert.Equal(new[] { 4208m, 2550m, 2200m }, rows.Select(r => r.Revenue).ToArray());
        Assert.Equal(8958m, RevenueReport.TotalRevenue(rows));
    }

    [Fact]
    public void RevenueReport_EmptyDataset_AndNoMatches_AreEmptySuccesses()
    {
        var fromNothing = ValueOf(RevenueReport.ByFlower(Array.Empty<Quote>(), 0m));
        var filteredOut = ValueOf(RevenueReport.ByFlower(SampleQuotes(), 99999m));

        Assert.Empty(fromNothing);
        Assert.Empty(filteredOut);
        Assert.Equal(0m, RevenueReport.TotalRevenue(fromNothing));
    }

    [Fact]
    public void RevenueReport_MinimumTotal_FiltersBeforeGrouping()
    {
        var rows = ValueOf(RevenueReport.ByFlower(SampleQuotes(), 2100m));

        Assert.Equal(3, rows.Length);
        var tulip = rows.Single(r => r.Flower == FlowerType.Tulip);
        Assert.Equal(1, tulip.Orders);
        Assert.Equal(2168m, tulip.Revenue);
    }

    [Fact]
    public void RevenueReport_TiedRevenue_IsOrderedByFlowerNameWhateverTheInputOrder()
    {
        var quotes = new[]
        {
            MakeQuote("A", FlowerType.Rose, 5, 1000m),
            MakeQuote("B", FlowerType.Lily, 5, 1000m),
            MakeQuote("C", FlowerType.Sunflower, 5, 1000m)
        };

        var forward = ValueOf(RevenueReport.ByFlower(quotes, 0m));
        var backward = ValueOf(RevenueReport.ByFlower(Enumerable.Reverse(quotes).ToArray(), 0m));

        var expected = new[] { FlowerType.Lily, FlowerType.Rose, FlowerType.Sunflower };
        Assert.Equal(expected, forward.Select(r => r.Flower).ToArray());
        Assert.Equal(expected, backward.Select(r => r.Flower).ToArray());
    }

    [Fact]
    public void RevenueReport_InvalidData_IsAnExplicitFailure()
    {
        var negativeTotal = new[] { MakeQuote("A", FlowerType.Rose, 5, -1m) };
        var zeroQuantity = new[] { MakeQuote("B", FlowerType.Rose, 0, 10m) };

        Assert.Equal("invalid_quote_data", ErrorOf(RevenueReport.ByFlower(negativeTotal, 0m)).Code);
        Assert.Equal("invalid_quote_data", ErrorOf(RevenueReport.ByFlower(zeroQuantity, 0m)).Code);
        Assert.Equal("invalid_report_settings", ErrorOf(RevenueReport.ByFlower(SampleQuotes(), -1m)).Code);
    }

    [Fact]
    public void ConfiguredFunctions_SameInput_GiveDocumentedDifferentResults()
    {
        var scoreForTarget = Scoring.Urgency(50);
        var volumeFirst = scoreForTarget(10)(0);
        var valueFirst = scoreForTarget(1)(5);
        var lily = new StockItem(FlowerType.Lily, 8);
        var sunflower = new StockItem(FlowerType.Sunflower, 0);

        Assert.Equal(420, volumeFirst(lily));
        Assert.Equal(500, volumeFirst(sunflower));
        Assert.Equal(942, valueFirst(lily));
        Assert.Equal(550, valueFirst(sunflower));

        var byVolume = ValueOf(StockReports.LowStock(SampleStock(), 10, 50, volumeFirst));
        var byValue = ValueOf(StockReports.LowStock(SampleStock(), 10, 50, valueFirst));
        Assert.Equal(new[] { FlowerType.Sunflower, FlowerType.Lily }, byVolume.Select(c => c.Flower).ToArray());
        Assert.Equal(new[] { FlowerType.Lily, FlowerType.Sunflower }, byValue.Select(c => c.Flower).ToArray());
    }

    [Fact]
    public void CurriedFunction_AcceptsArgumentsOneAtATime_AndRejectsInvalidConfiguration()
    {
        var sunflower = new StockItem(FlowerType.Sunflower, 0);

        Assert.Equal(500, Scoring.Urgency(50)(10)(0)(sunflower));
        Assert.Equal(0, Scoring.Urgency(50)(10)(0)(new StockItem(FlowerType.Rose, 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Scoring.Urgency(50)(-1)(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Scoring.Urgency(-1)(1)(1));
    }

    [Fact]
    public void RankByExtension_OrdersByScoreDescending_ThenByNameOrdinal()
    {
        var words = new[] { "dd", "a", "ccc", "bb" };

        var ranked = words.RankBy(word => word.Length, word => word).ToArray();
        var reversed = Enumerable.Reverse(words).RankBy(word => word.Length, word => word).ToArray();

        Assert.Equal(new[] { "ccc", "bb", "dd", "a" }, ranked);
        Assert.Equal(ranked, reversed);
        Assert.Equal(new[] { "dd", "a", "ccc", "bb" }, words);
    }

    [Fact]
    public void CollectExtension_GathersSuccesses_AndStopsAtTheFirstFailure()
    {
        var error = AppError.Domain("third", "The third item failed.");
        var evaluated = 0;
        var results = Enumerable.Range(1, 5).Select(n =>
        {
            evaluated++;
            return n == 3 ? Result<int, AppError>.Fail(error) : Result<int, AppError>.Ok(n);
        });

        var failed = results.Collect();

        Assert.Equal(error, ErrorOf(failed));
        Assert.Equal(3, evaluated);

        var allGood = Enumerable.Range(1, 3).Select(n => Result<int, AppError>.Ok(n * 10)).Collect();
        Assert.Equal(new[] { 10, 20, 30 }, ValueOf(allGood).ToArray());
        Assert.Empty(ValueOf(Array.Empty<Result<int, AppError>>().Collect()));
    }

    [Fact]
    public void InventoryItems_AreOrderedByNameWhateverTheInsertionOrder()
    {
        var first = Inventory.FromItems(new[]
        {
            new StockItem(FlowerType.Sunflower, 1),
            new StockItem(FlowerType.Rose, 2),
            new StockItem(FlowerType.Lily, 3)
        });
        var second = Inventory.FromItems(Enumerable.Reverse(first.Items));

        var expected = new[] { FlowerType.Lily, FlowerType.Rose, FlowerType.Sunflower };
        Assert.Equal(expected, first.Items.Select(i => i.Flower).ToArray());
        Assert.Equal(expected, second.Items.Select(i => i.Flower).ToArray());
    }

    [Fact]
    public void ReportText_FormatsRowsTotalsAndEmptyReports()
    {
        var rows = ValueOf(RevenueReport.ByFlower(SampleQuotes(), 0m));
        var empty = new RestockReport(ImmutableArray<ReorderLine>.Empty, 0);

        var revenueLines = ReportText.Revenue("REVENUE", rows);
        var restockLines = ReportText.Restock("RESTOCK", empty);

        Assert.Equal("REVENUE", revenueLines[0]);
        Assert.Equal("Tulip           2     32      4208.00", revenueLines[2]);
        Assert.Equal("Total revenue: 8958.00", revenueLines[^1]);
        Assert.Contains("No items need restocking.", restockLines);
        Assert.Equal("Total cost (cents): 0", restockLines[^1]);
    }
    [Fact]
    public void RevenueReport_ExcludedCategory_IsLeftOutAndTheOtherGroupsAreUnchanged()
    {
        var all = ValueOf(RevenueReport.ByFlower(SampleQuotes(), 0m));
        var withoutLily = ValueOf(RevenueReport.ByFlower(SampleQuotes(), 0m, new[] { FlowerType.Lily }));

        Assert.Equal(new[] { FlowerType.Tulip, FlowerType.Rose }, withoutLily.Select(r => r.Flower).ToArray());
        Assert.Equal(new[] { 4208m, 2550m }, withoutLily.Select(r => r.Revenue).ToArray());
        Assert.Equal(6758m, RevenueReport.TotalRevenue(withoutLily));
        Assert.Equal(
            all.Single(r => r.Flower == FlowerType.Tulip),
            withoutLily.Single(r => r.Flower == FlowerType.Tulip));
    }

    [Fact]
    public void RevenueReport_ExcludingEverythingOrNothing_BehavesAsExpected()
    {
        var everything = new[] { FlowerType.Rose, FlowerType.Tulip, FlowerType.Lily, FlowerType.Sunflower };

        var none = ValueOf(RevenueReport.ByFlower(SampleQuotes(), 0m, Array.Empty<FlowerType>()));
        var all = ValueOf(RevenueReport.ByFlower(SampleQuotes(), 0m, everything));

        Assert.Equal(3, none.Length);
        Assert.Empty(all);
        Assert.Equal(0m, RevenueReport.TotalRevenue(all));
    }

    [Fact]
    public void RevenueReport_GroupsReturnCategoryCountAndSum()
    {
        var quotes = new[]
        {
        MakeQuote("A", FlowerType.Rose, 5, 1000m),
        MakeQuote("B", FlowerType.Rose, 7, 1500m),
        MakeQuote("C", FlowerType.Lily, 5, 900m)
    };

        var rows = ValueOf(RevenueReport.ByFlower(quotes, 0m));

        Assert.Equal(new FlowerRevenue(FlowerType.Rose, 2, 12, 2500m), rows[0]);
        Assert.Equal(new FlowerRevenue(FlowerType.Lily, 1, 5, 900m), rows[1]);
    }
}
