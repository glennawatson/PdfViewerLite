# HyperPdf feature matrix

HyperPdf is the managed C# PDF engine available alongside PDFium in PdfViewerLite. This page records which app operations HyperPdf handles today and the remaining gaps. HyperPdf does not call PDFium; users can select PDFium as a separate engine per document.

Sources: the app interfaces in `src/PdfViewerLite.Core`, the adapter in `src/PdfViewerLite.HyperPdf`, the engine in `src/HyperPdfLibrary`, tests in `tests/HyperPdfLibrary.Tests` and `tests/PdfViewerLite.HyperPdf.Tests`, and PDFium headers in `~/source/pdf/pdfium/public`.

## States

| State | Meaning |
| --- | --- |
| Managed | HyperPdfLibrary or the HyperPdf adapter answers the call. |
| Partial | Managed code answers the call for supported cases; the row names known gaps. |
| Detection only | HyperPdf reports that the feature is present but does not run or render it. |
| Unsupported | HyperPdf has no implementation. The app may still offer the separate PDFium engine. |

HyperPdf reads JavaScript, XFA packets, multimedia and 3D objects as data. It does not execute scripts, render XFA, or play media. PDFium remains a separately selectable engine, not a compatibility backend used by HyperPdf.

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
| `Render` | Managed | RenderParityTests, FontRenderParityTests | Uses `PdfPageRenderer`; JBIG2 and JPX image decoders are managed, with mixed JPX high-throughput mode refused. |
| `GetCharacterCount`, `GetText` | Managed, for supported font mappings | TextParityTests, TextCorpusParityTests, CjkTextParityTests | Uses cached `PdfTextPage`; unsupported font encodings and complex scripts can affect text. |
| `GetCharacterIndexAt`, `GetTextBounds` | Managed, for extracted glyphs | TextGeometryParityTests, TextParityTests | Geometry parity covers the tested corpus. |
| `Find` | Managed, for extracted text | TextParityTests, TextQueryTests | Case and whole-word options are implemented; complex-script shaping and normalization remain limited. |
| `Dispose` | Managed | DocumentParityTests | Disposes the managed document and renderer. |

### IAnnotationEditor (`HyperPdfDocument.Editing.cs`, `HyperPdfAnnotations*.cs`)

The adapter reads, edits and saves annotations through the managed object store. Changes appear in managed rendering and are written by the managed writer.

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `HasUnsavedChanges`, `Author` (get and set) | Managed | NativeAnnotationEditingTests | Edit state and author are held by the managed adapter. |
| `GetAnnotations` | Managed | AnnotationParityTests, NativeAnnotationTests | Reads the managed annotation dictionaries and appearances. |
| `AddMarkup` | Managed | AnnotationParityTests, NativeAnnotationTests | Managed adapter call. |
| `AddInk` | Managed | AnnotationParityTests, NativeAnnotationTests | Managed adapter call. |
| `AddNote` | Managed | AnnotationParityTests, NativeAnnotationTests | Managed adapter call. |
| `AddText` | Managed | AnnotationParityTests, NativeAnnotationTests | Uses embedded or standard fonts where supported. |
| `AddShape` | Managed | AnnotationParityTests, NativeAnnotationTests | Managed adapter call. |
| `AddStamp` | Managed | AnnotationParityTests, NativeAnnotationTests | Managed adapter call. |
| `AddImageStamp` | Managed | AnnotationParityTests, NativeAnnotationTests | Managed adapter call. |
| `AddPolygon` | Managed | AnnotationParityTests, NativeAnnotationTests | Managed adapter call. |
| `AddCallout` | Managed | AnnotationParityTests, NativeAnnotationTests | Managed adapter call. |
| `GetReplies`, `AddReply` | Managed | AnnotationParityTests, NativeAnnotationTests | Managed adapter call. |
| `SetColor`, `SetContents`, `SetBounds`, `SetLineWidth`, `SetFontSize` | Managed | NativeAnnotationEditingTests | Managed adapter call. |
| `SetRemoved`, `Remove` | Managed | NativeAnnotationEditingTests | Managed adapter call. |
| `Save` | Managed | NativeAnnotationEditingTests, IncrementalWriterTests, CompactWriterTests | Saves managed edits through `PdfIncrementalWriter`. |

