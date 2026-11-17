# Flower Shop – Order Quote Pipeline (Assignments 4, 6 and 7 extended in one project)

## 1. Topic, users, team

- **Topic:** quotes for flower-shop orders.
- **Intended users:** the shop owner/florist, who loads a batch of orders and gets a priced quote for each, ranked by total.
- **Team:** `[Name Surname, Group]` – list every member and their part (see section 8).

## 2. Workflow

Input file (CSV) -> parse and validate -> **Stage 1** select eligible orders -> **Stage 2** price orders (delivery policy supplied from outside) -> **Stage 3** build the quote summary -> print.

**Input fields** (`data/orders.csv`, first line is the header):

| Field | Rule |
| --- | --- |
| OrderId | required, unique |
| Customer | required |
| Flower | Rose, Tulip, Lily or Sunflower (case-insensitive) |
| Quantity | whole number > 0 |
| DistanceKm | whole number >= 0 (0 = pick-up in the shop) |
| IsMember | `true` or `false` |

**Business rules**

- Unit prices: Rose 200, Tulip 120, Lily 180, Sunflower 100.
- Minimum order: **5 stems** (5 is accepted, 4 is rejected).
- Maximum delivery distance: **20 km** (20 is accepted, 21 is rejected).
- Bulk discount: **10%** of the flower subtotal when quantity >= **20**.
- Member discount: **5%** of the flower subtotal. Bulk and member discounts add up.
- Delivery fee (the configurable rule, 0 km is always free):
  - **Standard policy:** 300 + 50 per km
  - **Express policy:** 600 + 100 per km
- Total = subtotal - discount + delivery fee. Quotes are sorted by total (highest first), then by OrderId.

## 3. Dataset and expected results (written before running)

| Id | Customer | Flower | Qty | Km | Member | Purpose |
| --- | --- | --- | --- | --- | --- | --- |
| O1 | Aigerim | Rose | 10 | 5 | no | normal; delivery differs per policy |
| O2 | Dana | Tulip | 20 | 0 | yes | bulk boundary (20), pick-up (0 km), 15% discount |
| O3 | Marat | Lily | 5 | 20 | no | boundary: min quantity and max distance, accepted |
| O4 | Sara | Sunflower | 4 | 3 | no | rejected: quantity below 5 |
| O5 | Timur | Rose | 8 | 25 | no | rejected: distance above 20 km |
| O6 | Alina | Tulip | 12 | 10 | yes | member discount |

**Standard policy**

| Order | Subtotal | Discount | Delivery | Total |
| --- | --- | --- | --- | --- |
| O1 | 2000 | 0 | 550 | **2550** |
| O3 | 900 | 0 | 1300 | **2200** |
| O6 | 1440 | 72 | 800 | **2168** |
| O2 | 2400 | 360 | 0 | **2040** |

Grand total **8958**. Rejected: O4, O5.

**Express policy**

| Order | Subtotal | Discount | Delivery | Total |
| --- | --- | --- | --- | --- |
| O3 | 900 | 0 | 2600 | **3500** |
| O1 | 2000 | 0 | 1100 | **3100** |
| O6 | 1440 | 72 | 1600 | **2968** |
| O2 | 2400 | 360 | 0 | **2040** |

Grand total **11608**. Rejected: O4, O5. The ranking changes: O1 is first under Standard, O3 is first under Express.

## 4. Pipeline stages (`src/FlowerShop/OrderPipeline.cs`)

| Stage | Method | Input -> Output | Rule | Example |
| --- | --- | --- | --- | --- |
| 1 | `SelectEligible` | `ImmutableArray<Order>` + `ShopRules` -> `SelectionResult` | Keep Quantity >= 5 and DistanceKm <= 20; the rest get a rejection reason | O3 (5 stems, 20 km) is kept; O4 (4 stems) is rejected |
| 2 | `PriceOrders` | `SelectionResult` + `ShopRules` + `Func<Order, decimal>` -> `PricingResult` | subtotal = qty x unit price; discount = subtotal x (bulk + member rate); delivery fee from the delegate | O6: 1440 - 72 + 800 = 2168 (Standard) |
| 3 | `BuildSummary` | `PricingResult` -> `QuoteSummary` | Sort by total desc, then id; sum the grand total | Standard: O1, O3, O6, O2; total 8958 |

`OrderPipeline.Run` composes them with the small helper `Then`:
`orders.Then(SelectEligible).Then(PriceOrders).Then(BuildSummary)` (written with lambdas in the code).

