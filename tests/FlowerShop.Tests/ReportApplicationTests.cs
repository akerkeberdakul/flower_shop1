using FlowerShop;
using static FlowerShop.Tests.TestSupport;

namespace FlowerShop.Tests;

public class ReportApplicationTests
{
    private const string InventoryCsv = "Flower,Stock\nRose,100\nLily,8\nSunflower,0\n";

    private const string InventoryJson =
        "[{\"flower\":\"Rose\",\"stock\":100},{\"flower\":\"Lily\",\"stock\":8},{\"flower\":\"Sunflower\",\"stock\":0}]";

    private const string OrdersCsv =
        "OrderId,Customer,Flower,Quantity,DistanceKm,IsMember\n" +
        "O1,Aigerim,Rose,10,5,false\n" +
        "O2,Dana,Tulip,20,0,true\n" +
        "O3,Marat,Lily,5,20,false\n" +
        "O4,Sara,Sunflower,4,3,false\n" +
        "O5,Timur,Rose,8,25,false\n" +
        "O6,Alina,Tulip,12,10,true\n";

    private sealed class FailingCalculator : IReorderCostCalculator
    {
        public Result<long, AppError> CostCents(int quantity, int unitCostCents) =>
            Result<long, AppError>.Fail(AppError.Operational("native_unavailable", "The native library is missing."));
    }

    [Fact]
    public void NormalWorkflow_ReportsAreBuiltWithTheRealNativeCallAndExported()
    {
        using var temp = new TempDirectory();
        var inventory = temp.Write("inventory.csv", InventoryCsv);
        var orders = temp.Write("orders.csv", OrdersCsv);
        var export = temp.PathFor("report.txt");

        var run = ValueOf(ReportApplication.Run(inventory, orders, export, new NativeReorderCostCalculator()));

        var bundle = run.Bundle;
        Assert.Equal(new[] { FlowerType.Sunflower, FlowerType.Lily }, bundle.VolumeFirst.Lines.Select(l => l.Flower).ToArray());
        Assert.Equal(new[] { FlowerType.Lily, FlowerType.Sunflower }, bundle.ValueFirst.Lines.Select(l => l.Flower).ToArray());
        Assert.Equal(584100L, bundle.VolumeFirst.TotalCostCents);
        Assert.Equal(584100L, bundle.ValueFirst.TotalCostCents);
        Assert.Equal(new[] { FlowerType.Tulip, FlowerType.Rose, FlowerType.Lily }, bundle.Revenue.Select(r => r.Flower).ToArray());
        Assert.Equal(new[] { 4208m, 2550m, 2200m }, bundle.Revenue.Select(r => r.Revenue).ToArray());

        Assert.Equal(export, run.ExportedTo);
        var written = File.ReadAllLines(export);
        Assert.Equal(run.Lines.ToArray(), written);
        Assert.Contains("Total cost (cents): 584100", written);
        Assert.Contains("Total revenue: 8958.00", written);
        Assert.False(File.Exists(export + ".tmp"));
    }

    [Fact]
    public void NormalWorkflow_JsonInventory_GivesTheSameReports()
    {
        using var temp = new TempDirectory();
        var csvRun = ValueOf(ReportApplication.Run(
            temp.Write("inventory.csv", InventoryCsv),
            temp.Write("orders.csv", OrdersCsv),
            temp.PathFor("from_csv.txt"),
            new NativeReorderCostCalculator()));

        var jsonRun = ValueOf(ReportApplication.Run(
            temp.Write("inventory.json", InventoryJson),
            temp.PathFor("orders.csv"),
            temp.PathFor("from_json.txt"),
            new NativeReorderCostCalculator()));

        Assert.Equal(csvRun.Lines.ToArray(), jsonRun.Lines.ToArray());
    }

    [Fact]
    public void FailurePath_NativeFailure_ReportsTheProblemAndLeavesEveryFileUnchanged()
    {
        using var temp = new TempDirectory();
        var inventory = temp.Write("inventory.csv", InventoryCsv);
        var orders = temp.Write("orders.csv", OrdersCsv);
        var export = temp.Write("report.txt", "PREVIOUS REPORT");

        var result = ReportApplication.Run(inventory, orders, export, new FailingCalculator());

        var error = ErrorOf(result);
        Assert.Equal(ErrorKind.Operational, error.Kind);
        Assert.Equal("native_unavailable", error.Code);
        Assert.Equal("PREVIOUS REPORT", File.ReadAllText(export));
        Assert.Equal(InventoryCsv, File.ReadAllText(inventory));
        Assert.Equal(OrdersCsv, File.ReadAllText(orders));
        Assert.False(File.Exists(export + ".tmp"));
    }

