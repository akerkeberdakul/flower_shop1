using System.Collections.Immutable;

namespace FlowerShop;

public sealed record ReportSettings(
    int LowStockThreshold,
    int TargetStock,
    decimal MinimumQuoteTotal,
    IReadOnlyCollection<FlowerType>? ExcludedFlowers = null)
{
    public static ReportSettings Default { get; } = new(10, 50, 0m);
}

public sealed record ReportBundle(
    ReportSettings Settings,
    RestockReport VolumeFirst,
    RestockReport ValueFirst,
    ImmutableArray<FlowerRevenue> Revenue);

public static class ReportWorkflow
{
    public static Result<ReportBundle, AppError> Build(
        Inventory inventory,
        ImmutableArray<Order> orders,
        ReportSettings settings,
        ShopRules rules,
        Func<Order, decimal> deliveryPolicy,
        IReorderCostCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(deliveryPolicy);
        ArgumentNullException.ThrowIfNull(calculator);

        if (orders.IsDefault)
        {
            throw new ArgumentException("Orders were not provided.", nameof(orders));
        }

        if (settings.TargetStock < 0)
        {
            return Result<ReportBundle, AppError>.Fail(AppError.Domain(
                "invalid_report_settings",
                $"The target stock cannot be negative ({settings.TargetStock})."));
        }

        var items = inventory.Items;
        var scoreForTarget = Scoring.Urgency(settings.TargetStock);
        var volumeFirst = scoreForTarget(10)(0);
        var valueFirst = scoreForTarget(1)(5);
        var quotes = OrderPipeline.Run(orders, rules, deliveryPolicy).Quotes;

        return RestockWorkflow.Run(items, settings.LowStockThreshold, settings.TargetStock, volumeFirst, calculator)
            .Bind(volume => RestockWorkflow.Run(items, settings.LowStockThreshold, settings.TargetStock, valueFirst, calculator)
                .Bind(value => RevenueReport.ByFlower(quotes, settings.MinimumQuoteTotal, settings.ExcludedFlowers)
                    .Map(revenue => new ReportBundle(settings, volume, value, revenue))));
    }
}