**Trace of O6** (Alina, Tulip, 12, 10 km, member): `Order` -> stage 1 accepts it (12 >= 5, 10 <= 20) -> stage 2 creates `PricedOrder(subtotal 1440, discount 72, fee 800)` -> stage 3 creates `Quote(..., Total 2168)`.

**Higher-order function:** `PriceOrders` takes `Func<Order, decimal> deliveryPolicy` and calls it with `deliveryPolicy(order)`. The policies are in `DeliveryPolicies.cs` (`Standard`, `Express`, built by `FlatPlusPerKm`, which captures two immutable numbers). `Program.cs` chooses the policy and passes it in; there is no mutable global setting.

**Purity:** stages only read their arguments and return new immutable values (records and `ImmutableArray`). No Console, files, clock or random values are used inside them. File reading and printing happen only in `Program.cs`.

## 5. Requirement -> code map

| Requirement | Where |
| --- | --- |
| Typed immutable models | `Models.cs` (records) |
| Parsing and validation at the boundary | `OrderParser.cs` (`TryParse`, `ParseAll`) |
| Three pure stages and composition | `OrderPipeline.cs` (`SelectEligible`, `PriceOrders`, `BuildSummary`, `Run`) |
| Higher-order function and two policies | `PriceOrders`, `DeliveryPolicies.cs` |
| Policy chosen outside the pipeline | `Program.cs` |
| Output kept outside the core | `ReportFormatter.cs`, `Program.cs` |
| Tests | `tests/FlowerShop.Tests/OrderPipelineTests.cs` |

## 6. Build, run, test

- **Prerequisite:** .NET SDK 8.0 or newer. Working directory: the folder containing `FlowerShop.sln`.

```
dotnet build
dotnet run --project src/FlowerShop -- data/orders.csv
dotnet run --project src/FlowerShop -- data/orders_invalid.csv
dotnet run --project src/FlowerShop -- data/orders_empty.csv
dotnet test
```

- `orders.csv`: both policies, results as in section 3.
- `orders_invalid.csv`: prints one message per bad line (unknown flower, non-numeric quantity, missing customer, negative distance, wrong field count, bad boolean), then still prices the valid order B1 (Standard 2550, Express 3100). Exit code 2.
- `orders_empty.csv`: header only = valid empty dataset; prints "no orders", no quotes, grand total 0.00.

## 7. Tests (xUnit: 96 test methods = 122 cases counting theory rows; see sections 9.8 and 10.10)

| Required case | Tests |
| --- | --- |
| Normal workflow | `NormalWorkflow_StandardPolicy_...`, `NormalWorkflow_ExpressPolicy_...` |
| Empty dataset | `EmptyDataset_ParsesAsValidEmptyBatch`, `EmptyDataset_ProducesEmptySummary...` |
| Boundary value | `Boundary_ExactMinimum...`, `Boundary_JustOutsideLimits...`, `Boundary_BulkThreshold...` |
| Invalid and missing input | `InvalidOrMissingLine_...` (9 rows), `ZeroDistanceAndFalseMembership...`, `ParseAll_ReportsLineNumbers...`, `MissingCollections_AreRejected` |
| Two policies | `TwoPolicies_SameInput_...`, `HigherOrderStage_InvokesTheSuppliedDelegate...`, `PolicyReturningNegativeFee...` |
| Purity and stages | `Stage1_...DoesNotChangeCallerOwnedInput`, `Stage2_...`, `Stage3_...`, `SameInputAndPolicy_...` |

**Latest test outcome:** `[paste the result of dotnet test here after running it]`

## 8. Team contributions

| Member (name, group) | Logic | Tests | Documentation |
| --- | --- | --- | --- |
| `[Name, Group]` | `[e.g. parser and stage 1]` | `[...]` | `[...]` |

## 9. Assignment 6 – Exception handling in FP (Result, Maybe, Map, Bind, file boundary)

The earlier pipeline (`OrderPipeline`, `DeliveryPolicies`) is unchanged and still runs with `dotnet run --project src/FlowerShop -- data/orders.csv`.
Assignment 6 adds a **quote workflow** on top of it: a customer request is checked against the shop **inventory** (loaded from a CSV file), and the result is a quote or a clear failure.

### 9.1 Workflow, input, output

- **Input:** `data/inventory.csv` (`Flower,Stock`) and `data/quote_requests.csv` (same columns as the orders file).
- **Chain** (`QuoteWorkflow.RequestQuote`, pure code, no files, no Console):
  `inventory.Find` (Maybe) -> `ToResult` -> `Bind(CheckRequest)` -> `Bind(PriceWithPipeline)` -> `Map(QuoteDisplay.From)`