    [Fact]
    public void FileBoundaries_MissingInventory_IsASafeFailureAndNothingIsExported()
    {
        using var temp = new TempDirectory();
        var orders = temp.Write("orders.csv", OrdersCsv);
        var export = temp.PathFor("report.txt");

        var result = ReportApplication.Run(temp.PathFor("missing.csv"), orders, export, new NativeReorderCostCalculator());

        Assert.Equal("file_not_found", ErrorOf(result).Code);
        Assert.False(File.Exists(export));
    }

    [Fact]
    public void FileBoundaries_MissingOrders_IsASafeFailureAndNothingIsExported()
    {
        using var temp = new TempDirectory();
        var inventory = temp.Write("inventory.csv", InventoryCsv);
        var export = temp.PathFor("report.txt");

        var result = ReportApplication.Run(inventory, temp.PathFor("missing_orders.csv"), export, new NativeReorderCostCalculator());

        Assert.Equal("file_not_found", ErrorOf(result).Code);
        Assert.False(File.Exists(export));
    }

    [Fact]
    public void FileBoundaries_MalformedContent_IsASafeFailureAndNothingIsExported()
    {
        using var temp = new TempDirectory();
        var goodOrders = temp.Write("orders.csv", OrdersCsv);
        var badInventory = temp.Write("inventory.csv", "Flower,Stock\nRose,abc\n");
        var goodInventory = temp.Write("inventory_ok.csv", InventoryCsv);
        var badOrders = temp.Write("orders_bad.csv", "OrderId,Customer,Flower,Quantity,DistanceKm,IsMember\nQ1,A,Orchid,10,5,false\n");
        var exportOne = temp.PathFor("one.txt");
        var exportTwo = temp.PathFor("two.txt");

        var inventoryProblem = ReportApplication.Run(badInventory, goodOrders, exportOne, new NativeReorderCostCalculator());
        var ordersProblem = ReportApplication.Run(goodInventory, badOrders, exportTwo, new NativeReorderCostCalculator());

        Assert.Equal("malformed_content", ErrorOf(inventoryProblem).Code);
        Assert.Equal("malformed_content", ErrorOf(ordersProblem).Code);
        Assert.False(File.Exists(exportOne));
        Assert.False(File.Exists(exportTwo));
    }

    [Fact]
    public void FailurePath_ExportToAMissingFolder_IsReportedAsExportFailure()
    {
        using var temp = new TempDirectory();
        var inventory = temp.Write("inventory.csv", InventoryCsv);
        var orders = temp.Write("orders.csv", OrdersCsv);
        var export = Path.Combine(temp.PathFor("no_such_folder"), "report.txt");

        var result = ReportApplication.Run(inventory, orders, export, new NativeReorderCostCalculator());

        Assert.Equal("export_failed", ErrorOf(result).Code);
        Assert.Equal(InventoryCsv, File.ReadAllText(inventory));
    }

    [Fact]
    public void ExcludedCategory_IsLeftOutOfTheRevenueReportAndTheExportedFile()
    {
        using var temp = new TempDirectory();
        var inventory = temp.Write("inventory.csv", InventoryCsv);
        var orders = temp.Write("orders.csv", OrdersCsv);
        var export = temp.PathFor("report.txt");
        var settings = ReportSettings.Default with { ExcludedFlowers = new[] { FlowerType.Lily } };

        var run = ValueOf(ReportApplication.Run(inventory, orders, export, new NativeReorderCostCalculator(), settings));

        Assert.Equal(new[] { FlowerType.Tulip, FlowerType.Rose }, run.Bundle.Revenue.Select(r => r.Flower).ToArray());
        Assert.Equal(new[] { FlowerType.Sunflower, FlowerType.Lily }, run.Bundle.VolumeFirst.Lines.Select(l => l.Flower).ToArray());
        var written = File.ReadAllLines(export);
        Assert.Contains("REPORT 2: revenue by flower (standard delivery), excluding Lily", written);
        Assert.Contains("Total revenue: 6758.00", written);
    }

    [Fact]
    public void ExclusionArgument_ParsesNamesAndRejectsUnknownFlowers()
    {
        var parsed = ValueOf(ExclusionArgument.Parse(" lily, Rose ,lily"));
        var empty = ValueOf(ExclusionArgument.Parse(null));
        var error = ErrorOf(ExclusionArgument.Parse("Lily,Orchid"));

        Assert.Equal(new[] { FlowerType.Lily, FlowerType.Rose }, parsed.ToArray());
        Assert.Empty(empty);
        Assert.Equal("invalid_argument", error.Code);
        Assert.Contains("Orchid", error.Message);
    }
}
