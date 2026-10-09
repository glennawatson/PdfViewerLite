# HyperPdf performance work queue

Start these follow-ups after the current `PdfDocument` helper extraction and its build, test and benchmark verification, tracked in [#128](https://github.com/glennawatson/PdfViewerLite/issues/128).

1. [ ] [#129: Page-tree traversal scratch](https://github.com/glennawatson/PdfViewerLite/issues/129). Store one cursor per ancestor and preserve traversal order, inherited attributes and recovery limits.
2. [ ] [#130: Roman page labels](https://github.com/glennawatson/PdfViewerLite/issues/130). Write the requested case directly into the result string.
3. [ ] [#131: Space compaction](https://github.com/glennawatson/PdfViewerLite/issues/131). Compact both text buffers together and trim each tail once.
4. [ ] [#119: Text hit testing](https://github.com/glennawatson/PdfViewerLite/issues/119). Measure contiguous block summaries and preserve exact, overlapping and nearest/tolerance hits.

The first two costs are measured allocation findings. The latter two need focused baseline measurements. Each issue includes correctness checks and requires comparable BenchmarkDotNet/EventPipe results on net10.0 and net11.0. CMap/font table generation is outside this queue.