- **Boundary** (`QuoteCommand`): loads both files, runs the chain for each request, and handles every final outcome once with `Match`.

### 9.2 What each type means

| Type | Meaning |
| --- | --- |
| `Result<T, AppError>` | The operation either succeeded with a value or failed in an expected way. A success always has a value; a failure always has an error. |
| `AppError(Kind = Domain, ...)` | A **validation failure**: a shop rule rejected this request (an expected business outcome). |
| `AppError(Kind = Operational, ...)` | The **environment** failed: the file is missing, unreadable or malformed. |
| `Maybe<T>` (`Some` / `None`) | The answer to a **lookup**: the item is there or it is not. It is not an error by itself. |
| Workflow state | `Draft`-style stages are not used here; the stages of Assignment 4 (`SelectionResult`, `PricingResult`, `QuoteSummary`) are the state of the pipeline. |

`Maybe.None` becomes a `Result` failure only because this workflow requires the item (`ToResult`). A flower with **stock 0 is `Some`**: it exists and is sold out, which gives `insufficient_stock`, not `flower_not_found`.

### 9.3 Expected results and errors

| Case | Example request | Result | Error code and message |
| --- | --- | --- | --- |
| Success | Q1: 10 Rose, 5 km, not a member | success, total 2550.00 | – |
| Invalid quantity (domain) | Q2: 4 Rose | failure | `quantity_below_minimum`: "Order Q2: quantity 4 is below the minimum of 5 stems." |
| Missing item (domain) | Q5: Tulip, not in the inventory | failure | `flower_not_found`: "Order Q5: Tulip is not stocked." |
| State does not permit (domain) | Q3: 10 Lily, only 8 in stock | failure | `insufficient_stock`: "Order Q3: only 8 Lily in stock, 10 requested." |
| Distance (domain) | Q6: 10 Rose, 25 km | failure | `distance_too_far`: "Order Q6: distance 25 km exceeds the delivery limit of 20 km." |
| Present, zero stock | Q4: 5 Sunflower, stock 0 | failure | `insufficient_stock` (the flower is found, not absent) |
| Boundary success | Q7: 5 Lily, 20 km, member | success, total 2155.00 | – |
| Missing file (operational) | `data/does_not_exist.csv` | failure | `file_not_found`: "The inventory file '...' was not found." |
| Malformed file (operational) | `data/inventory_malformed.csv` | failure | `malformed_content`: "Inventory file '...' is malformed: Line 3: ... Line 4: ..." |

### 9.4 Demonstrations (working directory: the folder with `FlowerShop.sln`)

```
dotnet run --project src/FlowerShop -- quote data/inventory.csv data/quote_requests.csv
dotnet run --project src/FlowerShop -- quote data/does_not_exist.csv data/quote_requests.csv
dotnet run --project src/FlowerShop -- quote data/inventory_malformed.csv data/quote_requests.csv
dotnet run --project src/FlowerShop -- data/orders.csv      # the earlier pipeline, still active
```

Expected output of the first command (standard delivery policy):

```
Loaded 3 stock lines and 7 quote requests.

OK      Q1: 10 x Rose for Aigerim = 2550.00 (subtotal 2000.00, discount 0.00, delivery 550.00)
FAILED  [quantity_below_minimum] Order Q2: quantity 4 is below the minimum of 5 stems.
FAILED  [insufficient_stock] Order Q3: only 8 Lily in stock, 10 requested.
FAILED  [insufficient_stock] Order Q4: only 0 Sunflower in stock, 5 requested.
FAILED  [flower_not_found] Order Q5: Tulip is not stocked.
FAILED  [distance_too_far] Order Q6: distance 25 km exceeds the delivery limit of 20 km.
OK      Q7: 5 x Lily for Aibek = 2155.00 (subtotal 900.00, discount 45.00, delivery 1300.00)
```

The second command prints `ERROR (Operational) [file_not_found] ...` and the third prints `ERROR (Operational) [malformed_content] ...` with the bad line numbers. Both exit with code 1 and process no request.

**Running from Visual Studio:** the program starts in `bin/Debug/net8.0`. A relative path such as `data/orders.csv` that is not found there is searched in the parent folders (`DataPaths.Resolve`), so the files in the project's `data` folder are found without copying them. For the quote workflow set the debug arguments (Project properties > Debug > command line arguments) to `quote`, or to `quote data/inventory.csv data/quote_requests.csv`.

### 9.5 Where Map and Bind are used (and why their return types differ)

