# HyperPdf feature matrix

HyperPdf is the managed C# PDF engine that replaces PDFium in PdfViewerLite. This page shows what is native today, what still runs on PDFium, and what blocks each step.

Sources: the app interfaces in `src/PdfViewerLite.Core`, the adapter in `src/PdfViewerLite.HyperPdf`, the engine in `src/HyperPdfLibrary`, and the PDFium headers in `~/source/pdf/pdfium/public`.

## States

| State | Meaning |
| --- | --- |
| Managed | HyperPdfLibrary code answers the call. PDFium is not used. |
| Compatibility-backed | The adapter forwards the call to a PDFium copy of the file. The copy is opened on first use (`Compat` in `HyperPdfDocument.Compat.cs`). |
| Managed, not wired | Managed code exists and is tested, but the adapter still forwards to PDFium. |
| Unsupported | Neither engine handles it. |

JavaScript and XFA stay compatibility-backed by design. HyperPdf only detects them.

## 1. App interfaces

### IDocumentEngine (`HyperPdfEngine.cs`)

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `Name` | Managed | DocumentParityTests | None |
| `CanOpen` | Managed | DocumentParityTests | None |
| `Open` | Managed | DocumentParityTests, DocumentFixTests, SecurityFixTests, StructureFixTests | None. Opening also reads page sizes. Errors map to `DocumentOpenError`. |

### IDocument (`HyperPdfDocument.cs`)

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `FilePath`, `PageCount`, `IsDisposed` | Managed | DocumentParityTests | None |
| `GetPageSizes` | Managed | DocumentParityTests, PageTreeTests | None |
| `GetMetadata` | Managed | DocumentParityTests, DocumentTests | None |
| `GetPageLabel` | Managed | DocumentParityTests, DocumentTests | None |
| `GetOutline` | Managed | NavigationTests, DocumentParityTests | None |
| `GetLinks` | Managed | NavigationTests, DocumentParityTests | None |
| `Render` | Compatibility-backed | none | Needs the content interpreter and renderer (step 1). |
| `GetCharacterCount` | Compatibility-backed | none | Needs text extraction (step 2). |
| `GetCharacterIndexAt` | Compatibility-backed | none | Needs text extraction (step 2). |
| `GetText` | Compatibility-backed | none | Needs text extraction (step 2). |
| `GetTextBounds` | Compatibility-backed | none | Needs text extraction (step 2). |
| `Find` | Compatibility-backed | none | Needs text extraction (step 2). |
| `Dispose` | Managed | DocumentTests | None. Also disposes the PDFium copy if open. |

### IAnnotationEditor (`HyperPdfDocument.Compat.cs`, `HyperPdfAnnotations*.cs`)

Every member is forwarded to PDFium today. The managed editor is the internal `HyperPdfDocument.Annotations` property (`HyperPdfDocument.Annotations.cs`) and no adapter member calls it yet.

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `HasUnsavedChanges` | Compatibility-backed | none | Switch with `Save` (step 3). |
| `Author` (get and set) | Compatibility-backed | none | Switch with `Save` (step 3). Reading it opens the PDFium copy. |
| `GetAnnotations` | Managed, not wired | AnnotationParityTests, NativeAnnotationTests | Step 3. |
| `AddMarkup` | Managed, not wired | AnnotationParityTests, NativeAnnotationTests | Step 3. |
| `AddInk` | Managed, not wired | AnnotationParityTests, NativeAnnotationTests | Step 3. |
| `AddNote` | Managed, not wired | AnnotationParityTests, NativeAnnotationTests | Step 3. |
| `AddText` | Managed, not wired | AnnotationParityTests, NativeAnnotationTests | Step 3. Uses embedded or standard fonts. |
| `AddShape` | Managed, not wired | AnnotationParityTests, NativeAnnotationTests | Step 3. |
| `AddStamp` | Managed, not wired | AnnotationParityTests, NativeAnnotationTests | Step 3. |
| `AddImageStamp` | Managed, not wired | AnnotationParityTests, NativeAnnotationTests | Step 3. |
| `AddPolygon` | Managed, not wired | AnnotationParityTests, NativeAnnotationTests | Step 3. |
| `AddCallout` | Managed, not wired | AnnotationParityTests, NativeAnnotationTests | Step 3. |
| `GetReplies`, `AddReply` | Managed, not wired | AnnotationParityTests, NativeAnnotationTests | Step 3. |
| `SetColor`, `SetContents`, `SetBounds`, `SetLineWidth`, `SetFontSize` | Managed, not wired | NativeAnnotationEditingTests | Step 3. |
| `SetRemoved`, `Remove` | Managed, not wired | NativeAnnotationEditingTests | Step 3. |
| `Save` | Managed, not wired | NativeAnnotationEditingTests, IncrementalWriterTests, CompactWriterTests | Step 3. Uses `PdfIncrementalWriter`. |

