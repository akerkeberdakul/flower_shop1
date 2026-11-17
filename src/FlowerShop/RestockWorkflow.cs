using System.Collections.Immutable;

namespace FlowerShop;

public sealed record ReorderLine(FlowerType Flower, int Stock, int Quantity, int Score, long CostCents);

public sealed record RestockReport(ImmutableArray<ReorderLine> Lines, long TotalCostCents);

public static class RestockWorkflow
{
    public static Result<RestockReport, AppError> Run(
        IEnumerable<StockItem> items,
        int threshold,
        int targetStock,
        Func<StockItem, int> score,
        IReorderCostCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);

        return StockReports.LowStock(items, threshold, targetStock, score)
            .Bind(candidates => candidates
                .Select(candidate => PriceLine(candidate, calculator))
                .Collect())
            .Map(lines => new RestockReport(lines, lines.Sum(line => line.CostCents)));
    }

    private static Result<ReorderLine, AppError> PriceLine(ReorderCandidate candidate, IReorderCostCalculator calculator) =>
        calculator.CostCents(candidate.Shortage, FlowerCatalog.WholesaleCostCents(candidate.Flower))
            .Map(cost => new ReorderLine(candidate.Flower, candidate.Stock, candidate.Shortage, candidate.Score, cost));
}