- `Bind(item => CheckRequest(...))` and `Bind(order => PriceWithPipeline(...))`: the next step **may itself fail**, so it returns a `Result`; `Bind` keeps that `Result` as it is.
- `Map(quote => QuoteDisplay.From(quote))`: the last step **cannot fail** and returns a plain value; `Map` wraps it in a success.
- On a failure both skip the supplied function and pass the same error on. Proof: `FailurePropagation_MapAndBind_...` (counters stay 0) and `FailurePropagation_MissingItem_SkipsValidationAndPricing` (the delivery policy delegate is never called).
- `QuoteCommand` also uses `Bind` and `Map` to combine "inventory loaded" and "requests loaded".

### 9.6 Exception boundary and consistent state

- `FileBoundary.ReadLines` is the **only** place that touches the file system. It catches `FileNotFoundException`, `DirectoryNotFoundException`, `UnauthorizedAccessException`, `IOException` and invalid-path exceptions, and returns an Operational failure that names the file. There is no empty catch and no fallback.
- `InventoryLoader.Parse` and `RequestLoader.Parse` are pure. Parsing is **all-or-nothing**: one bad line gives one failure and no `Inventory` object, so nothing is partially loaded. `Inventory` and `Result` are immutable, so a failed load cannot change data loaded earlier (test `MissingFile_..._StateStaysConsistent`).
- The domain workflow never throws for business failures; they are `Result` failures.

### 9.7 Requirement -> code map

| Requirement | Where |
| --- | --- |
| Result type, no contradictory states | `Result.cs` (private constructor, private case classes, `Ok` / `Fail` reject null) |
| At least three domain failures | `QuoteWorkflow.CheckRequest`, `Maybe.ToResult` in `RequestQuote` |
| Structured error | `AppError.cs` |
| Missing value model and real lookup | `Maybe.cs`, `Inventory.Find` |
| Map and Bind, one real chain | `ResultExtensions` in `Result.cs`, `QuoteWorkflow.RequestQuote` |
| Final outcome handled once at the boundary | `QuoteCommand.Run` (`Match`) |
| File exceptions at the boundary | `FileBoundary.cs`, `InventoryLoader.Load`, `RequestLoader.Load` |
| Earlier pipeline stays active | `QuoteWorkflow.PriceWithPipeline` calls `OrderPipeline.Run` |

### 9.8 Tests for Assignment 6 (`tests/FlowerShop.Tests/ResultWorkflowTests.cs`, 20 methods)

| Required case | Tests |
| --- | --- |
| Complete success | `CompleteSuccess_ChainProducesTheCorrectDisplayModel` (total 2550) |
| Domain failures | `DomainFailure_QuantityBelowMinimum_...`, `..._FlowerNotStocked_...`, `..._InsufficientStock_...`, `..._DistanceTooFar_...` |
| Lookup present and absent | `Lookup_PresentAndAbsent_AreBothHandled`, `Lookup_ZeroStock_IsPresentData_NotAbsence`, `LegitimatePrimitiveValues_...`, `Quote_WithMemberFalse_...` |
| Map success | `Map_Success_ReceivesTheValueAndReturnsTheTransformedValue` |
| Bind success | `Bind_Success_ReceivesTheValueAndKeepsTheNextResult` |
| Failure propagation | `FailurePropagation_MapAndBind_...`, `FailurePropagation_MissingItem_SkipsValidationAndPricing` |
| Missing file | `MissingFile_IsReportedAsOperationalFailure_AndStateStaysConsistent`, `MissingFile_BlankPath_...` |
| Malformed file | `MalformedFile_BecomesOneClearFailure_...`, `MalformedContent_InMemory_...` (7 rows), `RequestFile_ValidAndMalformed_...` |
| Integration | `ValidInventoryContent_WithZeroStock_...`, `Integration_LoadedFiles_FlowThroughTheWholeChain` |

File tests write to a temporary folder (`Path.GetTempPath()` + a new GUID) and delete it afterwards. Together with the 18 pipeline tests plus `DataPathsTests.cs` (2 tests for the file path lookup), and `JsonInventoryTests.cs` (7 tests, 19 cases for Task 7), the project has 47 test methods (73 cases).

**Latest test outcome:** `[paste the result of dotnet test here after running it]`

### 9.9 Team contributions (Assignment 6)

| Member (name, group) | Logic | Tests | Documentation |
| --- | --- | --- | --- |
| `[Name, Group]` | `[e.g. Result, Map, Bind]` | `[...]` | `[...]` |

