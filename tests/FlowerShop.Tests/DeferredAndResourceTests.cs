using System.Collections.Immutable;
using System.Text;
using FlowerShop;
using static FlowerShop.Tests.TestSupport;

namespace FlowerShop.Tests;

public class DeferredAndResourceTests
{
    private static Inventory RoseStock() => Inventory.FromItems(new[] { new StockItem(FlowerType.Rose, 100) });

    private static List<Order> ThreeRoseOrders() => new()
    {
        new Order("A", "One", FlowerType.Rose, 10, 5, false),
        new Order("B", "Two", FlowerType.Rose, 12, 5, false),
        new Order("C", "Three", FlowerType.Rose, 5, 0, true)
    };

    private sealed class TrackingReader : StringReader
    {
        public TrackingReader(string text) : base(text) { }

        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposed = true;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class FailingReader : TextReader
    {
        public bool Disposed { get; private set; }

        public override string? ReadLine() => throw new IOException("The disk failed.");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposed = true;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class TrackingStream : MemoryStream
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposed = true;
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingStream : Stream
    {
        public bool Disposed { get; private set; }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new IOException("The disk is full.");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disposed = true;
            }

            base.Dispose(disposing);
        }
    }

    private static IEnumerable<string> BrokenLines()
    {
        yield return "partial line";
        throw new InvalidOperationException("The source failed.");
    }

    [Fact]
    public void DeferredExecution_CreatingTheEnumerable_DoesNotProcessAnything()
    {
        var policyCalls = 0;
        var sourceReads = 0;
        Func<Order, decimal> countingPolicy = _ =>
        {
            policyCalls++;
            return 0m;
        };
        var source = ThreeRoseOrders().Select(order =>
        {
            sourceReads++;
            return order;
        });

        var quotes = LazyQuotes.Generate(source, RoseStock(), ShopRules.Default, countingPolicy);

        Assert.Equal(0, policyCalls);
        Assert.Equal(0, sourceReads);
        Assert.NotNull(quotes);
    }

