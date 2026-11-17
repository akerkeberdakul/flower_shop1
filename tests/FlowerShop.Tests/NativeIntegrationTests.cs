using System.Collections.Immutable;
using System.Runtime.InteropServices;
using FlowerShop;
using static FlowerShop.Tests.TestSupport;

namespace FlowerShop.Tests;

public class NativeIntegrationTests
{
    [DllImport("flowerlib_missing_for_tests", EntryPoint = "fs_reorder_cost_cents", CallingConvention = CallingConvention.Cdecl)]
    private static extern long MissingLibraryCall(int quantity, int unitCostCents);

    private sealed class StubCalculator : IReorderCostCalculator
    {
        private readonly Func<int, int, Result<long, AppError>> _implementation;

        public StubCalculator(Func<int, int, Result<long, AppError>> implementation)
        {
            _implementation = implementation;
        }

        public int Calls { get; private set; }

        public Result<long, AppError> CostCents(int quantity, int unitCostCents)
        {
            Calls++;
            return _implementation(quantity, unitCostCents);
        }
    }

    private static ImmutableArray<StockItem> SampleStock() =>
        ImmutableArray.Create(
            new StockItem(FlowerType.Rose, 100),
            new StockItem(FlowerType.Lily, 8),
            new StockItem(FlowerType.Sunflower, 0));

    [Fact]
    public void RealNativeCall_ReturnsTheManuallyCalculatedValues()
    {
        Assert.Equal(114000L, NativeMethods.ReorderCostCents(20, 6000));
        Assert.Equal(405000L, NativeMethods.ReorderCostCents(50, 9000));
        Assert.Equal(114000L, NativeMethods.ReorderCostCents(19, 6000));
        Assert.Equal(99770L, NativeMethods.ReorderCostCents(21, 5001));
        Assert.Equal(900000000000L, NativeMethods.ReorderCostCents(1_000_000, 1_000_000));
    }

    [Fact]
    public void RealNativeCall_InvalidInput_ReturnsMinusOne()
    {
        Assert.Equal(-1L, NativeMethods.ReorderCostCents(0, 6000));
        Assert.Equal(-1L, NativeMethods.ReorderCostCents(-5, 6000));
        Assert.Equal(-1L, NativeMethods.ReorderCostCents(1_000_001, 6000));
        Assert.Equal(-1L, NativeMethods.ReorderCostCents(20, 0));
        Assert.Equal(-1L, NativeMethods.ReorderCostCents(20, 1_000_001));
    }

    [Fact]
    public void Adapter_WithTheRealLibrary_ReturnsAnOkResult()
    {
        var calculator = new NativeReorderCostCalculator();

        Assert.Equal(359100L, ValueOf(calculator.CostCents(42, 9000)));
        Assert.Equal(225000L, ValueOf(calculator.CostCents(50, 5000)));
    }

    [Fact]
    public void Adapter_ValidatesDomainInputBeforeCallingNative()
    {
        var nativeCalls = 0;
        var calculator = new NativeReorderCostCalculator((quantity, cost) =>
        {
            nativeCalls++;
            return 1;
        });

        var zeroQuantity = ErrorOf(calculator.CostCents(0, 6000));
        var tooMany = ErrorOf(calculator.CostCents(NativeReorderCostCalculator.MaxQuantity + 1, 6000));
        var zeroCost = ErrorOf(calculator.CostCents(10, 0));

        Assert.Equal(ErrorKind.Domain, zeroQuantity.Kind);
        Assert.Equal("invalid_reorder_input", zeroQuantity.Code);
        Assert.Equal("invalid_reorder_input", tooMany.Code);
        Assert.Equal("invalid_reorder_input", zeroCost.Code);
        Assert.Equal(0, nativeCalls);
    }

    [Fact]
    public void Adapter_RealLoadFailure_BecomesAClearOperationalFailure()
    {
        var calculator = new NativeReorderCostCalculator((quantity, cost) => MissingLibraryCall(quantity, cost));

        var error = ErrorOf(calculator.CostCents(10, 6000));

        Assert.Equal(ErrorKind.Operational, error.Kind);
        Assert.Equal("native_unavailable", error.Code);
        Assert.Contains("flowerlib", error.Message);
        Assert.Contains("DllNotFoundException", error.Message);
    }

    [Fact]
    public void Adapter_BindingFailures_AreTranslated()
    {
        var missingEntryPoint = new NativeReorderCostCalculator((_, _) => throw new EntryPointNotFoundException("no export"));
        var wrongArchitecture = new NativeReorderCostCalculator((_, _) => throw new BadImageFormatException("wrong architecture"));
        var rejecting = new NativeReorderCostCalculator((_, _) => -1);

        Assert.Equal("native_unavailable", ErrorOf(missingEntryPoint.CostCents(10, 6000)).Code);
        Assert.Equal("native_unavailable", ErrorOf(wrongArchitecture.CostCents(10, 6000)).Code);
        Assert.Equal("native_rejected", ErrorOf(rejecting.CostCents(10, 6000)).Code);
    }

    [Fact]
    public void RestockWorkflow_WithTheRealLibrary_UsesTheNativeCostInTheReport()
    {
        var report = ValueOf(RestockWorkflow.Run(
            SampleStock(),
            10,
            50,
            Scoring.Urgency(50)(10)(0),
            new NativeReorderCostCalculator()));

        Assert.Equal(new[] { FlowerType.Sunflower, FlowerType.Lily }, report.Lines.Select(l => l.Flower).ToArray());
        Assert.Equal(new[] { 50, 42 }, report.Lines.Select(l => l.Quantity).ToArray());
        Assert.Equal(new[] { 225000L, 359100L }, report.Lines.Select(l => l.CostCents).ToArray());
        Assert.Equal(584100L, report.TotalCostCents);
    }

    [Fact]
    public void RestockWorkflow_WhenTheCalculatorFails_ReturnsTheFirstFailureWithoutFurtherCalls()
    {
        var failure = AppError.Operational("native_unavailable", "The library is missing.");
        var calculator = new StubCalculator((_, _) => Result<long, AppError>.Fail(failure));

        var result = RestockWorkflow.Run(SampleStock(), 10, 50, Scoring.Urgency(50)(10)(0), calculator);

        Assert.Equal(failure, ErrorOf(result));
        Assert.Equal(1, calculator.Calls);
    }

    [Fact]
    public void RestockWorkflow_InvalidSettings_NeverReachTheCalculator()
    {
        var calculator = new StubCalculator((_, _) => Result<long, AppError>.Ok(1));

        var result = RestockWorkflow.Run(SampleStock(), -5, 50, Scoring.Urgency(50)(10)(0), calculator);

        Assert.Equal("invalid_report_settings", ErrorOf(result).Code);
        Assert.Equal(0, calculator.Calls);
    }
}
