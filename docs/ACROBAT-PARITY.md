# Parity with Adobe Acrobat Reader

This matrix compares PdfViewerLite with the free Adobe Acrobat Reader on the desktop, area by area. Acrobat Pro
features (editing page content, organising pages, export to Office, compare, redaction) are not part of Reader and
are not listed.

**Status** is one of:

- **Done**: it works, has tests, and is measured by a BenchmarkDotNet benchmark. Allocations are checked from
  EventPipe traces by `scripts/audit-allocations.sh` and explained in `benchmarks/allocations-explained.json`.
- **Partial**: part of what Reader does works; the gap is described.
- **Later**: planned, not started.
- **Out of scope**: not a product goal.

## Milestone: the same behaviour on Linux, Windows and macOS

The current milestone is this: the same document can be opened, navigated, annotated, printed and read aloud with
the same behaviour on all three desktops, with a clean Focus Mode and reliable natural speech. CI builds and packages
every platform on each release, but tests only run on Linux.

| Step | Linux | Windows | macOS |
|---|---|---|---|
| Open from the file manager into the running window | Tested (D-Bus single instance, desktop entry) | Built (named pipe single instance, *Open with* registration in the installer) | Built (Unix socket single instance, Finder open events, `CFBundleDocumentTypes`) |
| Navigate, search, Focus Mode | Tested (headless UI tests) | Shared code, built | Shared code, built |
| Annotate, fill, sign, save | Tested | Shared code, built | Shared code, built |
| Print | Tested (CUPS) | Built (print dialog, GDI printing in bands) | Built (CUPS, or Preview's print dialog) |
| Read Aloud | Tested with the real MeloTTS and Kokoro models through PulseAudio/PipeWire | Built (WASAPI) | Built (Core Audio AudioQueue) |
| Screen reader | Tested (AT-SPI tree walked in CI) | UI Automation from Avalonia, not tested | NSAccessibility from Avalonia, not tested |
| Packages | AppImage, deb, rpm, AUR | Inno Setup installer, portable zip | Signed `.app` in a `.dmg` |

## Viewing

| Feature | Acrobat Reader | PdfViewerLite | Status |
|---|---|---|---|
| Several documents | Tabs | Tabs in one window, session restore, reorder, reopen closed tabs; tab finder (Ctrl+Shift+A) and hover previews for hundreds of tabs, at most 16 documents kept in memory | Done |
| Page layouts | Single page, scrolling, two-up, cover page | Continuous, page by page, dual, dual with cover page | Done |
| Zoom | Fit width, fit page, marquee, free zoom | Fit width, fit page, free zoom; Ctrl+wheel zooms around the pointer | Done |
| Rotate view | Yes | Yes | Done |
| Full screen / presentation | Full screen mode | Present (Shift+F5), Esc to stop | Done |
| Read mode (chrome hidden) | Yes | Focus Mode covers reading; the toolbar stays | Partial |
| Page thumbnails | Yes | Virtualised, rendered on demand | Done |
| Bookmarks (outline) | Yes | Yes | Done |
| Layers | Yes | Show and hide optional content | Done |
| Attachments | Yes | Sidebar panel, save to a place you choose | Done |
| Links and history | Yes | Internal and external links, URLs written as plain text, back/forward | Done |
| Page labels | Yes | In the page box and thumbnails | Done |
| Document properties | Yes | Yes | Done |
| Password protected documents | Yes | Yes | Done |
| Reload on change | No | Automatic or a Reload bar | Done |
| Measuring tools | Yes | | Later |
| Other formats (images, DjVu, comics) | Images via conversion | Engine interface ready | Later |
| Tear tabs off into new windows | Yes | | Later |

## Search

| Feature | Acrobat Reader | PdfViewerLite | Status |
|---|---|---|---|
| Find in document | Yes | Incremental, match case, whole words, F3 / Shift+F3 | Done |
| Results list | Advanced Search panel | Search results sidebar | Done |
| Search scanned pages | Needs Pro OCR | *Recognise Text* adds a searchable layer with Tesseract | Done |
| Search across a folder of PDFs, indexes | Yes | | Later |

## Forms

| Feature | Acrobat Reader | PdfViewerLite | Status |
|---|---|---|---|
| Fill AcroForms | Yes | Text fields, check boxes, radio buttons, choice lists; Tab moves to the next field | Done |
| Save filled forms | Yes | Yes | Done |
| Form JavaScript (calculations, validation) | Yes | Not run | Later |
| XFA forms | Static XFA only | Not supported | Later |
| Submit forms over the web | Yes | | Out of scope |

## Annotations

| Feature | Acrobat Reader | PdfViewerLite | Status |
|---|---|---|---|
| Highlight, underline, strike-out | Yes | From selected text or the context menu | Done |
| Sticky notes, text boxes, freehand drawing | Yes | With colours, recolour, edit and delete | Done |
| Comment list | Yes | Annotations sidebar | Done |
| Shapes, stamps, arrows | Yes | Annotate ▸ Shape (rectangle, ellipse, arrow, line: drag to draw) and Stamp (Approved, Reviewed, Draft, Confidential, Final, Not Approved: click to place), in muted tones of the chosen colour; saved as standard PDF annotations | Done |
| Replies and review status on comments | Yes | | Later |
| Shared review in the cloud | Yes | | Out of scope |

## Signatures

| Feature | Acrobat Reader | PdfViewerLite | Status |
|---|---|---|---|
| Fill & Sign: drawn or typed signature | Yes | Yes | Done |
| Certificate (digital) signing | Yes | `.p12`/`.pfx`, incremental update with a detached CMS (SHA-256) signature; earlier signatures stay valid | Done |
| Validate signatures | Yes | List and check digital signatures | Done |
| Timestamp server, long-term validation | Yes | | Later |
| Request signatures (Adobe Sign) | Yes | | Out of scope |

## Printing

| Feature | Acrobat Reader | PdfViewerLite | Status |
|---|---|---|---|
| Print with preview | Yes | Preview with printer, copies, colour, two-sided, paper, page range, pages per sheet and annotations | Done |
| Native print dialog | Yes | XDG portal on Linux, the Windows print dialog, Preview's dialog on macOS | Done |
| Print to PDF | Through the system | Save as PDF in the preview | Done |
| Booklet and poster printing | Yes | | Later |

## Accessibility

| Feature | Acrobat Reader | PdfViewerLite | Status |
|---|---|---|---|
| Screen readers | Yes | Every control has an accessible name; checked by headless UI tests and over AT-SPI on Linux in CI | Done |
| Keyboard access | Yes | Every control reachable with Tab in a predictable order; shortcuts for every main action | Done |
| Caret browsing | Yes | F7: a steady cursor moved with the arrow keys, Shift selects | Done |
| High contrast and page colours | Replace document colours | Calm, High contrast, Dark and Light themes; soft page tones that keep highlight colours true | Done |
| Comfort | No | Reduced motion, steady caret, text labels on the tool bar, confirmation before closing several tabs ([COMFORT.md](COMFORT.md)) | Done |
| Reading order for assistive output | Tags, or its own guess | Columns, headings, lists, captions and footnotes put in order; page numbers and running headers dropped | Done |
| Tagged PDF structure | Yes | Reading order is inferred from layout, not read from tags | Partial |

## Read Aloud

| Feature | Acrobat Reader | PdfViewerLite | Status |
|---|---|---|---|
| Read the document aloud | Read Out Loud with system voices | MeloTTS neural voices on this computer (Australian by default, also British, American and Indian English), or Kokoro-82M; downloaded once from this project's GitHub release with consent and checked by SHA-256; nothing is sent anywhere | Done |
| Reading order | Its own reading order | The reading order above, so columns and footnotes are read sensibly | Done |
| Highlighting | No | Sentence marked and followed; optional word highlighting and a focus band that dims the rest | Done |
| Controls | Play, pause, stop | Play, pause, previous/next sentence (immediate); voice and speed changes resume from the current sentence | Done |
| Read from here | Read this page only | Right-click *Read Aloud from Here*, in the page view or Focus Mode | Done |
| Resume where you stopped | No | Position remembered per document | Done |
| Cloud voices | No | Optional Azure AI Speech with your own key; not the default | Done |

## Reflow

| Feature | Acrobat Reader | PdfViewerLite | Status |
|---|---|---|---|
| Reflow view | View ▸ Zoom ▸ Reflow | Focus Mode (Ctrl+4): the text in reading order, with font, size, line spacing, paragraph spacing, width and page colour | Done |
| Keep your place when switching | Partly | Switching between the page and Focus Mode keeps the reading position | Done |
| Liquid Mode | AI service | | Out of scope |

## Platform integration

| Feature | Acrobat Reader | PdfViewerLite | Status |
|---|---|---|---|
| Single running window | Yes | Linux D-Bus, Windows named pipe, macOS Unix socket and open events | Done |
| Recent documents | Yes | Start page plus the platform list: `recently-used.xbel`, Windows Recent items, macOS *Open Recent* | Done |
| Show in folder | Yes | Dolphin or the default file manager, Explorer, Finder | Done |
| Follow the desktop theme | Partly | Live from `kdeglobals`, the Windows registry, macOS appearance | Done |
| Open remote documents | Yes | http/https downloaded with Refit | Done |
| Drag and drop | Yes | Files and URLs | Done |
| Scanning to PDF | Pro | SANE on Linux, WIA on Windows, Image Capture on macOS | Later |
| Installers | Yes | AppImage, deb, rpm, AUR; Inno Setup and zip; `.dmg` | Done |

## Mobile

| Feature | Acrobat Reader | PdfViewerLite | Status |
|---|---|---|---|
| Android and iOS apps | Yes | Avalonia supports both, but no mobile app is planned for this milestone | Later |

## Out of scope

These are Adobe ecosystem features rather than reader features, and are not product goals:

- Adobe cloud storage, Document Cloud sync and sharing links.
- Shared reviews and other cloud collaboration.
- Adobe AI Assistant, generative summaries and Liquid Mode.
- Adobe Sign signature requests, and conversions that run on Adobe's servers.
- Accounts, subscriptions, and in-app upsell.

## How the desktop is reached

The app talks to the desktop only through `IDesktopPlatform` (`src/PdfViewerLite.Core/Platform`): theme, file
manager, recent documents, printing, sound output and the single running window. `DesktopPlatforms.Detect` picks
`WindowsPlatform`, `MacPlatform` or `KdePlatform`. The freedesktop parts of `KdePlatform` work on other Linux
desktops, and `FallbackPlatform` is used where none of these apply.