### ITextBoxEditor

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `AddTextBox` | Managed, not wired | NativeTextBoxTests | Step 3. |
| `GetTextBox` | Managed, not wired | NativeTextBoxTests | Step 3. |
| `GetFirstBaseline` | Managed, not wired | NativeTextBoxTests | Step 3. Uses `TextBoxFonts.cs` and `StandardTextShaper.cs`. |

### IImageSignatureEditor

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `AddImageSignature` | Managed, not wired | NativeAnnotationTests | Step 3. Uses `HyperPdfAnnotations.Images.cs`. |

### IFormFiller

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `HasForm` | Compatibility-backed | none | Step 6. The library can read the AcroForm dictionary (`HasAcroForm`) but has no field model. |
| `GetFields` | Compatibility-backed | none | Step 6. |
| `SetText` | Compatibility-backed | none | Step 6. Needs appearance regeneration. |
| `SetChecked` | Compatibility-backed | none | Step 6. |
| `SelectOption` | Compatibility-backed | none | Step 6. |

### IFormScriptSource

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `GetScripts` | Compatibility-backed | none | Stays on PDFium by design. |

### ITaggedStructureSource

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `GetTaggedBlocks` | Compatibility-backed | none | Step 5. Needs the structure tree and marked content ids from the interpreter. |

### ITextLayerWriter

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `AddTextLayer` | Compatibility-backed | none | Step 4. Needs the writer, content builder and font embedding. |

### ITextLayoutSource

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `GetCharacters` | Compatibility-backed | none | Step 2. Needs glyph positions, font size and weight from the interpreter. |

### IContentCheck (`HyperPdfDocument.ContentCheck.cs`)

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `CheckDocument` | Managed | ContentCheckTests | None. Reports XFA and JavaScript. |
| `CheckPage` | Managed | ContentCheckTests | None. Reports multimedia, 3D and unknown field scripts. |

### IAttachmentSource (`HyperPdfDocument.Attachments.cs`)

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `GetAttachments` | Managed | AttachmentSignatureParityTests | None |
| `SaveAttachment` | Managed | AttachmentSignatureParityTests | None |

### ISignatureSource (`HyperPdfDocument.Attachments.cs`)

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `SignatureCount` | Managed | AttachmentSignatureParityTests | None |
| `GetSignatures` | Managed | AttachmentSignatureParityTests | None. Reads only. No signing or validation. |

### ILayerSource (`HyperPdfDocument.Layers.cs`)

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `GetLayers` | Managed | OptionalContentTests | None |
| `SetLayerVisible` | Managed | OptionalContentTests | None. If the PDFium copy is open, the choice is sent to it too, and replayed when the copy opens (`ReplayLayers`). Drop that bridge when `Render` is native. |

### IPageExporter (`HyperPdfDocument.Export.cs`)

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `ExportPages` | Managed, with a PDFium fallback | PageExportTests, PageExportParityTests, PageImporterTests | Falls back to PDFium only when the PDFium copy holds unsaved edits. Goes away with step 3. |

## 2. PDFium API groups

"App uses" lists the FPDF calls found in `src/PdfViewerLite.Pdfium/Native/NativeMethods*.cs`. Counts of header functions come from `public/*.h`.