### ITextBoxEditor

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `AddTextBox` | Managed | NativeTextBoxTests | Managed adapter call. |
| `GetTextBox` | Managed | NativeTextBoxTests | Managed adapter call. |
| `GetFirstBaseline` | Managed | NativeTextBoxTests | Uses `TextBoxFonts.cs` and `StandardTextShaper.cs`. |

### IImageSignatureEditor

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `AddImageSignature` | Managed | NativeAnnotationTests | Uses `HyperPdfAnnotations.Images.cs`. |

### IFormFiller

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `HasForm`, `GetFields` | Managed | FormTests, FormParityTests | Reads the managed AcroForm field tree. |
| `SetText`, `SetChecked`, `SelectOption` | Managed | FormTests, FormParityTests, CombFormTests | Writes managed field values and regenerates supported appearances. Buttons, rich text and legacy multibyte field fonts have gaps; see coverage section 4. |

### IFormScriptSource

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `GetScripts` | Managed, read only | FormScriptReadingTests | Reads field scripts as data; general JavaScript execution is not offered. |

### ITaggedStructureSource

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `GetTaggedBlocks` | Managed, when tags cover the page text | TaggedParityTests, NativeTaggedSource | Reads the managed structure tree and marked content. Pages without reliable tags return false. |

### ITextLayerWriter

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `AddTextLayer` | Managed | NativeTextLayerTests | Writes an invisible OCR text layer with the managed writer. |

### ITextLayoutSource

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `GetCharacters` | Managed, for supported font mappings | TextParityTests, TextGeometryParityTests | Uses the managed text page and glyph positions. |

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
| `SetLayerVisible` | Managed | OptionalContentTests | Updates the managed optional-content state used by rendering. |

### IPageExporter (`HyperPdfDocument.Export.cs`)

| Member | State | Test evidence | Migration step |
| --- | --- | --- | --- |
| `ExportPages` | Managed | PageExportTests, PageExportParityTests, PageImporterTests | Uses the managed importer and writer. |

## 2. PDFium API groups

"App uses" lists the FPDF calls found in `src/PdfViewerLite.Pdfium/Native/NativeMethods*.cs`. Counts of header functions come from `public/*.h`.

