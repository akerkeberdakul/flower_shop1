using System.Collections.Immutable;

namespace FlowerShop;

public sealed record ReportRun(ReportBundle Bundle, ImmutableArray<string> Lines, string ExportedTo);

public static class ReportApplication
{
    public static Result<ReportRun, AppError> Run(
        string? inventoryPath,
        string? ordersPath,
        string exportPath,
        IReorderCostCalculator calculator,
        ReportSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(exportPath);
        ArgumentNullException.ThrowIfNull(calculator);

        return InventoryFiles.Load(inventoryPath)
            .Bind(inventory => RequestLoader.Load(ordersPath)
                .Bind(orders => ReportWorkflow.Build(
                    inventory,
                    orders,
                    settings ?? ReportSettings.Default,
                    ShopRules.Default,
                    DeliveryPolicies.Standard,
                    calculator)))
            .Bind(bundle =>
            {
                var lines = ReportText.Lines(bundle);
                return ReportExporter.ExportToFile(exportPath, lines)
                    .Map(path => new ReportRun(bundle, lines, path));
            });
    }
}
