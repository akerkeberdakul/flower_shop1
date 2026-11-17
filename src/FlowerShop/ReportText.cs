using System.Collections.Immutable;

namespace FlowerShop;

public static class ReportText
{
    public static ImmutableArray<string> Lines(ReportBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var lines = ImmutableArray.CreateBuilder<string>();

        lines.Add("FLOWER SHOP REPORTS");
        lines.Add("");
        lines.AddRange(Restock(
            $"REPORT 1 (volume-first scoring): restock list, stock <= {bundle.Settings.LowStockThreshold}, target {bundle.Settings.TargetStock}",
            bundle.VolumeFirst));
        lines.Add("");
        lines.AddRange(Restock(
            $"REPORT 1 (value-first scoring): restock list, stock <= {bundle.Settings.LowStockThreshold}, target {bundle.Settings.TargetStock}",
            bundle.ValueFirst));
        lines.Add("");
        var excluded = bundle.Settings.ExcludedFlowers;
        var exclusionNote = excluded is { Count: > 0 }
            ? $", excluding {string.Join(", ", excluded.Select(flower => flower.ToString()).OrderBy(name => name, StringComparer.Ordinal))}"
            : "";
        lines.AddRange(Revenue($"REPORT 2: revenue by flower (standard delivery){exclusionNote}", bundle.Revenue));

        return lines.ToImmutable();
    }

    public static ImmutableArray<string> Restock(string title, RestockReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add(title);
        lines.Add(FormattableString.Invariant($"{"Flower",-10} {"Stock",5} {"Order",6} {"Score",6} {"Cost(cents)",12}"));

        if (report.Lines.IsEmpty)
        {
            lines.Add("No items need restocking.");
        }

        foreach (var line in report.Lines)
        {
            lines.Add(FormattableString.Invariant(
                $"{line.Flower,-10} {line.Stock,5} {line.Quantity,6} {line.Score,6} {line.CostCents,12}"));
        }

        lines.Add(FormattableString.Invariant($"Total cost (cents): {report.TotalCostCents}"));
        return lines.ToImmutable();
    }

    public static ImmutableArray<string> Revenue(string title, ImmutableArray<FlowerRevenue> rows)
    {
        var lines = ImmutableArray.CreateBuilder<string>();
        lines.Add(title);
        lines.Add(FormattableString.Invariant($"{"Flower",-10} {"Orders",6} {"Stems",6} {"Revenue",12}"));

        if (rows.IsEmpty)
        {
            lines.Add("No quotes matched.");
        }

        foreach (var row in rows)
        {
            lines.Add(FormattableString.Invariant(
                $"{row.Flower,-10} {row.Orders,6} {row.Stems,6} {row.Revenue,12:F2}"));
        }

        lines.Add(FormattableString.Invariant($"Total revenue: {RevenueReport.TotalRevenue(rows):F2}"));
        return lines.ToImmutable();
    }
}