### 9.10 Tasks 6 and 7: failure propagation and malformed JSON

**Task 6 - failure propagation test:** `FailurePropagation_MapAndBind_KeepTheErrorAndDoNotCallTheirFunctions` in `ResultWorkflowTests.cs`. It creates `Result.Fail(error)`, calls `Map` and `Bind` on it, and asserts with `Assert.Equal(error, ErrorOf(...))` that the original error is unchanged. Two counters also prove that the supplied functions were not called.

*What could go wrong if Map or Bind replaced the original error with a different one?* The real cause would be lost. The user and the logs would see a misleading message (for example "pricing failed" when the real problem was a missing flower), the caller that decides by `Code` or `Kind` would take the wrong branch (retry an operational error that is really a business rejection, or the other way around), tests would pass for the wrong reason, and the first failure, which is the one that has to be fixed, would be hard to find.

**Task 7 - malformed JSON:** `InventoryJsonLoader` (`src/FlowerShop/InventoryJsonLoader.cs`) loads `data/inventory.json`. The quote command uses it when the inventory path ends with `.json`.

- Broken JSON syntax (`JsonException`, empty file) -> `Result.Fail(AppError.Operational("malformed_json", ...))`; the message names the file and includes the position reported by the parser.
- Valid JSON with wrong content (not an array, missing `stock`, `"stock": "5"`, negative or fractional stock, unknown flower, duplicate flower) -> `malformed_content`. The properties are checked one by one, so a missing `stock` is an error and is not read as 0.
- All-or-nothing: any problem gives one failure and no `Inventory` object. A previously loaded `Inventory` is immutable, so it stays unchanged (test `MalformedJson_PreviouslyLoadedValidData_RemainsUnchanged`).

```
dotnet run --project src/FlowerShop -- quote data/inventory.json data/quote_requests.csv
dotnet run --project src/FlowerShop -- quote data/inventory_malformed.json data/quote_requests.csv
```

The first prints the same 7 results as the CSV inventory. The second prints `ERROR (Operational) [malformed_json] ...` and exits with code 1.

**Why should file exceptions be handled at the application boundary?** The boundary is the only place that knows about the file system, so it is the only place that can name the file, decide that the problem is operational and choose what to do (message and exit code). If the inner functions caught exceptions they would hide errors or invent defaults, and the domain code would depend on I/O. At the boundary the exception becomes a `Result` once, and everything inside stays pure and testable.

## 10. Extension methods, LINQ reports, currying, deferred execution, resources and a native C library

This part extends the same project. The earlier pipeline (`OrderPipeline`), the explicit `Result` failures (Assignment 6) and the file boundary are reused, not replaced.

### 10.1 How to build and run (working directory: the folder with `FlowerShop.sln`)

Prerequisites: .NET SDK 8.0 or newer and a C compiler for the native library (see 10.8).

```
native\build.cmd                  (Windows x64)   or   ./native/build.sh   (Linux x86_64)
dotnet build
dotnet test
dotnet run --project src/FlowerShop -- report
dotnet run --project src/FlowerShop -- report data/inventory.csv data/orders.csv my_report.txt
dotnet run --project src/FlowerShop -- report data/inventory.json data/orders.csv
dotnet run --project src/FlowerShop -- quote data/inventory.csv data/quote_requests.csv
```

`report` loads the inventory (CSV or JSON, chosen by the file extension) and the orders, builds both reports (the restock report uses the native C function), prints them and exports them to `report_output.txt` (or the third argument).

Failure demonstrations:

```
dotnet run --project src/FlowerShop -- report data/does_not_exist.csv
dotnet run --project src/FlowerShop -- report data/inventory_malformed.csv
dotnet run --project src/FlowerShop -- report data/inventory.csv data/orders_invalid.csv
```

They print `ERROR (Operational) [file_not_found]` or `[malformed_content]`, exit with code 1 and create no export file. To see the native failure, start the program after renaming the binary next to the executable (`src/FlowerShop/bin/Debug/net8.0/flowerlib.dll`, or `libflowerlib.so` on Linux): the output is `ERROR (Operational) [native_unavailable] ...` and the existing export file is not touched.

### 10.2 Two reports