| Header (functions) | App uses | HyperPdf state | Test evidence | Notes and migration step |
| --- | --- | --- | --- | --- |
| fpdfview (61) | Load, close, page count, page size, `FPDF_GetMetaText`, page labels, file version, security revision, `FPDF_RenderPageBitmap`, bitmaps, coordinate mapping, last error | Mixed. Open, sizes, labels, metadata: Managed. Render: Compatibility-backed | DocumentParityTests, DocumentTests | Render is step 1. |
| fpdf_text (37) | Load page, count and get chars, char boxes, font size and weight, rects, char at position, find start and next, web links | Compatibility-backed | none | Step 2. Needs fonts and the interpreter. Find also needs normalisation to match PDFium. |
| fpdf_annot (67) | Subtype, rect, colour, border, flags, ink lists, vertices, attachment points, string values, AP, objects, linked annots, form field name, type, flags, value, options, checked | Annotations: Managed, not wired. Form field reads: Compatibility-backed | AnnotationParityTests, NativeAnnotationTests, AnnotationDictionaryTests, PageAnnotationTests | Annotations are step 3. Form fields are step 6. |
| fpdf_edit (131) | Create doc and page, path and rect objects, text objects, image objects, page boxes, rotation, `FPDFPage_GenerateContent`, `FPDFPage_Flatten`, fonts | Compatibility-backed for text layer, flatten and image signatures. Page boxes, import: Managed | WriterFixTests, ContentBuilderTests, PageImporterTests | `PdfContentBuilder` and `PdfDocumentBuilder` cover creation. Text layer is step 4. |
| fpdf_doc (31) | Bookmarks, actions, destinations, links, `FPDFLink_Enumerate`, link rects, URIs | Managed | NavigationTests, DocumentParityTests | None. Named destinations come from `NameTree.cs`. |
| fpdf_formfill (17) | Init and exit environment, `FORM_OnChar`, replace selection, select all, set focus, `FPDF_FFLDraw`, highlight colours | Compatibility-backed | none | Step 6. Needs rendering of widgets and appearance streams. |
| fpdf_structtree (35) | Struct tree for page, children, type, alt text, actual text, marked content ids | Compatibility-backed | none | Step 5. |
| fpdf_ppo (7) | `FPDF_ImportPagesByIndex`, `FPDF_ImportNPagesToOne` | Managed | PageImporterTests, PageExportTests, PageExportParityTests | `PdfPageImporter`, `PdfObjectImporter`. |
| fpdf_save (4) | `FPDF_SaveAsCopy` with incremental and full modes | Managed (library). Adapter save is Compatibility-backed | IncrementalWriterTests, CompactWriterTests, ObjectWriterTests, WriterFixTests | Adapter `Save` moves with step 3. |
| fpdf_signature (10) | Signature count, object, byte range, contents, reason, sub-filter, time | Managed | AttachmentSignatureParityTests | `PdfSignatureField.cs`. No cryptographic validation. |
| fpdf_attachment (15) | Count, get, name, file, size | Managed | AttachmentSignatureParityTests | `PdfAttachment.cs`, `PdfDocument.FileSpecs.cs`. |
| fpdf_catalog (3) | `FPDFCatalog_IsTagged` | Compatibility-backed | none | Trivial to move: read `MarkInfo` from the catalog. Do it with step 5. |
| fpdf_javascript (5) | `FPDFDoc_GetJavaScriptActionCount`, field additional actions | Managed detection. Execution not offered | ContentCheckTests | JavaScript stays compatibility-backed by design. |
| fpdf_thumbnail (3) | Not used | Unsupported | none | Not needed. The app renders its own thumbnails. |
| fpdf_transformpage (22) | `FPDFPage_SetMediaBox`, `SetCropBox`, `TransFormWithClip` | Managed in the exporter | PageExportTests, PageExportParityTests | `HyperPdfDocument.Export.cs` and `PdfFormPlacement.cs`. |
| fpdf_flatten (1) | `FPDFPage_Flatten` | Compatibility-backed | none | Needs the renderer or appearance merge. After step 3. |
| fpdf_progressive (6) | Not used | Unsupported | none | Not needed. |
| fpdf_searchex (3) | Not used | Unsupported | none | Not needed. |
| fpdf_sysfontinfo (7) | Not used by name. System fonts come through PDFium | Managed (library has system font lookup) | SystemFontTests | See `Fonts/`. Step 1 and 2 depend on it. |
| fpdf_ext (1) | Not used | Unsupported | none | Not needed. |

## 3. Library capabilities (`src/HyperPdfLibrary`)

