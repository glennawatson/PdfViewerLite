# Features and parity with GNOME Papers 51

This matrix compares PdfViewerLite with Papers ("Document Viewer"), GNOME's document viewer as of GNOME 51, for PDF
documents. Every feature has a BenchmarkDotNet benchmark; allocations are checked from EventPipe traces by
`scripts/audit-allocations.sh`, and every allocation the library makes in a measured workload is explained in
`benchmarks/allocations-explained.json`.

| Area | Papers 51 | PdfViewerLite | Status |
|---|---|---|---|
| Multiple documents | One window per document | Tabs in one window, session restore, reorder, reopen closed tabs | Done |
| Many open documents | n/a | Tab finder (Ctrl+Shift+A), hover previews of tabs, at most 16 documents kept in memory | Done |
| Continuous scrolling | Yes | Yes | Done |
| Single / dual page, odd pages left | Yes | Single, dual, dual with cover page | Done |
| Page by page (non-continuous) | Yes | Yes, More ▸ Page by Page | Done |
| Fit width / fit page / free zoom | Yes | Yes, Ctrl+wheel zooms around the pointer | Done |
| Rotation | Yes | Yes | Done |
| Thumbnails sidebar | Yes | Yes, virtualised and rendered on demand | Done |
| Outline (bookmarks) | Yes | Yes | Done |
| Search with results list | Yes | Yes, incremental, match case, whole words | Done |
| Text selection and copy | Yes | Yes, across pages | Done |
| Links (internal and external) | Yes | Yes, including URLs written as plain text | Done |
| Back / forward history | Yes | Yes | Done |
| Page labels | Yes | Yes, in the page box and thumbnails | Done |
| Night mode / page colours | Yes (inverted) | Page tones per colour scheme, keeping highlight colours true | Done |
| Document properties | Yes | Yes | Done |
| Password protected documents | Yes | Yes | Done |
| Reload on file change | Yes | Automatic or a Reload bar, your choice | Done |
| Recent documents | Start view | Start page plus the shared `recently-used.xbel` | Done |
| Drag and drop | Yes | Files and URLs | Done |
| Open remote (http/https) documents | Via GVfs | Downloaded with Refit | Done |
| Annotations: highlight, underline, strike-out, squiggly | Yes | Yes, from selected text or the right-click menu | Done |
| Annotations: notes, free text, ink | Yes | Yes, with colours, recolour, edit and delete | Done |
| Annotations sidebar | Yes | Yes | Done |
| Form filling | Yes | Text fields, check boxes, choice lists; Tab moves to the next field | Done |
| Signatures | Draw or type, digital signatures listed and checked | Draw or type a signature, list and check digital signatures | Done |
| Attachments | Yes | Sidebar panel, save to a place you choose | Done |
| Save / save a copy | Yes | Yes; signed documents are saved incrementally | Done |
| Print | Yes | Browser-style preview; sends straight to the printer queue via CUPS (printer, copies, colour, two-sided, paper, pages, pages per sheet, annotations), Save as PDF, or the desktop's dialog via the XDG portal | Done |
| Presentation mode | Yes | More ▸ Present (Shift+F5), Esc to stop | Done |
| Text recognition (OCR) | No | More ▸ Recognise Text adds a searchable text layer with Tesseract | Done |
| Caret navigation | Yes | F7: a steady cursor moved with the arrow keys, Shift selects | Done |
| Layers (optional content) | Yes | Layers panel: tick to show, clear to hide; pages are drawn from an in-memory copy with the chosen default visibility, as PDFium has no layer API | Done |
| Signing with a certificate | Yes | Fill & Sign ▸ Sign with Certificate (.p12/.pfx): an incremental update with a detached CMS (SHA-256) signature; earlier signatures stay valid | Done |
| Other formats (DjVu, comics, TIFF) | Partly | Engine interface ready | Later |
| Tear tabs off into new windows | n/a | | Later |

## Desktop integration

The app talks to the desktop only through `IDesktopPlatform` (`src/PdfViewerLite.Core/Platform`): theme, file
manager, recent documents, printing and the single running window. `KdePlatform` implements it for KDE Plasma (the
freedesktop parts work on other Linux desktops); `FallbackPlatform` is used where there is no implementation. A GNOME
or Windows integration is a new `IDesktopPlatform`, chosen in `DesktopPlatforms.Detect`.