| | Report 1: restock list | Report 2: revenue by flower |
| --- | --- | --- |
| Intended user | The shop owner who orders flowers from the supplier | The shop owner who reviews sales |
| Input | `Inventory` from `data/inventory.csv` or `.json` | Quotes produced by the earlier `OrderPipeline` from `data/orders.csv` |
| Filters | stock <= 10 (`LowStockThreshold`) | quote total >= `MinimumQuoteTotal` (0) |
| Output fields | flower, stock, order quantity (target 50 minus stock), urgency score, supplier cost in cents (native call) | flower, number of orders, stems, revenue |
| Ordering | urgency score descending, then flower name ordinal | revenue descending, then flower name ordinal |
| Code | `StockReports.LowStock`, `RestockWorkflow.Run` | `RevenueReport.ByFlower` |

Operators: `Where`, `Select`, `OrderByDescending` + `ThenBy` (inside the `RankBy` extension and directly in report 2), `GroupBy`, `Sum`, `Count`, `Aggregate` (`RevenueReport.TotalRevenue`). Report 2 composes the stages `Where -> GroupBy -> Select -> OrderByDescending -> ThenBy -> ToImmutableArray`. The calculations are pure functions that return `Result`; printing is in `ReportCommand` and file export is in `ReportExporter`.

Expected results for the sample data (stock: Rose 100, Lily 8, Sunflower 0; the standard delivery policy):

```
FLOWER SHOP REPORTS

REPORT 1 (volume-first scoring): restock list, stock <= 10, target 50
Flower     Stock  Order  Score  Cost(cents)
Sunflower      0     50    500       225000
Lily           8     42    420       359100
Total cost (cents): 584100

REPORT 1 (value-first scoring): restock list, stock <= 10, target 50
Flower     Stock  Order  Score  Cost(cents)
Lily           8     42    942       359100
Sunflower      0     50    550       225000
Total cost (cents): 584100

REPORT 2: revenue by flower (standard delivery)
Flower     Orders  Stems      Revenue
Tulip           2     32      4208.00
Rose            1     10      2550.00
Lily            1      5      2200.00
Total revenue: 8958.00
```

Rose has stock 100, so it is not in report 1. Orders O4 and O5 are rejected by the pipeline, so they are not in report 2.

### 10.3 Custom extension methods (`src/FlowerShop/QueryExtensions.cs`)

| Extension | Receiver -> return | Behaviour | Why it belongs on the receiver | Used in |
| --- | --- | --- | --- | --- |
| `RankBy(score, tieBreaker)` | `IEnumerable<T>` -> `IOrderedEnumerable<T>` | Highest score first; equal scores are ordered by the ordinal comparison of the tie-breaker string. It does not change the source. | It is a query operation on any sequence, so it composes in a LINQ chain and makes the deterministic order a rule of the project. | `StockReports.LowStock` |
| `Collect()` | `IEnumerable<Result<T, E>>` -> `Result<ImmutableArray<T>, E>` | Success with all values in order, or the first failure; the sequence is not enumerated further after the first failure (no more native calls). | The operation is about a sequence of results, so the receiver is that sequence; it avoids a hand-written loop in every workflow. | `RestockWorkflow.Run` |

### 10.4 Currying and partial application (`src/FlowerShop/Scoring.cs`)

`Scoring.Urgency` has the type `Func<int, Func<int, Func<int, Func<StockItem, int>>>>`: target stock, then shortage weight, then price weight, then the item. Score = `max(0, target - stock) * shortageWeight + retailPrice * priceWeight`.

```
var scoreForTarget = Scoring.Urgency(50);
var volumeFirst = scoreForTarget(10)(0);
var valueFirst = scoreForTarget(1)(5);
```

On the same input (Lily: stock 8, price 180; Sunflower: stock 0, price 100): `volumeFirst` gives Lily 420 and Sunflower 500, `valueFirst` gives Lily 942 and Sunflower 550. So the ranking is different: Sunflower first with `volumeFirst`, Lily first with `valueFirst`. The configured functions are passed into `StockReports.LowStock` as parameters (`ReportWorkflow.Build`); there is no global setting. Negative values are rejected (`ArgumentOutOfRangeException`) when all three arguments are applied.

### 10.5 Deferred execution (`src/FlowerShop/LazyQuotes.cs`)

`LazyQuotes.Generate` returns `IEnumerable<Result<QuoteDisplay, AppError>>` with `yield return`: one quote is calculated per iteration step. Arguments are checked immediately; the calculation starts only when the sequence is enumerated. The `quote` command prints from this stream.

The tests use a counting delivery policy and a counting source (probe code lives only in the tests): creating the enumerable gives 0 calls; the first `MoveNext` gives 1 call; two `ToArray()` calls give 3 + 3 = 6 calls (repeated enumeration repeats the work); after `ToImmutableArray()` (9 calls) any further use of the snapshot gives no new calls. Materialisation is explicit where a stable snapshot is needed; file reads are not repeated because the files are loaded once into `ImmutableArray` snapshots, and no business state changes.

