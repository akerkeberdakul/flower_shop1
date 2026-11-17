namespace FlowerShop;

public static class ReportCommand
{
    public static int Run(string[] args)
    {
        var inventoryPath = DataPaths.Resolve(args.Length > 0 ? args[0] : Path.Combine("data", "inventory.csv"));
        var ordersPath = DataPaths.Resolve(args.Length > 1 ? args[1] : Path.Combine("data", "orders.csv"));
        var exportPath = args.Length > 2 ? args[2] : "report_output.txt";

        var outcome = ExclusionArgument.Parse(args.Length > 3 ? args[3] : null)
            .Bind(excluded => ReportApplication.Run(
                inventoryPath,
                ordersPath,
                exportPath,
                new NativeReorderCostCalculator(),
                ReportSettings.Default with { ExcludedFlowers = excluded }));

        return outcome.Match(
            run =>
            {
                foreach (var line in run.Lines)
                {
                    Console.WriteLine(line);
                }

                Console.WriteLine();
                Console.WriteLine($"Report exported to {run.ExportedTo}");
                return 0;
            },
            error =>
            {
                Console.WriteLine($"ERROR ({error.Kind}) [{error.Code}] {error.Message}");
                return 1;
            });
    }
}
