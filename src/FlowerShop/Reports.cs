using System.Collections.Immutable;

namespace FlowerShop;

public sealed record ReorderCandidate(FlowerType Flower, int Stock, int Shortage, int Score);

public sealed record FlowerRevenue(FlowerType Flower, int Orders, int Stems, decimal Revenue);

public static class StockReports
{
    public static Result<ImmutableArray<ReorderCandidate>, AppError> LowStock(
        IEnumerable<StockItem> items,
        int threshold,
        int targetStock,
        Func<StockItem, int> score)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(score);

        if (threshold < 0 || targetStock <= threshold)
        {
            return Result<ImmutableArray<ReorderCandidate>, AppError>.Fail(AppError.Domain(
                "invalid_report_settings",
                $"The threshold must be 0 or more and below the target stock (threshold {threshold}, target {targetStock})."));
        }

        var snapshot = items.ToImmutableArray();

        var invalid = snapshot.FirstOrDefault(item => item.Stock < 0);
        if (invalid is not null)
        {
            return Result<ImmutableArray<ReorderCandidate>, AppError>.Fail(AppError.Domain(
                "invalid_stock_data",
                $"Stock of {invalid.Flower} is negative ({invalid.Stock})."));
        }

        var candidates = snapshot
            .Where(item => item.Stock <= threshold)
            .Select(item => new ReorderCandidate(item.Flower, item.Stock, targetStock - item.Stock, score(item)))
            .RankBy(candidate => candidate.Score, candidate => candidate.Flower.ToString())
            .ToImmutableArray();

        return Result<ImmutableArray<ReorderCandidate>, AppError>.Ok(candidates);
    }
}

public static class RevenueReport
{
    public static Result<ImmutableArray<FlowerRevenue>, AppError> ByFlower(
        IEnumerable<Quote> quotes,
        decimal minimumTotal,
        IReadOnlyCollection<FlowerType>? excluded = null)
    {
        ArgumentNullException.ThrowIfNull(quotes);

        if (minimumTotal < 0)
        {
            return Result<ImmutableArray<FlowerRevenue>, AppError>.Fail(AppError.Domain(
                "invalid_report_settings",
                $"The minimum quote total cannot be negative ({minimumTotal})."));
        }

        var snapshot = quotes.ToImmutableArray();

        var invalid = snapshot.FirstOrDefault(quote => quote.Total < 0 || quote.Quantity <= 0);
        if (invalid is not null)
        {
            return Result<ImmutableArray<FlowerRevenue>, AppError>.Fail(AppError.Domain(
                "invalid_quote_data",
                $"Quote {invalid.OrderId} has a negative total or a quantity below 1."));
        }

        var rows = snapshot
            .Where(quote => quote.Total >= minimumTotal)
            .Where(quote => excluded is null || !excluded.Contains(quote.Flower))
            .GroupBy(quote => quote.Flower)
            .Select(group => new FlowerRevenue(
                group.Key,
                group.Count(),
                group.Sum(quote => quote.Quantity),
                group.Sum(quote => quote.Total)))
            .OrderByDescending(row => row.Revenue)
            .ThenBy(row => row.Flower.ToString(), StringComparer.Ordinal)
            .ToImmutableArray();

        return Result<ImmutableArray<FlowerRevenue>, AppError>.Ok(rows);
    }

    public static decimal TotalRevenue(IEnumerable<FlowerRevenue> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return rows.Aggregate(0m, (total, row) => total + row.Revenue);
    }
}