### 10.6 Resource ownership and disposal

| Resource | Created in | Owner | Released | Exception path |
| --- | --- | --- | --- | --- |
| `StreamReader` over the input file | `LineStream.ReadFile` (called by `FileBoundary.ReadLines`) | the iterator in `LineStream.Iterate` | `using var` ends when the iteration finishes, fails or is stopped (disposing the enumerator) | test with a failing reader and an early stop; `FileBoundary` snapshots the lines inside its try block |
| `FileStream` and `StreamWriter` of the export | `ReportExporter.Write` / `ExportToFile` | `ReportExporter.Write` | `using` ends of the method | on an I/O error the result is `export_failed`; the temporary file is deleted and the old report is kept (the new file replaces it only after a complete write) |
| File handle of `File.ReadAllLines` | `Program.cs` (the orders command) | the .NET library opens and closes it inside the call | before the call returns, also on an exception | convenience API: no `using` needed; observable cleanup is tested through the file tests |

Garbage collection is not relied on for file handles. A test deletes the file after an early stop to show that the handle was released.

### 10.7 Resource note: retention and allocation

- **Materialisation points:** `FileBoundary.ReadLines` (all lines of a file as an `ImmutableArray`), `StockReports.LowStock` and `RevenueReport.ByFlower` (`ToImmutableArray`), `RestockWorkflow` (`Collect`), `ReportApplication` (the lines of the report, used once for the export and once for the printing). `Inventory.Items` builds a new sorted array on each call; it is not cached.
- **Deferred queries and closures:** `LazyQuotes.Generate` keeps its arguments (requests, inventory, rules, delivery policy) alive until the enumeration has finished or the enumerator is disposed. The `quote` command enumerates it immediately, so the retention is short.
- **Long-lived collections, caches and event subscriptions:** not used. There is no static collection that grows; `ShopRules.Default`, `ReportSettings.Default` and the delegates in `Scoring` and `DeliveryPolicies` are small immutable values.
- **Design decision:** the file is read through a stream and the lines are materialised once inside the boundary, so the stream is closed before any processing starts and the reports work on immutable snapshots instead of an open resource.
- **Trade-off:** streaming (`LineStream` alone) uses little memory for large files but keeps the file open while iterating and can be consumed only once; a snapshot uses memory for the whole data set but can be reused safely and closes the file early. The files of this project are small, so the snapshot is used. No memory leak was found or fixed.

### 10.8 Native C library (`native/`)

Function contract (`native/flowerlib.h`, `native/flowerlib.c`):

| Item | Value |
| --- | --- |
| Export | `int64_t fs_reorder_cost_cents(int32_t quantity, int32_t unit_cost_cents)` |
| Purpose | Supplier cost of a restock order with a volume discount: 5% from 20 stems, 10% from 50 stems |
| Units and rounding | Smallest currency units (cents); integer arithmetic, rounded to the nearest cent, halves rounded up |
| Valid range | quantity 1 to 1,000,000; unit cost 1 to 1,000,000 cents |
| Invalid input | returns -1 (no other side effect; no native memory is allocated or returned) |
| Manual example | 20 stems x 6000 cents = 120000; 5% discount -> 114000 cents. Rounding example: 21 x 5001 = 105021; 95% = 99769.95 -> 99770 |
| C# binding | `NativeMethods.ReorderCostCents` (`DllImport("flowerlib", EntryPoint = "fs_reorder_cost_cents", CallingConvention = Cdecl)`; `int` and `long` match `int32_t` and `int64_t`; no project setting is needed for `DllImport`) |
| Adapter | `NativeReorderCostCalculator` (`IReorderCostCalculator`): validates the domain input first (`invalid_reorder_input`), translates `DllNotFoundException`, `EntryPointNotFoundException` and `BadImageFormatException` into `native_unavailable`, and a returned -1 into `native_rejected` |
| Used in | `RestockWorkflow` (the cost column and the total of report 1) |

Build and platform:

