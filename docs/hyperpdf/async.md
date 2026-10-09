# Async and cancellation

Every operation that may wait on the file has an async form that takes a `CancellationToken`. Use it from UI code, for
files on slow disks or network shares, and wherever the user can move on before the work ends, such as switching tabs.
Cancelling stops the work itself, not just the wait.

```csharp
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Text;

using var cancel = new CancellationTokenSource();

using var document = await PdfDocumentReader.OpenAsync("report.pdf", password: null, cancel.Token);
var text = await PdfDocumentText.GetTextPageAsync(document, 0, cancel.Token);

await foreach (var page in PdfDocumentText.FindAsync(document, "invoice", PdfTextSearchOptions.None, cancel.Token))
{
    Console.WriteLine($"Page {page.PageIndex + 1}: {page.Matches.Length} matches");
}

// When the user leaves, stop everything this document was doing.
cancel.Cancel();
```

## Which form to use

| Situation | Use |
|---|---|
| UI thread, or code that must not block | Async forms |
| A file on a slow disk or a network share | Async forms |
| Opening several documents at once | Async forms |
| The user can leave before the work ends | Async forms with a token |
| A tight loop over a document that is already open | Sync forms |
| Rendering many pages in parallel | Start each call on the thread pool, or use `Parallel.ForEachAsync` |

An async call runs its work on the awaiting thread, so on its own it does not render pages in parallel.

## Async members

| Area | Members |
|---|---|
| Open | `PdfDocumentReader.OpenAsync`, `PdfDocumentReader.OpenWithAsync` |
| Pages | `PdfDocumentPages.PrefetchPageAsync`, `PdfDocumentPages.GetPageAsync`, `PdfDocumentText.GetTextPageAsync` |
| Walks | `PdfDocumentText.GetTextPagesAsync`, `PdfDocumentText.FindAsync` (both `IAsyncEnumerable`) |
| Navigation | `PdfDocumentNavigation.GetOutlineAsync`, `PdfDocumentLinks.GetLinksAsync`, `PdfDocumentContent.ScanAnnotationsAsync` |
| Render | `PdfPageRenderer.RenderAsync` |
| Save | `PdfDocumentSaving.SaveAsync` |
| Whole document | `PdfDocumentCheck.CheckAsync`, `PdfOptimizer.OptimizeAsync` |
| Byte sources | `PdfByteSource.ReadAsync`, `PrefetchAsync`, `PdfByteSources.OpenAsync` |

Small in-memory operations, such as setting metadata or form values, stay synchronous.

## What to expect

- When the work is already done, an async call returns at once without allocating.
- A cancelled operation throws `OperationCanceledException` from the awaited task and releases its buffers.
- Argument errors throw at the call; other failures come back through the task.

Measurements are in [benchmarks/hyperpdf-async-performance.md](../../benchmarks/hyperpdf-async-performance.md).
