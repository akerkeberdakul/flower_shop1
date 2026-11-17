namespace FlowerShop;

public static class InventoryFiles
{
    public static Result<Inventory, AppError> Load(string? path) =>
        path is not null && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? InventoryJsonLoader.Load(path)
            : InventoryLoader.Load(path);
}