| Feature | State | Test evidence | Notes |
| --- | --- | --- | --- |
| Lexer and parser (`Syntax/`) | Managed | ParserTests, StructureFixTests | |
| Object model (`Objects/`) | Managed | ParserTests | |
| Xref tables, xref streams, object streams (`Structure/`) | Managed | ParserTests, StructureFixTests | |
| Xref repair (`XrefRepair.cs`) | Managed | StructureFixTests, DocumentFixTests | Rebuilds files with broken xref. |
| Page tree and inherited attributes (`Document/`) | Managed | PageTreeTests | |
| Page labels, outline, links, name trees | Managed | NavigationTests, DocumentTests | |
| Stream filters: Flate, LZW, ASCII85, ASCIIHex, RunLength, predictors (`Filters/`) | Managed | FilterTests, FilterFixTests | |
| DCT (JPEG) decode (`Graphics/Images/Jpeg/`) | Managed | JpegDecoderTests, JpegIdctTests, JpegImageDecoderTests | |
| CCITT fax decode | Managed | CcittFaxDecoderTests | |
| JBIG2 decode | Unsupported | none | `PdfImageCodec.Jbig2` is named but no decoder exists. Scanned pages with JBIG2 need PDFium. |
| JPX (JPEG 2000) decode | Unsupported | none | `PdfImageCodec.Jpx` is named but no decoder exists. |
| Image decode to pixels (`PdfImageDecoder`, `SampleUnpacker`) | Managed | ImageDecoderTests, ImageFixTests | |
| Colour spaces: Device, Cal, Lab, ICC, Indexed, Separation, DeviceN (`Graphics/Colors/`) | Managed | ColorSpaceTests, ColorFixTests, IccProfileTests | Pattern colour space is parsed only. |
| Functions: sampled, exponential, stitching, PostScript | Managed | FunctionTests, FunctionFixTests | |
| Security: RC4 and AES, handler revisions 2 to 6 (`Security/`) | Managed | CipherTests, SecurityFixTests | |
| Writer: incremental, compact, object streams, builder, importer (`Writing/`) | Managed | IncrementalWriterTests, CompactWriterTests, ObjectWriterTests, ContentBuilderTests, PageImporterTests, WriterFixTests | |
| Optional content layers (`Layers/`) | Managed | OptionalContentTests | |
| Annotation dictionaries and appearances (`Annotations/`) | Managed | AnnotationDictionaryTests, PageAnnotationTests | |
| Font programs: CFF, Type 1, TrueType, CMaps, standard 14 metrics, glyph lists (`Fonts/`) | Managed | CffProgramTests, Type1ProgramTests, CMapTests, GlyphListTests, StandardFontsTests, SystemFontTests | Programs parse and emit outlines. No PDF font loader ties them to page text yet. |
| Content stream tokenizer (`Content/`) | Managed | ParserTests | Reads operators and operands only. |
| Content interpreter (graphics state, text state, XObjects, patterns, shading) | In progress | none | Not in the tree. Blocks rendering and text. |
| Renderer (rasterizer, clipping, blending) | In progress | none | Not in the tree. `HotPathAllocationTests` guards the pieces that exist. |
| Text extraction and layout | In progress | none | Needs the interpreter and PDF font loader. |
| Structure tree reader | Unsupported | none | Not started. |
| AcroForm field model | Unsupported | none | Not started. |
| JavaScript and XFA | Compatibility-backed by design | ContentCheckTests (detection only) | Never planned for managed code. |

## 4. Migration plan

Order is by value and by what each step needs first.

1. **Render.** Moves `Render`. Blocked by the content interpreter, the renderer, a PDF font loader that joins `Fonts/Programs` to page fonts, and image paths for JBIG2 and JPX. Until JBIG2 and JPX decode, pages that use them must fall back to PDFium per page, not per document.
2. **Text extraction.** Moves `GetCharacterCount`, `GetCharacterIndexAt`, `GetText`, `GetTextBounds`, `Find` and `GetCharacters`. Blocked by the interpreter (glyph positions) and the font loader (ToUnicode via `CMap/`, encodings, standard font metrics). Needs parity tests through `EnginePair` for character counts, bounds and search hits.
3. **Annotation editing and save.** Wire `HyperPdfDocument.Annotations` into `IAnnotationEditor`, `ITextBoxEditor` and `IImageSignatureEditor`, including `Save`, `HasUnsavedChanges` and `Author`. The code and tests exist (`NativeAnnotationTests`, `NativeAnnotationEditingTests`, `NativeTextBoxTests`), but wiring waits for step 1. Until then pages draw through the PDFium copy, which would not show native edits, and `Save` would drop form and text-layer edits held in that copy. The remaining work is the adapter wiring, one source of truth for edits, and removing the `Compat.ExportPages` fallback. Reading existing annotations in `GetAnnotations` needs the same index order as PDFium, which `AnnotationParityTests` checks.
4. **Text layer writing (OCR).** Moves `AddTextLayer`. Needs invisible text content plus an embedded font. Uses `PdfContentBuilder` and the writer. Blocked by font embedding, not by rendering.
5. **Structure tree.** Moves `GetTaggedBlocks` and `FPDFCatalog_IsTagged`. Blocked by marked content ids from the interpreter and by a structure tree reader.
6. **Forms.** Moves `HasForm`, `GetFields`, `SetText`, `SetChecked`, `SelectOption`. Blocked by an AcroForm field model, appearance regeneration, and widget rendering (step 1).
7. **Flatten.** Moves `FPDFPage_Flatten` use. Blocked by steps 1 and 3.
8. **Make HyperPdf the default engine.** Possible only when steps 1 to 7 are done. PDFium stays in the app as a second engine the user can pick per document, for comparison and testing. The parity tests keep running against it. JavaScript and XFA stay compatibility-backed by design.