| Header (functions) | App uses | HyperPdf state | Test evidence | Notes and migration step |
| --- | --- | --- | --- | --- |
| fpdfview (61) | Load, close, page count, page size, `FPDF_GetMetaText`, page labels, file version, security revision, `FPDF_RenderPageBitmap`, bitmaps, coordinate mapping, last error | Managed for open, sizes, labels, metadata and rendering; some PDFium-only diagnostics are not exposed | DocumentParityTests, RenderParityTests | HyperPdf renders through `PdfPageRenderer`. |
| fpdf_text (37) | Load page, count and get chars, char boxes, font size and weight, rects, char at position, find start and next, web links | Managed for supported fonts and text | TextParityTests, TextGeometryParityTests, TextQueryTests, CjkTextParityTests | Complex scripts and unsupported font mappings remain limited. |
| fpdf_annot (67) | Subtype, rect, colour, border, flags, ink lists, vertices, attachment points, string values, AP, objects, linked annots, form field name, type, flags, value, options, checked | Managed for supported annotations and AcroForm fields | AnnotationParityTests, NativeAnnotationTests, AnnotationDictionaryTests, PageAnnotationTests, FormParityTests | Annotation and form coverage has the limits noted in sections 4 and 6. |
| fpdf_edit (131) | Create doc and page, path and rect objects, text objects, image objects, page boxes, rotation, `FPDFPage_GenerateContent`, `FPDFPage_Flatten`, fonts | Managed for document building, import, page export, annotation and text-layer edits; flatten remains unsupported | WriterFixTests, ContentBuilderTests, PageImporterTests, NativeTextLayerTests | `PdfContentBuilder`, `PdfDocumentBuilder` and the managed editing adapter cover these operations. |
| fpdf_doc (31) | Bookmarks, actions, destinations, links, `FPDFLink_Enumerate`, link rects, URIs | Managed | NavigationTests, DocumentParityTests | None. Named destinations come from `NameTree.cs`. |
| fpdf_formfill (17) | Init and exit environment, `FORM_OnChar`, replace selection, select all, set focus, `FPDF_FFLDraw`, highlight colours | Managed for field reading, edits, scripts-as-data and appearance rendering; some field appearance cases remain partial | FormTests, FormParityTests, FormAppearanceTests, FormRuntimeTests | No general JavaScript runtime. See coverage section 4. |
| fpdf_structtree (35) | Struct tree for page, children, type, alt text, actual text, marked content ids | Managed structure reading and marked-content mapping; partial app exposure | StructureTreeTests, MarkedContentTests, TaggedParityTests | Structure reading order is exposed when tag coverage is reliable. |
| fpdf_ppo (7) | `FPDF_ImportPagesByIndex`, `FPDF_ImportNPagesToOne` | Managed | PageImporterTests, PageExportTests, PageExportParityTests | `PdfPageImporter`, `PdfObjectImporter`. |
| fpdf_save (4) | `FPDF_SaveAsCopy` with incremental and full modes | Managed incremental and compact save | IncrementalWriterTests, CompactWriterTests, ObjectWriterTests, WriterFixTests, NativeAnnotationEditingTests | The annotation adapter saves managed edits. |
| fpdf_signature (10) | Signature count, object, byte range, contents, reason, sub-filter, time | Managed | AttachmentSignatureParityTests | `PdfSignatureField.cs`. No cryptographic validation. |
| fpdf_attachment (15) | Count, get, name, file, size | Managed | AttachmentSignatureParityTests | `PdfAttachment.cs`, `PdfDocumentFileSpecs.cs`. |
| fpdf_catalog (3) | `FPDFCatalog_IsTagged` | Managed reading | AccessibilityDocumentTests, StructureTreeTests | `PdfStructureTree` reads `MarkInfo`. |
| fpdf_javascript (5) | `FPDFDoc_GetJavaScriptActionCount`, field additional actions | Managed detection. Execution not offered | ContentCheckTests | HyperPdf reports presence; PDFium is a separate engine choice. |
| fpdf_thumbnail (3) | Not used in the app | Partial managed library support | ThumbnailRenderTests | The library can decode supported embedded `/Thumb` images or render its own page previews; the app does not map the full FPDF thumbnail API. |
| fpdf_transformpage (22) | `FPDFPage_SetMediaBox`, `SetCropBox`, `TransFormWithClip` | Managed in the exporter | PageExportTests, PageExportParityTests | `HyperPdfDocument.Export.cs` and `PdfFormPlacement.cs`. |
| fpdf_flatten (1) | `FPDFPage_Flatten` | Unsupported in HyperPdf | none | Flattening is not exposed by the managed adapter. |
| fpdf_progressive (6) | Not used | Managed equivalent | ProgressiveRenderTests | HyperPdf has a managed progressive render API. |
| fpdf_searchex (3) | Not used | Unsupported | none | Not needed. |
| fpdf_sysfontinfo (7) | Not used by name. System fonts come from managed matching and on-demand open font assets | Managed | SystemFontTests, BundledFaceTests, FontDataDemandTests | System font discovery is cross-platform managed code. |
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
| JBIG2 decode | Managed | Jbig2ImageDecoderTests, Jbig2DecoderTests, Jbig2RobustnessTests | `Graphics/Images/Jbig2/` handles page segments, supported region/dictionary types and `/JBIG2Globals`; damaged input keeps decoded pixels or fails safely. |
| JPX (JPEG 2000) decode | Partial | JpxImageDecoderTests, JpxDecoderTests, JpxHighThroughputTests | `Graphics/Images/Jpx/` decodes regular and high-throughput blocks; the MIXED high-throughput mode is refused, as in PDFium. |
| Image decode to pixels (`PdfImageDecoder`, `SampleUnpacker`) | Managed | ImageDecoderTests, ImageFixTests | |
| Colour spaces: Device, Cal, Lab, ICC, Indexed, Separation, DeviceN (`Graphics/Colors/`) | Managed | ColorSpaceTests, ColorFixTests, IccProfileTests | Pattern colour space is parsed only. |
| Functions: sampled, exponential, stitching, PostScript | Managed | FunctionTests, FunctionFixTests | |
| Security: RC4 and AES, handler revisions 2 to 6 (`Security/`) | Managed | CipherTests, SecurityFixTests | |
| Writer: incremental, compact, object streams, builder, importer (`Writing/`) | Managed | IncrementalWriterTests, CompactWriterTests, ObjectWriterTests, ContentBuilderTests, PageImporterTests, WriterFixTests | |
| Optional content layers (`Layers/`) | Managed | OptionalContentTests | |
| Annotation dictionaries and appearances (`Annotations/`) | Managed | AnnotationDictionaryTests, PageAnnotationTests | |
| Font programs and PDF font loading (`Fonts/`) | Partial overall | Type1FontTests, TrueTypeFontTests, CffProgramTests, CompositeFontTests, CjkFontTests, PredefinedCMapTests, BundledFaceTests, FontRenderTests | Page fonts load and render for supported encodings. Unsupported scripts, MMType1 and some substitution details remain gaps. Requested CMaps and open font faces are generated on demand from pinned upstream data and cached; no font or CMap binaries are checked in. |
| Content interpreter (`Content/`) | Managed, with unsupported operators and codecs | TextRenderTests, ShapeRenderTests, TransparencyRenderTests, PageRenderTests | Interprets supported text, path, image, shading, form and marked-content operators. See coverage sections 5 and 6 for gaps. |
| Renderer (`Rendering/`) | Managed, with known gaps | PageRenderTests, RenderAllocationTests, ProgressiveRenderTests, ThumbnailRenderTests | Skia renderer; no minimum line width, JPX mixed high-throughput mode is refused, and some transparency/shading cases remain approximate or unsupported. |
| Text extraction and layout | Managed, for supported font mappings | TextExtractionTests, TextQueryTests, TextPageCacheTests, TextParityTests, TextGeometryParityTests | Uses the content interpreter and PDF font loader. Complex script shaping and some reading order cases remain limited. |
| Structure tree reader | Partial | StructureTreeTests, MarkedContentTests, TaggedParityTests | Reads structure roles, attributes, marked content and mappings; app accepts tags only when coverage checks pass. |
| AcroForm field model | Partial | FormTests, FormAppearanceTests, FormRuntimeTests | Managed field reads, edits and supported appearances; some widget appearance and general JavaScript cases remain unsupported. |
| XMP metadata read and edit | Partial | XmpTests, MetadataEditTests, XmpEditorTests | Catalog, page and object packets can be read. Selected `/Info` and existing XMP properties can be edited; the app metadata view still uses `/Info`. RDF nesting and value types remain limited. |
| FDF and XFDF interchange | Partial | FdfTests, XfdfReadTests, InterchangeRoundTripTests | The library imports and exports supported fields and annotations. The app has no import or export command; full format conformance is not claimed. |
| XFA packets, JavaScript and multimedia data | Managed reading only | CatalogStructureTests, ActionDataTests, MultimediaTests | Reads XFA packet bytes, document scripts, media, 3D and RichMedia objects. Script execution, XFA rendering, media playback and 3D rendering are not provided. |
| Portfolio, associated files and catalog features | Managed reading only | PortfolioTests, CatalogStructureTests, ExtensionTests | Reads collections, associated files, viewer preferences, transitions, output intents, developer extensions, measures, document parts and web capture. Most are not exposed in the app. |
| Page content objects and true redaction | Partial overall | PageObjectParseTests, PageObjectEditTests, RedactionTextTests, RedactionApplyTests | Library edits supported page objects and applies content-removing redaction; embedded glyph programs are not trimmed. See coverage section 15. |
| Accessibility and conformance reports | Partial | AccessibilityDocumentTests, AccessibilityStructureTests, PdfConformanceReportTests, RasterReportTests | Reads PDF/UA, PDF/A and PDF/R claims and reports selected structural findings. It does not certify conformance or repair all accessibility faults. |

## 4. Current coverage and remaining gaps

Order is by value and by what each step needs first.

Rendering, text extraction and search, annotations, forms, text-layer writing, structure reading, and page export have managed implementations and adapter wiring. The library also reads XMP, XFA packet data and interactive catalog structures; FDF/XFDF import and export exist in the library without app commands. Remaining limits include JPX mixed high-throughput mode, minimum line width, selected font encodings and complex-script text, unsupported annotation and form appearances, and richer tagged-PDF interpretation. JavaScript execution, XFA rendering, media playback and 3D rendering are not provided by HyperPdf. PDFium remains a separate engine and parity baseline.
