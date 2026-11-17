using System.Text.Json;

namespace FlowerShop;

public static class InventoryJsonLoader
{
    public static Result<Inventory, AppError> Load(string? path) =>
        FileBoundary.ReadLines(path, "inventory")
            .Bind(lines => Parse(string.Join("\n", lines), path ?? ""));

    public static Result<Inventory, AppError> Parse(string? json, string source)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure("malformed_json", source, "the file is empty.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return Failure("malformed_json", source, $"invalid JSON ({ex.Message})");
        }

        using (document)
        {
            return ReadItems(document.RootElement, source);
        }
    }

    private static Result<Inventory, AppError> ReadItems(JsonElement root, string source)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            return Failure("malformed_content", source, "the top level must be an array of {\"flower\", \"stock\"} objects.");
        }

        var items = new List<StockItem>();
        var problems = new List<string>();
        var seen = new HashSet<FlowerType>();
        var index = 0;

        foreach (var element in root.EnumerateArray())
        {
            index++;

            if (element.ValueKind != JsonValueKind.Object)
            {
                problems.Add($"Item {index}: must be an object.");
                continue;
            }

            if (!element.TryGetProperty("flower", out var flowerProperty) || flowerProperty.ValueKind != JsonValueKind.String)
            {
                problems.Add($"Item {index}: 'flower' must be text.");
                continue;
            }

            var flowerName = flowerProperty.GetString() ?? "";
            if (!FlowerCatalog.TryParse(flowerName, out var flower))
            {
                problems.Add($"Item {index}: unknown flower '{flowerName}'.");
                continue;
            }

            if (!element.TryGetProperty("stock", out var stockProperty)
                || stockProperty.ValueKind != JsonValueKind.Number
                || !stockProperty.TryGetInt32(out var stock)
                || stock < 0)
            {
                problems.Add($"Item {index}: 'stock' must be a whole number of 0 or more.");
                continue;
            }

            if (!seen.Add(flower))
            {
                problems.Add($"Item {index}: duplicate flower '{flower}'.");
                continue;
            }

            items.Add(new StockItem(flower, stock));
        }

        return problems.Count > 0
            ? Failure("malformed_content", source, string.Join(" ", problems))
            : Result<Inventory, AppError>.Ok(Inventory.FromItems(items));
    }

    private static Result<Inventory, AppError> Failure(string code, string source, string details) =>
        Result<Inventory, AppError>.Fail(
            AppError.Operational(code, $"Inventory file '{source}' is malformed: {details}"));
}
