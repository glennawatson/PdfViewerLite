# HyperPdfLibrary performance

This page reports how fast HyperPdfLibrary is, how much managed memory it allocates and how much memory it holds.
All numbers were measured on 9 October 2026. Read [Measurement limits](#measurement-limits) before you compare them.

## Summary

- **Warm work allocates nothing.** Drawing a tile of a page that is already recorded allocates 0 bytes on every
  sample page: vector layers, text, a scanned image, transparency groups and generated annotations. Warm find, hit
  test, glyph outline, width and CMap lookups also allocate 0 bytes.
- **HyperPDF beats PDFium on opening, navigation, find and image-heavy tiles.** Opening is about 3 times faster, the
  open-edit-save round trip about 1.8 times faster, and warm scan and annotation tiles about 2 times faster.
- **HyperPDF is slower on warm text, vector and transparency tiles and on hit testing.** A warm text tile takes
  about 1.7 times as long as PDFium's, and a transparency tile 1.3 times as long.
- **Large scanned books need much more native memory than PDFium.** Rendering a 900-page JBIG2 book holds about
  1.1 GB of working set against PDFium's 119 MB, and cold pages are about 10 times slower. The page-picture cache
  keeps full-size decoded images alive beyond the 128 MiB image cache limit. See [Native memory](#native-memory).

## How it was measured

| Item | Setting |
|---|---|
| Machine | AMD Ryzen 7 5800X (8 cores, 16 threads), 62 GiB RAM, Fedora, Linux 7.2 |
| Runtimes | .NET 10.0.12 and .NET 11.0.0-rc.1 |
| Timing | BenchmarkDotNet default job, Release, `--profiler EP` (CPU sampling, no allocation tracing) |
| CPU pinning | Both timing runs ran one after the other in the same benchmark slot (CPUs 1-3 and their SMT siblings) |
| Allocations | EventPipe allocation trace from start-up, read by `scripts/AllocationAudit.cs` (short job, .NET 11) |
| Native memory | A probe that opens a corpus file, renders a 512 px tile of every page at 150%, then disposes it |

Timing tables show the mean. Treat a difference smaller than the two error margins added together as no difference.

## Timing

### Open and navigate

The sample document has a few pages, an outline, labels and links.
End to end opens a file, renders a tile, builds the text page, adds a highlight and saves.

| Operation | PDFium .NET 10 | HyperPDF .NET 10 | PDFium .NET 11 | HyperPDF .NET 11 |
|---|---:|---:|---:|---:|
| Open and read every page size | 283 µs | 88 µs | 385 µs ± 27 | 106 µs ± 11 |
| Open and read outline, labels and links | 962 µs | 279 µs | 925 µs | 267 µs |
| End to end: open, render, text, annotate, save | 2,339 µs | 1,356 µs | 2,249 µs | 1,252 µs |
| Save incremental after a page reorder | n/a | 84 µs | n/a | 78 µs |

### Render one 512 px tile at 200%

Warm means the page is open and already recorded to a picture. Cold means open the file and draw the first tile,
which records the page.

| Page | Engine | Warm .NET 10 | Warm .NET 11 | Cold .NET 10 | Cold .NET 11 |
|---|---|---:|---:|---:|---:|
| Text (3 pages of body text) | PDFium | 195 µs | 198 µs | 653 µs | 780 µs ± 43 |
| | HyperPDF | 324 µs | 329 µs | 541 µs | 580 µs ± 16 |
| Layers (vector shapes in optional content) | PDFium | 25 µs | 24 µs | 89 µs | 87 µs |
| | HyperPDF | 44 µs | 43 µs | 102 µs | 98 µs |
| Scan (256 px greyscale image) | PDFium | 388 µs | 400 µs ± 14 | 455 µs | 497 µs ± 20 |
| | HyperPDF | 173 µs | 168 µs | 335 µs | 337 µs |
| Transparency (groups, blend modes, soft mask) | PDFium | 5,815 µs | 7,302 µs ± 371 | 5,967 µs | 6,222 µs |
| | HyperPDF | 7,578 µs | 11,126 µs ± 769 | 7,676 µs | 8,130 µs |
| Annotations (48 generated appearances) | PDFium | 418 µs | 383 µs | 1,109 µs | 1,373 µs ± 133 |
| | HyperPDF | 196 µs ± 13 | 178 µs | 747 µs | 993 µs ± 75 |

The .NET 11 transparency and cold annotation rows were noisy (another benchmark ran in the other slot). An earlier
.NET 11 run on the whole machine measured the warm transparency tile at 6,359 µs for PDFium and 8,491 µs for HyperPDF,
the same 1.3 ratio as .NET 10.

### Text

The text page is 48 lines of Helvetica body text.

| Operation | PDFium .NET 10 | HyperPDF .NET 10 | PDFium .NET 11 | HyperPDF .NET 11 |
|---|---:|---:|---:|---:|
| Build the text page (cold) | n/a | 909 µs | n/a | 840 µs |
| Find a phrase (warm) | 21.6 µs | 1.0 µs | 21.9 µs | 1.9 µs ± 0.2 |
| Hit test a point (warm) | 9.4 µs | 16.3 µs | 17.4 µs ± 0.7 | 16.4 µs |

PDFium's find includes its own text page lookup through the app's adapter, so the find ratio flatters HyperPDF.

### Fonts: one 512 px tile of a text page at 150%

| Font | Warm .NET 10 | Warm .NET 11 | Cold .NET 10 | Cold .NET 11 |
|---|---:|---:|---:|---:|
| Embedded TrueType | 533 µs | 515 µs | 1,089 µs | 1,170 µs ± 61 |
| Composite (CID) | 486 µs | 468 µs | 1,027 µs | 1,026 µs ± 38 |
| Standard 14 (substituted system face) | 1,570 µs | 1,513 µs | 2,286 µs | 2,173 µs |

The warm standard-font tile costs three times the embedded-font tile. It replays a recorded picture, so the cost is
Skia drawing the substituted face, not font loading.

### Image codecs: decode one page image

| Codec and sample | .NET 10 | .NET 11 |
|---|---:|---:|
| JBIG2, Google Books page | 2.9 ms | 2.8 ms |
| JBIG2, Library of Congress page | 16.2 ms | 16.3 ms |
| JBIG2, Internet Archive generic region page | 55.2 ms | 53.8 ms |
| JPEG 2000 reversible 5/3 page | 69 ms ± 3 | 77 ms ± 9 (median 58 ms) |
| JPEG 2000 irreversible 9/7 page | 77 ms ± 3 | 108 ms ± 10 (median 105 ms) |

JPEG 2000 decodes code blocks with `Parallel.For`, so it is sensitive to the three pinned cores and to the other slot.
On the whole machine (.NET 11) the same pages took 52 ms and 59 ms.

### Optimiser (indicative only)

These come from the short allocation-audit job (3 iterations, .NET 11, pinned slot), so the error margins are wide.

| File and preset | Mean | Managed allocation |
|---|---:|---:|
| Scanned corpus file (14 JBIG2 pages), balanced | 15.9 ms | 372 KB |
| Tagged text file, balanced | 2.2 ms | 305 KB |
| Tagged text file, keep quality | 2.7 ms | 303 KB |
| 1200 px photo page, smaller (downsample to JPEG) | 24.1 ms | 35 KB |

Optimising opens a working copy, so every object is parsed again; the rest is the object graph, content hashes,
subset font tables, recoded images and the report. All of it runs once per user request. Two copies look avoidable
and were fixed afterwards: `TrueTypeSubsetter` now passes unchanged font tables to `SfntWriter` as slices of the source
font, so each is copied once (it was copied twice, about 32 KB and 52 KB per text run; not re-measured by the audit).

## Managed allocations per operation

Measured with the EventPipe audit on .NET 11. Every remaining allocation is explained in
`benchmarks/allocations-explained.json`.

| Operation | HyperPDF | PDFium adapter | What remains and why |
|---|---:|---:|---|
| Warm tile, all five sample pages | 0 B | 0 B | Nothing |
| Warm find, warm hit test | 0 B | 0 B | Nothing |
| Warm glyph outlines, widths, text, CMap lookups | 0 B | n/a | Nothing |
| Device CMYK and ICC colour conversion | 0 B | n/a | Nothing |
| Read a content stream, Flate decode | 0 B | n/a | Nothing |
| Open | 67.6 KB | 2.1 KB | Object store, xref entries, page tree, name table. PDFium's open allocates natively |
| Cold text tile | 39.0 KB | 1.4 KB | Open, font load, glyph outlines, page picture |
| Cold scan tile | 285.9 KB | 1.4 KB | 262 KB is the decoded image's BGRA array before it is copied into a Skia image |
| Cold annotation tile | 154.5 KB | 1.3 KB | 48 generated appearance streams, recorded once |
| End to end | 174.4 KB | 2.6 KB | Every first-use cost once, plus the edit and the save |
| Build a text page | 296 KB | n/a | The text page itself: characters, lines, words, text |
| Load a font | 7.0-13.4 KB | n/a | Font program, encoding, widths, glyph text |
| JPEG 2000 page decode, including HTJ2K | 21.7-23.6 MB | n/a | The decoded BGRA array is 21-23.5 MB; decoder state is about 6-8 KB |
| JBIG2 page decode | 0.4-2.4 KB | n/a | Segment, region and symbol dictionary state |
| Layer toggle | 160 B | n/a | The hidden set is copied so render threads read it without a lock |
| Save incremental | 77.9 KB | n/a | Cross-reference rows and the saved bytes |

### Changes made in this audit

| Benchmark | Before | After | Change |
|---|---:|---:|---|
| Load font, embedded TrueType | 18,738 B | 13,362 B | One-character glyph texts are shared, not created per code |
| Load font, standard 14 | 12,365 B | 6,985 B | Same |
| Cold text tile, standard 14 | 34,637 B | 29,185 B | Same |
| Cold text tile through the adapter | 44,520 B | 38,998 B | Same |
| End to end | 179,004 B | 174,425 B | Same |
| Raster text pass over a 900-page book, `byte[]` in `BDC` | 330,683 B (9,366 copies) | 0 B | Inline property lists are parsed in place instead of copied first |

## Native memory

Process memory for two cached corpus books, rendering one 512 px tile of every page at 150% on .NET 11. Working set
is resident memory; private is the process's private bytes; GC is the managed heap's committed memory.

| File and engine | After open | After every page | After dispose and full GC | Cold page |
|---|---:|---:|---:|---:|
| `ia-us-reports-341.pdf`, 928 JPEG 2000 pages, HyperPDF | 79 MB | 834 MB (GC 477 MB) | 804 MB (GC 503 MB) | 173 ms |
| same file, PDFium | 42 MB | 153 MB | 129 MB | 40 ms |
| `google-us-reports-supreme-court.pdf`, 893 JBIG2 pages, HyperPDF | 69 MB | 1,101 MB (GC 256 MB) | 592 MB (GC 190 MB) | 161 ms |
| same file, PDFium | 40 MB | 119 MB | 103 MB | 15 ms |

What the numbers show:

- **Opening is cheap.** The file is memory-mapped, so opening a 34 MB book adds about 40-50 MB of working set.
- **Memory reaches its peak within the first 60 pages and then stays flat**, so the caches are bounded. Shrinking the
  image cache to 8 MiB did not lower the peak.
- **The eight cached page pictures hold the memory.** A picture keeps a native reference to every image it drew, so
  each cached scanned page keeps its full-size decoded image even after the image cache evicts it. Bilevel JBIG2 and
  greyscale pages are stored as 4-byte BGRA, so one 600 dpi page costs tens of megabytes. Disposing the document
  released about 500 MB.
- **Managed memory left after dispose is the shared array pool.** A heap snapshot after dispose found no live
  HyperPdfLibrary or Skia objects. The remaining 85-190 MB of managed heap is `ArrayPool<T>.Shared` keeping large
  `int[]` and `byte[]` buffers from JPEG 2000 and JBIG2 decoding. The runtime trims these when memory is tight.
- **About 300 MB of native memory stays resident after dispose without any live owner.** No Skia object is alive, and
  Skia's resource cache holds 23 MB of its 32 MB limit. This is probably the native allocator keeping freed pages;
  limiting glibc arenas did not change it. Attributing it needs a native heap profiler, which is not an approved tool.
- **Decoded images pass through managed memory.** Each image is decoded into a managed array and then copied into a
  Skia image, so a JPEG 2000 page briefly allocates about 24 MB on the large object heap.

Options to cut native memory, in order of expected effect (not yet measured):

1. Bound the page-picture cache by the bytes of the images its pictures hold, not only by page count.
2. Store bilevel and greyscale images as `Alpha8` or `Gray8` instead of BGRA: 4 times smaller.
3. Decode images into pooled or native memory instead of a new managed array.

### Memory work after the audit

Measured on .NET 11 with the same probe, rendering the first 200 pages of each book in the same benchmark slot (peak is
the highest working set sampled every 10 pages). The full-book "before" numbers above come from the audit.

| Book | Peak working set | Working set after 200 pages | Cold page (ms per tile) | After dispose and GC |
|---|---:|---:|---:|---:|
| JBIG2 Google book, before | 1,094 MiB | 1,066 MiB | 153 | 589 MiB |
| JBIG2 Google book, after | 782 MiB | 546 MiB | 64 | 505 MiB |
| JPEG 2000 colour book, before | 1,259 MiB | 1,283 MiB | 116 | 1,184 MiB |
| JPEG 2000 colour book, after | 912 MiB | 732 MiB | 127 (noisy) | 755 MiB |
| PDFium, both books | 91-141 MiB | 91-128 MiB | 14-37 | 87-117 MiB |

Decoding one JBIG2 page image (`HyperPdfScanBenchmarks`, short job): 93.1 ms to BGRA, 41.7 ms to the compact gray form the
renderer now uses. The colour JPEG 2000 image is RGB, so its decode time does not change (4.1 ms against 3.7 ms for the
small image; the full-size page is not isolated by this benchmark). The next-page benchmark was too noisy to use
(error margins larger than the difference); the probe numbers above are the evidence for page time.

What changed:

- **Greyscale and bilevel images are 1 byte per pixel** (Gray8) when every colour of the image is an opaque gray, checked
  through the colour space. Images with masks, colour keys or colour stay BGRA.
- **Decoder output is Skia's pixel memory.** Large pixel arrays sit on the pinned object heap and `SKImage.FromPixels`
  uses them, so the extra managed copy is gone.
- **Both caches are bounded by bytes.** `PdfRenderOptions.ImageCacheBytes` (default 64 MiB) and the new
  `PdfRenderOptions.PictureCacheBytes` (default 128 MiB, counting each picture's own operations plus the images it drew;
  the newest page is always kept; also capped at 32 pages).
- **Large decoder buffers use bounded pools** (`ScratchPools`, 48 MiB kept for reuse, trimmed when a document is disposed).
- **`PdfDocument.Dispose` empties the render caches, image cache, page pictures of its renderers, text pages and parsed
  objects.** Renders racing the dispose finish or throw `ObjectDisposedException`.
- A 128 MiB scratch budget was tried and gave a higher peak (1,151 MiB for the colour book), so the budget stays at 48 MiB.

What is still above PDFium: a colour scan costs 4 bytes per pixel in Skia (about 21 MB per page), and Skia's mipmap and
resource caches (28 of 32 MiB) and the managed decoder buffers add to it. The colour book still peaks at about 7 times
PDFium's working set. Decoding at display resolution was not done: pictures are recorded once and replayed at every zoom, so
the decoded size cannot depend on one zoom level.

## Cache retention

| Cache | Holds | Scope | Bound | Eviction |
|---|---|---|---|---|
| Page pictures (`PdfPageRenderer`) | Up to four Skia pictures per page (content, print, annotations, print annotations) and a paused progressive recording | One renderer | 128 MiB of picture operations plus drawn image pixels (`PdfRenderOptions.PictureCacheBytes`), at most 32 pages; the newest page always stays | Least recently used; a new layer version replaces the old one; all on renderer or document dispose |
| Images (`ImageCache`) | Decoded Skia images by image stream, plus streams that failed to decode | One document | 64 MiB of pixels (`PdfRenderOptions.ImageCacheBytes`) | Least recently used; an image in use is released when its last user finishes. Pictures keep their own reference |
| Fonts (`PdfFontCache`) | Loaded fonts by font dictionary | One document | None | Kept for the document's life |
| Glyph outlines (`GlyphOutlineCache`) | Skia paths by glyph | One font | 65,536 slots in pages of 256 | Kept for the font's life |
| Colour spaces, shadings, patterns, soft masks (`ObjectCache`) | Parsed objects; shadings keep a Skia shader, patterns and soft masks a Skia picture | One document | None | Kept for the document's life |
| Text pages (`PdfTextPageCache`) | Built text pages | One document | 16 pages | Least recently used |
| Parsed objects (`PdfObjectStore`) | Every object read, by object number | One document | The file's object count | Kept for the document's life |
| Names (`PdfNameTable`) | Interned name spellings | One document | Names in the file | Kept for the document's life |
| Lab and CalRGB colours (`ColorCache`) | Converted colours | One colour space | 4,096 slots | Overwritten on collision |
| ICC transforms (`IccProfile`) | Colour transforms | Process | 16 recent, plus a weak table by stream | Round robin; weak entries die with their stream |
| Predefined CMaps, CID-to-Unicode tables | Parsed tables | Process | The fixed set of predefined names | Never |
| System font matches (`SystemFontMatcher`) | Substitute Skia faces | Process | Fonts asked for | Never |
| Render surface (`RenderSurface`) | One Skia surface and paint | Per thread | The largest tile drawn | Lives as long as the thread |
| Stream source pages (`StreamPdfByteSource`) | 64 KiB file pages, when the file is not mapped | One document | 4 MiB | Least recently used |

Notes:

- `PdfDocument.Dispose` closes the file and empties the render caches, image cache, text pages, parsed objects and the
  page pictures of every renderer made for the document, then trims the scratch pools.
- The adapter keeps one renderer per document in a weak table and releases it with the document.

## Measurement limits

- **Shared machine.** Other agents ran benchmarks and builds at the same time. Timing runs were pinned to three cores
  through the benchmark lock, but the other slot was busy, which widened some .NET 11 error margins (marked ± above).
  Rows with wide margins are not evidence of a runtime difference.
- **No high priority.** BenchmarkDotNet could not raise its process priority (permission denied).
- **CPU sampling overhead.** Timing runs used `--profiler EP`, which samples stacks. It adds a small, even cost to
  both engines.
- **Allocation sampling.** The runtime reports allocations in batches. Rows marked amortised in the audit, such as
  pool growth, are shares rather than whole objects. Codec and corpus benchmarks ran only a few operations, so their
  totals vary by a few percent between runs.
- **PDFium's memory is native.** The allocation audit sees only PDFium's managed adapter, so its small byte counts are
  not comparable with HyperPDF's managed totals. Use the native memory table for a fair comparison.
- **Native attribution.** Working set and private bytes come from the operating system. The probe cannot split
  native memory between Skia, the allocator and mapped file pages.
- **One file per codec.** The corpus numbers come from two books. Other scans will differ with resolution and colour.