| Item | Linux (built and checked here) | Windows |
| --- | --- | --- |
| Operating system / architecture | Ubuntu 24.04, x86_64 | Windows x64 |
| Compiler | gcc 13.3.0 | MinGW-w64 `gcc` (x86_64) or MSVC `cl` |
| Build command | `./native/build.sh` = `gcc -shared -fPIC -O2 -Wall -Wextra -o bin/linux-x64/libflowerlib.so flowerlib.c` (in `native/`) | `native\build.cmd` = `gcc -shared -O2 -Wall -Wextra -o bin\win-x64\flowerlib.dll flowerlib.c`, or `cl /LD /O2 flowerlib.c /Fe:bin\win-x64\flowerlib.dll` |
| Binary location | `native/bin/linux-x64/libflowerlib.so` (included in the archive) | `native/bin/win-x64/flowerlib.dll` (build it with the script; not included because it was built on Linux) |

Loading: `FlowerShop.csproj` copies every file of `native/bin/<win-x64 or linux-x64>/` next to the executable (`CopyToOutputDirectory`), and the test project receives the same files through its project reference. Run the native build before `dotnet build`; after a clean extraction the sequence is: native build, `dotnet build`, `dotnet test`. Only x64 Windows and Linux are declared as supported; for another platform build the library for it and place it in a folder that the project file maps. The Linux build was checked here by calling the exported function through Python `ctypes` with the examples above.

Integration difficulties and resolutions: (1) the C type `long` is 32 bits on Windows and 64 bits on Linux, so the contract uses `int32_t` and `int64_t` and the C# binding uses `int` and `long`; (2) a deleted library cannot test a fresh load failure once it is loaded, so the failure test calls a separate `DllImport` of a library that does not exist (a real, fresh load failure) and the adapter test also injects failing bindings; (3) the binary has to be next to both the application and the test host, which is solved with the project-file copy rule. Replace or add to these with what you observed on your own computer.

### 10.9 Requirement -> code map

| Requirement | Where |
| --- | --- |
| Two custom extensions, used | `QueryExtensions.RankBy` (`StockReports.LowStock`), `QueryExtensions.Collect` (`RestockWorkflow.Run`) |
| Two LINQ reports | `StockReports.LowStock`, `RevenueReport.ByFlower`, `ReportWorkflow.Build`, `ReportText` |
| Currying and two partial applications | `Scoring.Urgency`, `ReportWorkflow.Build` |
| Deferred execution with `yield return` | `LazyQuotes`, `LineStream` |
| Resource disposal | `LineStream`, `ReportExporter` |
| Retention note | section 10.7 |
| Native library, binding, adapter | `native/`, `NativeBinding.cs` |
| Integration (reports, native call, export, `Result` errors) | `ReportApplication.Run`, `ReportCommand` |
| File boundaries | `FileBoundary`, `InventoryLoader`, `InventoryJsonLoader`, `RequestLoader` |
| Earlier pipeline and explicit errors | `OrderPipeline.Run` inside `ReportWorkflow.Build`; `Result` and `AppError` everywhere |

### 10.10 Tests for this assignment (49 new methods; 96 in the project, 122 cases)

| Area | Test class and methods |
| --- | --- |
| Reports | `ReportQueryTests`: `LowStockReport_*` (normal, empty, no matches, boundary, tied scores, invalid data), `RevenueReport_*` (normal grouping and totals, empty and no matches, minimum total, tied revenue, invalid data), `ReportText_*`, `InventoryItems_*` |
| Extensions | `RankByExtension_*`, `CollectExtension_*` |
| Configured functions | `ConfiguredFunctions_SameInput_GiveDocumentedDifferentResults`, `CurriedFunction_*` |
| Deferred execution | `DeferredAndResourceTests`: `DeferredExecution_*` (4 tests: nothing before enumeration, one item per step, repeated work vs snapshot, eager argument checks) |
| Resource lifetime | `LineStream_*` (not opened early, full iteration, early stop, failure, file handle), `ExportWrite_*` (success and failure with a tracked stream), `ExportToFile_*` (success, missing folder, blank path, source failing midway keeps the old file, replace) |
| Native integration | `NativeIntegrationTests`: `RealNativeCall_*` (manual values, invalid input), `Adapter_*` (real library, domain validation, real load failure, binding failures), `RestockWorkflow_*` |
| Complete workflows | `ReportApplicationTests`: `NormalWorkflow_*` (CSV and JSON, real native call, export), `FailurePath_*` (native failure and export failure keep all files unchanged) |
| File boundaries | `FileBoundaries_*` (missing inventory, missing orders, malformed content) plus the earlier file tests |

**Latest test outcome:** `[paste the result of dotnet test here after running it]`

### 10.11 Team contributions (extension, LINQ and native assignment)

| Member (name, group) | Logic | Tests | Documentation |
| --- | --- | --- | --- |
| `[Name, Group]` | `[e.g. reports and extensions]` | `[...]` | `[...]` |
