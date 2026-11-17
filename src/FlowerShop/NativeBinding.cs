using System.Runtime.InteropServices;

namespace FlowerShop;

public static class NativeMethods
{
    public const string LibraryName = "flowerlib";

    [DllImport(LibraryName, EntryPoint = "fs_reorder_cost_cents", CallingConvention = CallingConvention.Cdecl)]
    public static extern long ReorderCostCents(int quantity, int unitCostCents);
}

public interface IReorderCostCalculator
{
    Result<long, AppError> CostCents(int quantity, int unitCostCents);
}

public sealed class NativeReorderCostCalculator : IReorderCostCalculator
{
    public const int MaxQuantity = 1_000_000;
    public const int MaxUnitCostCents = 1_000_000;

    private readonly Func<int, int, long> _binding;

    public NativeReorderCostCalculator() : this(NativeMethods.ReorderCostCents) { }

    public NativeReorderCostCalculator(Func<int, int, long> binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _binding = binding;
    }

    public Result<long, AppError> CostCents(int quantity, int unitCostCents)
    {
        if (quantity < 1 || quantity > MaxQuantity)
        {
            return Fail(AppError.Domain(
                "invalid_reorder_input",
                $"The reorder quantity must be between 1 and {MaxQuantity} (got {quantity})."));
        }

        if (unitCostCents < 1 || unitCostCents > MaxUnitCostCents)
        {
            return Fail(AppError.Domain(
                "invalid_reorder_input",
                $"The unit cost must be between 1 and {MaxUnitCostCents} cents (got {unitCostCents})."));
        }

        try
        {
            var cents = _binding(quantity, unitCostCents);

            return cents < 0
                ? Fail(AppError.Operational(
                    "native_rejected",
                    $"The native function rejected the input ({quantity}, {unitCostCents}) and returned {cents}."))
                : Result<long, AppError>.Ok(cents);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return Fail(AppError.Operational(
                "native_unavailable",
                $"The native library '{NativeMethods.LibraryName}' could not be used on {RuntimeInformation.OSDescription} ({RuntimeInformation.ProcessArchitecture}): {ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static Result<long, AppError> Fail(AppError error) => Result<long, AppError>.Fail(error);
}