    [Fact]
    public void DeferredExecution_IterationProcessesOneItemAtATime()
    {
        var policyCalls = 0;
        Func<Order, decimal> countingPolicy = _ =>
        {
            policyCalls++;
            return 0m;
        };
        var quotes = LazyQuotes.Generate(ThreeRoseOrders(), RoseStock(), ShopRules.Default, countingPolicy);

        using var enumerator = quotes.GetEnumerator();
        Assert.Equal(0, policyCalls);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, policyCalls);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(2, policyCalls);
    }

    [Fact]
    public void DeferredExecution_RepeatedEnumerationRepeatsWork_ButASnapshotDoesNot()
    {
        var policyCalls = 0;
        Func<Order, decimal> countingPolicy = _ =>
        {
            policyCalls++;
            return 0m;
        };
        var quotes = LazyQuotes.Generate(ThreeRoseOrders(), RoseStock(), ShopRules.Default, countingPolicy);

        var firstPass = quotes.ToArray();
        Assert.Equal(3, policyCalls);
        var secondPass = quotes.ToArray();
        Assert.Equal(6, policyCalls);

        var snapshot = quotes.ToImmutableArray();
        Assert.Equal(9, policyCalls);
        var totals = snapshot.Select(result => ValueOf(result).Total).ToArray();
        var again = snapshot.Count(result => result.IsSuccess);
        Assert.Equal(9, policyCalls);
        Assert.Equal(3, again);
        Assert.Equal(3, totals.Length);
        Assert.Equal(firstPass.Length, secondPass.Length);
    }

    [Fact]
    public void DeferredExecution_ArgumentsAreCheckedEagerly()
    {
        Assert.Throws<ArgumentNullException>(() =>
            LazyQuotes.Generate(null!, RoseStock(), ShopRules.Default, DeliveryPolicies.Standard));
    }

    [Fact]
    public void LineStream_OpensTheResourceOnlyWhenEnumerated_AndReleasesItAfterFullIteration()
    {
        var opened = 0;
        TrackingReader? reader = null;
        var lines = LineStream.Read(() =>
        {
            opened++;
            reader = new TrackingReader("first\nsecond");
            return reader;
        });

        Assert.Equal(0, opened);
        Assert.Null(reader);

        var all = lines.ToArray();

        Assert.Equal(1, opened);
        Assert.Equal(new[] { "first", "second" }, all);
        Assert.NotNull(reader);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void LineStream_StoppingEarly_StillReleasesTheResource()
    {
        var reader = new TrackingReader("first\nsecond\nthird");
        var lines = LineStream.Read(() => reader);

        string? first;
        using (var enumerator = lines.GetEnumerator())
        {
            Assert.True(enumerator.MoveNext());
            first = enumerator.Current;
            Assert.False(reader.Disposed);
        }

        Assert.Equal("first", first);
        Assert.True(reader.Disposed);
    }

    [Fact]
    public void LineStream_WhenReadingFails_TheExceptionPropagatesAndTheResourceIsReleased()
    {
        var reader = new FailingReader();

        Assert.Throws<IOException>(() => LineStream.Read(() => reader).ToArray());

        Assert.True(reader.Disposed);
    }

    [Fact]
    public void LineStream_ReadFile_ReleasesTheFileHandleAfterAnEarlyStop()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("lines.txt", "one\ntwo\nthree\n");

        string? first;
        using (var enumerator = LineStream.ReadFile(path).GetEnumerator())
        {
            Assert.True(enumerator.MoveNext());
            first = enumerator.Current;
        }

        File.Delete(path);

        Assert.Equal("one", first);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void LineStream_ReadFile_MissingFileFailsOnlyWhenEnumerated()
    {
        using var temp = new TempDirectory();
        var lines = LineStream.ReadFile(temp.PathFor("missing.txt"));

        Assert.Throws<FileNotFoundException>(() => lines.ToArray());
    }

    [Fact]
    public void ExportWrite_Success_ReleasesTheStreamAndWritesUtf8WithoutBom()
    {
        var stream = new TrackingStream();

        var result = ReportExporter.Write(() => stream, new[] { "alpha", "beta" }, "memory");

        Assert.Equal("memory", ValueOf(result));
        Assert.True(stream.Disposed);
        var text = new UTF8Encoding(false).GetString(stream.ToArray());
        Assert.Equal("alpha" + Environment.NewLine + "beta" + Environment.NewLine, text);
    }

    [Fact]
    public void ExportWrite_WhenWritingFails_ReportsAFailureAndStillReleasesTheStream()
    {
        var stream = new ThrowingStream();

        var result = ReportExporter.Write(() => stream, new[] { "alpha" }, "broken-target");

        var error = ErrorOf(result);
        Assert.Equal(ErrorKind.Operational, error.Kind);
        Assert.Equal("export_failed", error.Code);
        Assert.Contains("broken-target", error.Message);
        Assert.True(stream.Disposed);
    }

    [Fact]
    public void ExportToFile_WritesTheFile_AndLeavesNoTemporaryFile()
    {
        using var temp = new TempDirectory();
        var path = temp.PathFor("report.txt");

        var result = ReportExporter.ExportToFile(path, new[] { "one", "two" });

        Assert.Equal(path, ValueOf(result));
        Assert.Equal(new[] { "one", "two" }, File.ReadAllLines(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void ExportToFile_MissingFolder_IsAFailureAndCreatesNothing()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.PathFor("no_such_folder"), "report.txt");

        var error = ErrorOf(ReportExporter.ExportToFile(path, new[] { "one" }));

        Assert.Equal("export_failed", error.Code);
        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(temp.PathFor("no_such_folder")));
    }

    [Fact]
    public void ExportToFile_BlankPath_IsAnInvalidPathFailure()
    {
        Assert.Equal("invalid_path", ErrorOf(ReportExporter.ExportToFile("  ", new[] { "one" })).Code);
    }

    [Fact]
    public void ExportToFile_WhenTheSourceFailsMidway_KeepsThePreviousFileAndRemovesTheTemporaryOne()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("report.txt", "PREVIOUS");

        Assert.Throws<InvalidOperationException>(() => ReportExporter.ExportToFile(path, BrokenLines()));

        Assert.Equal("PREVIOUS", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void ExportToFile_ReplacesAnExistingFileOnSuccess()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("report.txt", "PREVIOUS");

        ValueOf(ReportExporter.ExportToFile(path, new[] { "NEW" }));

        Assert.Equal(new[] { "NEW" }, File.ReadAllLines(path));
    }
}
