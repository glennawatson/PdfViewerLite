# HyperPdf PDF coverage tracker

This page tracks how much of PDF 1.x (ISO 32000-1) and PDF 2.0 (ISO 32000-2) HyperPdfLibrary can read and render. It is a map by spec clause. For app operations (`IDocument`, `IFormFiller` and so on) and PDFium API groups, see the [feature matrix](feature-matrix.md). This page does not repeat it.

Sections 1 to 13 mainly track reading and rendering; the XMP edit row records its related save path. Sections 14 and 15 describe optimisation, page object editing and redaction separately. App operations and PDFium API groups are in the feature matrix.

## How to read the tables

| Column | Meaning |
| --- | --- |
| Status | Done, Partial, Missing, Read only, Compat (PDFium) or Out of scope. See below. |
| HyperPdf | Files under `src/HyperPdfLibrary`. "none" means no code. |
| Tests | Test classes in `tests/HyperPdfLibrary.Tests` or `tests/PdfViewerLite.HyperPdf.Tests` that I opened and that touch the row. "none found" means no test class covers it. |
| App | Whether PdfViewerLite uses the feature today through either engine. |
| PDFium | Whether PDFium supports it, from the source in `~/source/pdf/pdfium`. |
| Issue | The GitHub issue that tracks the gap. |

| Status | Meaning |
| --- | --- |
| Done | Implemented and tested. Known small gaps are in the notes. |
| Partial | Some of the clause works. The notes say what does not. |
| Missing | No code. |
| Compat (PDFium) | The feature is available through the separately selected PDFium engine; HyperPdf does not call PDFium as a fallback. |
| Read only | HyperPdf reads the data but does not execute or apply it. |
| Out of scope | The feature is not a reading or rendering concern for HyperPdf. |

Test evidence is by class name. Some rows share a class, such as `ParserTests`, because one class covers several clauses. I did not open every test method, so a class name means "this class covers the area", not "every case in the row".

## 1. File structure

Clause 7.5 and Annex F.

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Header, version, junk before header | 7.5.2 | Done | `Structure/XrefReader.cs`, `Objects/PdfObjectStore.Open.cs` | ParserTests, WriterFixTests | Yes | Yes | |
| Body, indirect objects, generation numbers | 7.5.3 | Done | `Objects/PdfObjectStore.cs`, `Objects/PdfObjectStore.Parsing.cs` | ParserTests, StructureFixTests | Yes | Yes | |
| Classic xref tables | 7.5.4 | Done | `Structure/XrefReader.cs`, `Structure/XrefTable.cs` | ParserTests, XrefBoundsTests | Yes | Yes | |
| Xref streams | 7.5.8 | Done | `Structure/XrefReader.cs` | ParserTests, XrefBoundsTests | Yes | Yes | |
| Object streams | 7.5.7 | Done | `Structure/ObjectStreamIndex.cs`, `Structure/ObjectStreamCache.cs` | ParserTests, EncryptedRepairTests, ObjectStreamCacheTests | Yes | Yes | Last 8 decoded streams within 4 MiB kept; others decoded again on demand |
| Incremental updates, `/Prev` chain | 7.5.6 | Done | `Structure/XrefReader.cs` | IncrementalWriterTests, XrefBoundsTests | Yes | Yes | |
| Hybrid-reference files, `/XRefStm` | 7.5.8.4 | Done | `Structure/XrefReader.cs` | ParserTests, XrefBoundsTests | Yes | Yes | |
| Repair of damaged files | none (practice) | Done | `Structure/XrefRepair.cs` | XrefRepairTests, StructureFixTests, EncryptedRepairTests | Yes | Yes | |
| Repair report (`WasRepaired`, `GetRepairs`) | none (practice) | Done | `Objects/PdfOpenContext.cs`, `Document/PdfDocumentCheck.cs`, `Document/PdfDiagnosticCode.cs` | RepairReportTests | Yes | Yes | Rebuilt xref, trailer, catalog and page tree, wrong or missing stream length or `endstream`, bad page boxes, truncated Flate or LZW data, malformed `/W`, encodings and font descriptors. Truncated zlib and deflate data is reported on both runtimes: .NET 11 from the decoder status, .NET 10 because the inflater asked for input past the end (`Compat/EndTrackingStream.cs`). A stream that only lacks its Adler-32 checksum is not damaged on either. Truncated JPEGs are reported while decoding. Reports are kept once each, capped at 1,024 |
| Truncated DCT data | none (practice) | Done | `Graphics/Images/JpegImageDecoder.cs`, `Graphics/Images/JpegMarkers.cs`, `Document/PdfDocumentChecker.cs` | ImageAndResourceRepairTests, CheckAndSaveRepairTests | Yes | Yes | The decoder keeps the rows it decoded. Image decoding reports `TruncatedStream` once per image stream when the data does not end with the end-of-image marker (one tail check, no allocation), for both the SkiaSharp and managed paths, so the repair log and viewer notice include it. A scan damaged inside complete data is not reported |
| Glyph names `gNN`, `gidNN`, `glyphNN`, `indexNN`, `cidNNN` | none (practice) | Done | `Fonts/Programs/NumberedGlyphName.cs`, `Fonts/Programs/FontProgram.cs` | RepairReportTests | Yes | Yes | Fallback after the name lookup fails; `cidNNN` goes through the CFF charset. Not in PDFium |
| Malformed `/W`, `/W2`: skip and go on | none (practice) | Done | `Fonts/CidMetricReader.cs`, `Fonts/CidMetricsTable.cs` | RepairReportTests | Yes | Yes | Differs from PDFium, which stops reading at the first bad item |
| Whole-document check | none (practice) | Done | `Document/PdfDocumentChecker.cs`, `Document/PageTreeChecker.cs`, `Document/ContentChecker.cs` | CheckAndSaveRepairTests | Yes | Yes | Decodes every stream, parses every page's content, checks the page tree. `PdfCheckOptions.Recovery` and `IgnoreXrefStreams` apply to the static check that opens the file. Async forms yield between batches |
| Conforming save (types, parent, count, boxes, unique keys, rebuilt page tree) | 7.7 | Done | `Writing/PdfSaveRepairs.cs`, `Writing/PdfObjectWriter.cs`, `Writing/PdfCompactWriter.cs` | CheckAndSaveRepairTests, RepairReportTests, ImageAndResourceRepairTests | Yes | Yes | A page with no `/Resources`, own or inherited, gets an empty dictionary (ISO 32000 requires the entry). Compact save also writes damaged byte-filtered streams again from the part that decoded. Incremental save fixes only the objects it writes and never adds a page tree node |
| Linearized files read as normal files | Annex F | Done | `Structure/XrefReader.cs` | LinearizedTests | Yes | Yes | |
| Linearization parameter dictionary, hint tables, first-page fast open | Annex F | Missing | none | none found | No | Yes (`core/fpdfapi/parser/cpdf_linearized_header.cpp`, `cpdf_hint_tables.cpp`, `cpdf_data_avail.cpp`) | [#54](https://github.com/glennawatson/PdfViewerLite/issues/54) |

## 2. Objects, syntax and filters

Clauses 7.2 to 7.4.

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Lexical rules, whitespace, comments | 7.2 | Done | `Syntax/PdfLexer.cs`, `Syntax/PdfCharacters.cs` | ParserTests | Yes | Yes | |
| Booleans, numbers (including odd forms), null | 7.3.2 to 7.3.9 | Done | `Objects/PdfNumber.cs`, `Syntax/PdfParser.cs` | ParserTests | Yes | Yes | |
| Literal and hex strings | 7.3.4 | Done | `Syntax/PdfStringDecoder.cs` | ParserTests | Yes | Yes | |
| Names, `#xx` escapes | 7.3.5 | Done | `Objects/PdfName.cs`, `Objects/PdfNameTable.cs` | ParserTests | Yes | Yes | |
| Arrays, dictionaries, streams, references | 7.3.6 to 7.3.10 | Done | `Objects/PdfArray.cs`, `Objects/PdfDictionary.cs`, `Objects/PdfStream.cs` | ParserTests | Yes | Yes | |
| Text strings: PDFDocEncoding, UTF-16BE (and LE), PDF 2.0 UTF-8 | 7.9.2 | Done | `Objects/PdfText.cs` | DocumentTests, DocumentParityTests | Yes | Yes (no PDF 2.0 UTF-8 mark) | |
| Dates | 7.9.4 | Done | `Objects/PdfDate.cs` | DocumentTests | Yes | Yes | |
| Rectangles | 7.9.5 | Done | `Objects/PdfRectangle.cs` | PageTreeTests | Yes | Yes | |
| ASCIIHexDecode | 7.4.2 | Done | `Filters/AsciiHexFilter.cs` | FilterTests | Yes | Yes | |
| ASCII85Decode | 7.4.3 | Done | `Filters/Ascii85Filter.cs` | FilterTests, FilterFixTests | Yes | Yes | |
| LZWDecode, `/EarlyChange` | 7.4.4 | Done | `Filters/LzwFilter.cs` | FilterTests | Yes | Yes | |
| FlateDecode | 7.4.4 | Done | `Filters/FlateFilter.cs`, `Compat/ZLibCodec.cs` | FilterTests, FilterFixTests, DecodeLimitTests | Yes | Yes | |
| Predictors (TIFF 2, PNG 10 to 15) | 7.4.4.4 | Done | `Filters/PredictorFilter.cs` | FilterTests, FilterFixTests | Yes | Yes | |
| RunLengthDecode | 7.4.5 | Done | `Filters/RunLengthFilter.cs` | FilterTests | Yes | Yes | |
| BrotliDecode (proposed extension, not in ISO 32000-2) | n/a | Done for reading, with predictors and partial output on damage. Never written: saving rewrites Brotli streams as Flate, keeping any image codec after it | `Filters/BrotliFilter.cs`, `Writing/BrotliRewriter.cs` | BrotliTests | Yes | No | |
| CCITTFaxDecode (G3 1D/2D, G4) | 7.4.6 | Done | `Graphics/Images/CcittFaxDecoder.cs`, `Graphics/Images/CcittParameters.cs` | CcittFaxDecoderTests | Yes | Yes (`core/fxcodec/fax`) | |
| JBIG2Decode, `/JBIG2Globals` | 7.4.7 | Done (managed image decoding; supported regions, dictionaries, and globals) | `Graphics/Images/Jbig2/`, `Graphics/Images/PdfImageDecoder.cs`, `Filters/PdfStreamDecoder.cs` | Jbig2ImageDecoderTests, Jbig2DecoderTests, Jbig2RobustnessTests, DecoderCancellationTests | Yes | Yes (`core/fxcodec/jbig2`) | [#56](https://github.com/glennawatson/PdfViewerLite/issues/56) |
| DCTDecode (baseline, progressive, Adobe transform) | 7.4.8 | Done | `Graphics/Images/Jpeg/`, `Graphics/Images/JpegImageDecoder.cs` | JpegDecoderTests, JpegIdctTests, JpegImageDecoderTests | Yes | Yes (`core/fxcodec/jpeg`) | |
| JPXDecode | 7.4.9 | Partial (regular and high-throughput code-blocks decode; MIXED high-throughput mode is refused, matching PDFium) | `Graphics/Images/Jpx/`, `Graphics/Images/PdfImageDecoder.cs`, `Filters/PdfStreamDecoder.cs` | JpxImageDecoderTests, JpxDecoderTests, JpxHighThroughputTests | Yes | Yes (`core/fxcodec/jpx`) | [#57](https://github.com/glennawatson/PdfViewerLite/issues/57) |
| Crypt filter | 7.4.10 | Done | `Filters/PdfStreamDecoder.cs`, `Security/PdfSecurityHandler.cs` | StreamCryptTests | Yes | Yes (`core/fpdfapi/parser/fpdf_parser_decode.cpp`) | |
| Filter chains, abbreviations, `/DecodeParms` arrays, decode size cap | 7.4.1 | Done | `Filters/PdfStreamDecoder.cs`, `PdfLimits.cs` | FilterTests, DecodeLimitTests | Yes | Yes | |
| External streams `/F`, `/FFilter` | 7.3.8.4 | Missing | none | none found | No | No | |
| Stream `/Length` wrong or indirect | 7.3.8.2 | Done | `Syntax/PdfParser.cs` | StructureFixTests | Yes | Yes | |

## 3. Encryption

Clause 7.6.

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Standard handler R2, R3 (RC4 40 and 128 bit) | 7.6.3 | Done | `Security/PdfSecurityHandler.cs`, `Security/Rc4.cs`, `Security/Md5.cs` | CipherTests, SecurityFixTests | Yes | Yes | |
| Standard handler R4 (AESV2) | 7.6.3, 7.6.5 | Done | `Security/PdfSecurityHandler.cs` | SecurityFixTests, StreamCryptTests | Yes | Yes | |
| Standard handler R5 and R6 (AESV3, 256 bit), password normalisation | 7.6.4 (2.0) | Done | `Security/PdfSecurityHandler.Authentication.cs` | PasswordAndPaddingTests, SecurityFixTests | Yes | Yes | |
| Crypt filters, `/StmF`, `/StrF`, `/EFF`, Identity | 7.6.6 | Done | `Security/PdfSecurityHandler.cs`, `Security/CryptMethods.cs` | StreamCryptTests, EncryptedWriterTests | Yes | Yes | |
| Permissions, `/EncryptMetadata` | 7.6.3.2 | Done | `Security/PdfSecurityHandler.cs` | SecurityFixTests, StreamCryptTests | Yes (read-only flags) | Yes | |
| Public-key handler (`Adobe.PubSec`, `/Recipients`, s3/s4/s5) | 7.6.5 | Done (RSA key transport only; ECDH recipients not handled) | `Security/PdfSecurityHandler.PublicKey.cs`, `Document/PdfDocumentReader.cs` | PublicKeyTests | No | No (not found in source) | [#55](https://github.com/glennawatson/PdfViewerLite/issues/55) |
| Unencrypted wrapper documents (`EncryptedPayload`, PDF 2.0) | 7.6.7 | Done | `Document/PdfDocumentWrapper.cs`, `Document/PdfEncryptedPayload.cs` | WrapperDocumentTests | No | No | [#55](https://github.com/glennawatson/PdfViewerLite/issues/55) |
| Encrypted object streams, damaged encrypted files | 7.6.2 | Done | `Structure/XrefRepair.cs`, `Security/PdfSecurityHandler.cs` | EncryptedRepairTests | Yes | Yes | |

## 4. Document structure and interactive features

Clauses 7.7 to 7.12, 12 and 14.

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Catalog, trailer, version override | 7.7.2 | Done | `Document/PdfDocument.cs`, `Document/PdfDocumentMetadata.cs` | DocumentTests | Yes | Yes | |
| Page tree, inherited attributes, page boxes, rotation | 7.7.3 | Done | `Document/PageTreeReader.cs`, `Document/PdfDocumentPages.cs`, `Document/InheritedAttributes.cs`, `Document/PdfPage.cs` | PageTreeTests, DocumentFixTests | Yes | Yes | |
| Page `/UserUnit` | 7.7.3.3 | Partial (optimiser uses it for image resolution; page geometry and rendering do not) | `Optimizing/ContentUsageScanner.cs` | none found | No | No (not found in source) | [#68](https://github.com/glennawatson/PdfViewerLite/issues/68) |
| Content stream arrays (`/Contents`) | 7.7.3.3 | Done | `Content/ContentReader.cs`, `Document/PdfPage.cs` | PageRenderTests | Yes | Yes | |
| Name trees, number trees | 7.9.6, 7.9.7 | Done | `Document/NameTree.cs` | NavigationTests | Yes | Yes | |
| Page labels | 12.4.2 | Done | `Document/PdfDocumentLabels.cs` | DocumentTests, DocumentParityTests | Yes | Yes | |
| Outlines (bookmarks) | 12.3.3 | Done | `Navigation/PdfOutlineItem.cs`, `Document/PdfDocumentNavigation.cs` | NavigationTests, DocumentParityTests | Yes | Yes | |
| Destinations (explicit and named) | 12.3.2 | Done | `Navigation/PdfDestination.cs`, `Document/NameTree.cs` | NavigationTests | Yes | Yes | |
| Actions: GoTo, GoToR, GoToE, URI, Launch, Named, JavaScript (read) | 12.6.4 | Done | `Navigation/*Action.cs` | NavigationTests | Yes | Yes | |
| Actions run without scripts: ResetForm, Hide, SetOCGState (library); Named, GoTo, URI, Launch, SubmitForm, ImportData (host callback; the library and app never send or import data) | 12.6.4, 12.7.6 | Done | `Navigation/PdfActionRunner.cs`, `Navigation/IPdfActionHost.cs`, `Forms/PdfForm.Actions.cs` | FormRuntimeTests, FormRuntimeUiTests | Yes (buttons, page open) | Recognised; few executed | [#93](https://github.com/glennawatson/PdfViewerLite/issues/93) |
| Actions: Thread, Sound, Movie, Rendition, Trans, GoTo3DView, RichMediaExecute, JavaScript | 12.6.4 | Read only (skipped when run) | `Navigation/*Action.cs` | NavigationTests | No | Recognised (`core/fpdfdoc/cpdf_action.cpp`); few executed | [#70](https://github.com/glennawatson/PdfViewerLite/issues/70) |
| Action chains (`/Next`), additional actions (`/AA`) | 12.6.3, 12.6.3 | Partial | `Navigation/PdfAction.cs`, `Forms/PdfWidgetScripts.cs` | NavigationTests, FormScriptReadingTests | Form scripts only | Yes | [#70](https://github.com/glennawatson/PdfViewerLite/issues/70) |
| Links (link annotations) | 12.5.6.5 | Done | `Document/PdfDocumentLinks.cs`, `Navigation/PdfLink.cs` | NavigationTests, DocumentParityTests, DocumentFixTests | Yes | Yes | |
| File specifications | 7.11 | Done | `Document/PdfDocumentFileSpecs.cs` | NavigationTests | Yes | Yes | |
| Embedded files | 7.11.4 | Done | `Document/PdfAttachment.cs`, `Document/PdfDocumentAttachments.cs` | AttachmentSignatureParityTests | Yes | Yes (`fpdfsdk/fpdf_attachment.cpp`) | |
| Portfolios and collections (`/Collection`) | 7.11.6 | Done (library reads schema, sorting, folders and items; no app portfolio UI) | `Document/PdfDocumentPortfolio.cs`, `Portfolio/` | PortfolioTests | No | No public portfolio API | [#72](https://github.com/glennawatson/PdfViewerLite/issues/72) |
| Associated files (`/AF`, PDF 2.0) | 14.13 | Done (library reads catalog, page, annotation, XObject and structure owners; page copy carries referenced files) | `Document/PdfDocumentAssociatedFiles.cs`, `Editing/PdfCarryFiles.cs` | PortfolioTests, CarryStructuresTests | No | No public associated-file API | [#72](https://github.com/glennawatson/PdfViewerLite/issues/72) |
| Page copy (InsertPages, ExtractPages) keeps form fields (`/Parent` chains, `/AcroForm` `/Fields`, `/DR`, `/DA`, `/Q`, clash renaming), outline entries, named destinations (renamed on clash), page labels, optional content groups and state, embedded file names; retargets or drops `/Dest`, `/A` GoTo and `/P` | 12.7, 12.3, 12.4.2, 8.11, 7.11.4 | Done (XFA, `/SigFlags`, `/CO`, radio-button groups of layers and structure-element links of outlines are not carried) | `Editing/PdfDocumentCarrier.cs`, `PdfCarry*.cs`, `PdfFormDefaults.cs`, `PdfNameTreeMerge.cs`, `Writing/PdfPageImporter.cs` | CarryStructuresTests, CarriedStructuresPdfiumTests | No | PDFium reopens the output | [#86](https://github.com/glennawatson/PdfViewerLite/issues/86) |
| Metadata streams (XMP) | 14.3 | Partial (catalog, page and arbitrary owner packets read; app metadata still uses `/Info`) | `Document/PdfDocumentMetadata.cs`, `Metadata/XmpParser.cs`, `Security/PdfSecurityHandler.cs` | XmpTests, StreamCryptTests | No | No (not parsed) | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| Document information dictionary | 14.3.3 | Done | `Document/PdfDocumentMetadata.cs`, `Document/PdfDocumentInfo.cs` | DocumentTests, DocumentParityTests | Yes | Yes | |
| Optional content groups, OCMD, `/VE`, `/P` policy | 8.11 | Done | `Layers/`, `Document/PdfDocumentLayers.cs` | OptionalContentTests, PageRenderTests | Yes | Yes (`core/fpdfapi/page/cpdf_occontext.cpp`) | |
| Optional content usage, `/AS` auto states, `/Intent`, `/Order`, radio groups | 8.11.4 | Partial | `Layers/OptionalContentReader.cs` | OptionalContentTests | Layer list only | Yes | [#78](https://github.com/glennawatson/PdfViewerLite/issues/78) |
| AcroForm: field tree, values, flags, appearances | 12.7 | Done | `Forms/PdfForm*.cs`, `Forms/FormAppearance*.cs` | FormTests, FormParityTests, CombFormTests, FormAppearanceTests | Yes | Yes (`fpdfsdk/cpdfsdk_interactiveform.cpp`) | |
| AcroForm appearances in the field's own font: non-WinAnsi encodings and `/Differences`, Type0 fonts with Identity-H (via `/ToUnicode`) or Unicode CMaps, bevel and inset borders | 12.7.3 | Done. Not done: push-button captions and icons (`/MK /I`, `/TP`, `/IF`), rich text `/RV` `/DS` drawing, legacy multi-byte CMaps (fall back to Helvetica), round bevels on radio buttons | `Forms/FormFont.cs`, `Forms/FormCodeMap.cs`, `Forms/FormCodes.cs`, `Forms/FormBorder.cs`, `Forms/FormAppearance.Border.cs` | FormFontAppearanceTests, FormAppearanceTests, FormParityTests | Yes | Yes | [#69](https://github.com/glennawatson/PdfViewerLite/issues/69) |
| Form field tint (highlight) over fillable fields, colour and opacity from settings | 12.7 | Done (default 0xB4CCDC, alpha 72, read as 0xRRGGBB in both engines) | `Rendering/AnnotationTintAppearance.cs`, `Rendering/PdfFormHighlight.cs` | FormHighlightParityTests, FormRuntimeUiTests | Yes | Yes (`FPDF_SetFormFieldHighlightColor`) | [#93](https://github.com/glennawatson/PdfViewerLite/issues/93) |
| Calculation order (`/AcroForm /CO`) and widget tab order (`/Tabs` R, C, S) | 12.7.3, 12.5.2 | Done | `Forms/PdfForm.Order.cs` | FormRuntimeTests, FormRuntimeUiTests | Yes | No (annotation order) | [#93](https://github.com/glennawatson/PdfViewerLite/issues/93) |
| Form field scripts: keystroke, format, validate, calculate; built-ins AFNumber, AFPercent, AFDate(Ex), AFTime(Ex), AFSpecial, AFRange_Validate, AFSimple_Calculate, plus AFParseDateEx and AFMergeChange as engine helpers | 12.6.3, 12.7.5 | Done for the built-ins (no general JavaScript) | `Core/Forms/Scripting/*` | FormScriptTests | Yes | Yes (with V8 build) | [#93](https://github.com/glennawatson/PdfViewerLite/issues/93) |
| XFA forms: run and render | 12.7.8 | Compat (PDFium) | `Document/PdfDocumentContent.cs` (detect only) | ContentCheckTests | Detect only | Only in XFA builds | |
| XFA packets read as data | 12.7.8 | Done (single stream and packet array; no XFA rendering) | `Document/PdfDocumentXfa.cs`, `Xfa/` | CatalogStructureTests | No | Yes (`FPDF_GetXFAPacket*`) | [#79](https://github.com/glennawatson/PdfViewerLite/issues/79) |
| Signature fields: list, byte range, contents, reason, time | 12.8 | Done | `Document/PdfSignatureField.cs` | AttachmentSignatureParityTests | Yes | Yes (`fpdfsdk/fpdf_signature.cpp`) | |
| Signature validation, DSS/VRI, DocMDP, FieldMDP, `/Perms`, UR3, document timestamps, modification detection by revision | 12.8.2, 12.8.4 | Done (offline revocation by default; `adbe.x509.rsa_sha1` reported unsupported) | `Signatures/`, `Document/PdfDocumentSignatureValidation.cs`, `Objects/PdfObjectStore.Revisions.cs` | SignatureValidationTests, ModificationDetectionTests, ByteRangeTests, SecurityStoreTests, SignatureFormatTests, SignatureValidationParityTests | App validates in Core (`SignatureVerifier`) | No (no `DSS`, validation not offered) | [#71](https://github.com/glennawatson/PdfViewerLite/issues/71) |
| Annotation: Text, Link, FreeText, Line, Square, Circle, Polygon, PolyLine, Highlight, Underline, Squiggly, StrikeOut, Stamp, Caret, Ink, Popup, FileAttachment (read, edit) | 12.5.6 | Done | `Annotations/`, `src/PdfViewerLite.HyperPdf/HyperPdfAnnotations*.cs` | AnnotationDictionaryTests, PageAnnotationTests, AnnotationParityTests, NativeAnnotationTests | Yes | Yes | |
| Annotation: Widget | 12.5.6.19 | Done | `Forms/`, `Rendering/PageRecorder.cs` | FormTests, PageRenderTests | Yes | Yes | |
| Annotation: Redact (mark with a preview, apply with true content removal, overlay from `/IC`, `/OverlayText`, `/Repeat`, `/DA`, `/Q`, `/RO`) | 12.5.6.23 | Done | `Redaction/`, `src/PdfViewerLite.HyperPdf/HyperPdfAnnotations.Redaction.cs` | RedactionTextTests, RedactionContentTests, RedactionApplyTests, RedactionCorpusTests, PageObjectPdfiumTests, RedactionViewModelTests | Yes (HyperPDF engine) | Subtype only | [#85](https://github.com/glennawatson/PdfViewerLite/issues/85) |
| Annotation: Sound, Movie, Screen, PrinterMark, TrapNet, Watermark, 3D, RichMedia (read as data, never played) | 12.5.6 | Partial (media, 3D and RichMedia data read; PrinterMark, TrapNet and Watermark only keep generic dictionaries) | `Document/PdfDocumentMedia.cs`, `Document/PdfDocumentMedia3D.cs`, `Media/`, `Annotations/PdfPageAnnotations.cs` | MultimediaTests, PageAnnotationTests | Detect only | Recognised, not played | [#79](https://github.com/glennawatson/PdfViewerLite/issues/79) |
| Annotations without `/AP` | 12.5.5 | Partial (in-memory appearances generated for supported subtypes; other subtypes may remain blank) | `Rendering/PageRecorder.cs`, `Rendering/AnnotationAppearance*.cs` | AnnotationRenderTests | Yes (supported subtypes) | Yes, for some subtypes (`cpdf_generateap`) | [#64](https://github.com/glennawatson/PdfViewerLite/issues/64) |
| Annotation flags (Hidden, Print, NoView) | 12.5.3 | Done | `Rendering/PageRecorder.cs`, `Annotations/PdfAnnotationFlags.cs` | PageRenderTests | Yes | Yes | |
| Multimedia (sound, movie, renditions) read as data; playback out of scope | 13.2 | Done (library data reader; no playback) | `Document/PdfDocumentMedia.cs`, `Document/PdfDocumentActionTypes.cs`, `Media/` | MultimediaTests, ActionDataTests | Detect only | Recognised, not played | [#79](https://github.com/glennawatson/PdfViewerLite/issues/79) |
| 3D artwork read as data; 3D rendering out of scope | 13.6 | Done (library stream, view and activation data; no 3D rendering) | `Document/PdfDocumentMedia3D.cs`, `Media/Pdf3D*.cs` | MultimediaTests, ActionDataTests | Detect only | Recognised, not rendered | [#79](https://github.com/glennawatson/PdfViewerLite/issues/79) |
| RichMedia read as data; playback out of scope | ISO 32000-2 13.7 | Done (library assets, configurations and instances; no playback) | `Document/PdfDocumentMedia3D.cs`, `Media/PdfRichMedia*.cs` | MultimediaTests, ActionDataTests | Detect only | Recognised, not played | [#79](https://github.com/glennawatson/PdfViewerLite/issues/79) |
| Document-level JavaScript: read as text | 12.6.4.16 | Done (name-tree, open-action and additional-action text; no execution) | `Document/PdfDocumentActions.cs`, `Navigation/JavaScriptAction.cs` | ActionDataTests, ContentCheckTests | Detect only | Yes (`FPDFDoc_GetJavaScriptAction*`) | [#79](https://github.com/glennawatson/PdfViewerLite/issues/79) |
| Document-level JavaScript: run | 12.6.4.16 | Compat (PDFium) | none | none | No | Yes (V8 build) | |
| Articles and threads | 12.4.3 | Done (library reads thread and bead data; app has no article navigation) | `Document/PdfDocumentCatalog.cs`, `Features/PdfThread.cs` | CatalogStructureTests, ActionDataTests | No | Action type only | [#74](https://github.com/glennawatson/PdfViewerLite/issues/74) |
| Page transitions, presentation | 12.4.4 | Partial (transition and advance data read; no presentation mode) | `Document/PdfDocumentViewerPreferences.cs`, `Features/PdfPageTransition.cs` | CatalogStructureTests, ActionDataTests | No | Action type only | [#74](https://github.com/glennawatson/PdfViewerLite/issues/74) |
| Viewer preferences | 12.2 | Done (library reads preferences; app does not apply them) | `Document/PdfDocumentViewerPreferences.cs` | CatalogStructureTests | No | No public preference API | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| Output intents | 14.11.5 | Done (read and used for eligible PDF/A rendering) | `Document/PdfDocumentCatalog.cs`, `Graphics/Colors/OutputIntentColors.cs` | CatalogStructureTests, OutputIntentRenderTests | Yes (PDF/A rendering) | No public output-intent API | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| Extensions (`/Extensions`, developer extensions) | 7.12 | Done (library reads declared developer extensions) | `Document/PdfDocumentExtensionDeclarations.cs`, `Extensions/` | ExtensionTests | No | No public extension API | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| Measurement and geospatial (`/VP`, `/Measure`) | 12.9, 12.10 | Done (library reads viewport and measure data; no app measurement tool) | `Document/PdfDocumentMeasure.cs`, `Features/PdfViewport.cs`, `Features/PdfMeasure.cs` | CatalogStructureTests | No | No public measure API | [#75](https://github.com/glennawatson/PdfViewerLite/issues/75) |
| Document parts (DPart, PDF 2.0) | 14.12 | Done (library reads hierarchy and page membership) | `Document/PdfDocumentWebCapture.cs`, `Features/PdfDocumentPart*.cs` | CatalogStructureTests | No | No public DPart API | [#74](https://github.com/glennawatson/PdfViewerLite/issues/74) |
| Piece info, web capture | 14.5, 14.10 | Done (library reads both as data) | `Document/PdfDocumentCatalog.cs`, `Document/PdfDocumentWebCapture.cs` | CatalogStructureTests | No | No public API | [#74](https://github.com/glennawatson/PdfViewerLite/issues/74) |
| Logical structure tree, tagged PDF, role map, attributes | 14.7, 14.8 | Partial (managed reader and conditional app exposure; PDF/UA gaps remain) | `Structure/Tagged/`, `src/PdfViewerLite.HyperPdf/HyperPdfDocument.Tagged.cs` | StructureTreeTests, MarkedContentTests, TaggedParityTests | Yes (HyperPdf, when tag coverage passes) | Yes (`core/fpdfapi/page/cpdf_structtree`) | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| `/MarkInfo` (tagged flag) | 14.7.1 | Done (reading only) | `Structure/Tagged/PdfStructureTree.cs` | AccessibilityDocumentTests, StructureTreeTests | Yes (HyperPdf) | Yes | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| Marked content (BMC, BDC, EMC, MP, DP) and `/OC` | 14.6 | Done | `Content/ContentInterpreter.Marked.cs` | PageRenderTests, OptionalContentTests | OC only | Yes | |
| Marked content ids (MCID) joined to structure | 14.7.4 | Partial (captured and joined for supported page content; not every structure case is exposed) | `Structure/Tagged/MarkedContentRecorder.cs`, `Structure/Tagged/StructureTreeBuilder.cs` | MarkedContentTests, StructureTreeTests, TaggedParityTests | Yes (HyperPdf, conditionally) | Yes | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| Artifacts | 14.8.2.2 | Partial (identified for accessibility scans, not filtered from ordinary text extraction) | `Structure/Tagged/MarkedContentRecorder.cs`, `Accessibility/AccessibilityPageScan.cs` | MarkedContentTests, AccessibilityDocumentTests | Yes (accessibility reading only) | Yes | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |

## 5. Graphics and rendering

Clauses 8 to 11. For known renderer behaviour gaps, see section 8.

### Content stream operators (Annex A)

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Graphics state: `q Q cm w J j M d ri i gs` | 8.4 | Partial | `Content/ContentInterpreter.State.cs`, `ContentInterpreter.ExtGState.cs` | ShapeRenderTests, TransparencyRenderTests | Yes | Yes | [#66](https://github.com/glennawatson/PdfViewerLite/issues/66) |
| Path construction: `m l c v y h re` | 8.5.2 | Done | `Content/ContentInterpreter.Path.cs` | ShapeRenderTests | Yes | Yes | |
| Path painting: `S s f F f* B B* b b* n` | 8.5.3 | Done | `Content/ContentInterpreter.Path.cs` | ShapeRenderTests | Yes | Yes | |
| Clipping: `W W*` | 8.5.4 | Done | `Content/ContentInterpreter.Path.cs` | ShapeRenderTests | Yes | Yes | |
| Colour: `CS cs SC SCN sc scn G g RG rg K k` | 8.6 | Done | `Content/ContentInterpreter.Color.cs` | ColorSpaceTests, ShapeRenderTests | Yes | Yes | |
| Shading: `sh` | 8.7.4.2 | Done | `Content/ContentInterpreter.XObject.cs`, `Graphics/Shadings/` | ShadingRenderTests | Yes | Yes | [#67](https://github.com/glennawatson/PdfViewerLite/issues/67) |
| XObjects: `Do` | 8.8 | Done | `Content/ContentInterpreter.XObject.cs` | ShapeRenderTests, ImageRenderTests | Yes | Yes | |
| Inline images: `BI ID EI` | 8.9.7 | Done | `Content/ContentReader.cs`, `Content/InlineImageSize.cs`, `Graphics/Images/PdfImageDecoder.cs` | ImageRenderTests, StructureFixTests | Yes | Yes | |
| Text objects and state: `BT ET Tc Tw Tz TL Tf Tr Ts` | 9.3, 9.4 | Done (supported fonts; font gaps are tracked in section 6) | `Content/ContentInterpreter.Text.cs`, `Fonts/PdfFontLoader.cs` | TextRenderTests, FontRenderTests, Type3RenderTests | Yes | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58) |
| Text positioning: `Td TD Tm T*` | 9.4.2 | Done | `Content/ContentInterpreter.Text.cs` | TextRenderTests | Yes | Yes | |
| Text showing: `Tj TJ ' "` | 9.4.3 | Done (supported font mappings) | `Content/ContentInterpreter.Text.cs`, `Fonts/PdfFontLoader.cs` | TextRenderTests, FontRenderTests | Yes | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58) |
| Type 3 glyph operators: `d0 d1` | 9.6.5 | Done (supported glyph procedures) | `Fonts/PdfType3Font.cs`, `Fonts/Type3Font.cs` | Type3FontTests, Type3RenderTests | Yes | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58) |
| Marked content: `MP DP BMC BDC EMC` | 14.6 | Done | `Content/ContentInterpreter.Marked.cs` | PageRenderTests | Yes | Yes | |
| Compatibility: `BX EX` | 7.8.2 | Done | `Content/OperatorTable.cs` | ParserTests | Yes | Yes | |
| Unknown operators ignored | 7.8.2 | Done | `Content/OperatorTable.cs` | StructureFixTests | Yes | Yes | |

### Graphics state (ExtGState keys, Table 57)

| Key | Status | HyperPdf | Tests | PDFium | Issue |
| --- | --- | --- | --- | --- | --- |
| `LW LC LJ ML D` | Done | `Content/ContentInterpreter.ExtGState.cs` | ShapeRenderTests | Yes | |
| `Font` | Done | `Content/ContentInterpreter.ExtGState.cs` | TextRenderTests | Yes | |
| `CA ca AIS` (alpha) | Done (`AIS` not checked) | `Content/ContentInterpreter.ExtGState.cs` | TransparencyRenderTests | Yes | |
| `BM` (all 16 blend modes) | Done | `Content/ContentInterpreter.ExtGState.cs`, `Graphics/PdfBlendMode.cs` | TransparencyRenderTests | Yes | |
| `SMask` (alpha and luminosity, `/BC`, `/TR`) | Done | `Content/ContentInterpreter.ExtGState.cs`, `Graphics/PdfSoftMask.cs` | TransparencyRenderTests | Yes | |
| `OP op OPM` (overprint) | Missing (ignored) | none | none found | Partial | [#66](https://github.com/glennawatson/PdfViewerLite/issues/66) |
| `RI` (rendering intent) | Missing (ignored) | none | none found | Ignored | [#66](https://github.com/glennawatson/PdfViewerLite/issues/66) |
| `FL` (flatness), `SM` (smoothness), `SA` (stroke adjustment) | Missing (ignored) | none | none found | Partial | [#66](https://github.com/glennawatson/PdfViewerLite/issues/66) |
| `TR TR2` (transfer), `HT` (halftone), `BG BG2`, `UCR UCR2` | Missing (ignored) | none | none found | `TR2` only (`core/fpdfapi/page/cpdf_allstates.cpp`) | [#66](https://github.com/glennawatson/PdfViewerLite/issues/66) |
| `TK` (text knockout) | Missing | none | none found | No | [#65](https://github.com/glennawatson/PdfViewerLite/issues/65) |

### Colour spaces (8.6)

| Row | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- |
| DeviceGray, DeviceRGB, DeviceCMYK, default colour spaces | Done | `Graphics/Colors/Device*.cs`, `Graphics/Colors/CmykTable.cs` | ColorSpaceTests, ColorFixTests | Yes | Yes | |
| CalGray, CalRGB | Done | `Graphics/Colors/CalGrayColorSpace.cs`, `CalRgbColorSpace.cs` | ColorFixTests | Yes | Yes | |
| Lab | Done | `Graphics/Colors/LabColorSpace.cs` | ColorSpaceTests | Yes | Yes | |
| ICCBased: matrix/TRC and gray profiles | Done | `Graphics/Colors/IccBasedColorSpace.cs`, `IccMatrixTransform.cs`, `IccGrayTransform.cs` | IccProfileTests | Yes | Yes (`core/fxcodec/icc`) | |
| ICCBased: lookup-table profiles (A2B0, mAB), CMYK profiles | Partial (falls back to `/Alternate`) | `Graphics/Colors/IccBasedColorSpace.cs` | IccProfileTests | Yes | Yes | [#76](https://github.com/glennawatson/PdfViewerLite/issues/76) |
| Indexed | Done | `Graphics/Colors/IndexedColorSpace.cs` | ColorSpaceTests | Yes | Yes | |
| Separation, DeviceN (including `/All`, `/None`, NChannel) | Done (NChannel attributes not used) | `Graphics/Colors/SeparationColorSpace.cs`, `DeviceNColorSpace.cs`, `TintTransform.cs` | ColorSpaceTests | Yes | Yes | |
| Pattern colour space | Done | `Graphics/Colors/PatternColorSpace.cs`, `Graphics/PdfPatternPaint.cs` | ShadingRenderTests | Yes | Yes | |
| Functions: sampled, exponential, stitching, PostScript calculator | Done | `Graphics/Functions/` | FunctionTests, FunctionFixTests, HotPathAllocationTests | Yes | Yes (`cpdf_psengine.cpp`) | |

### Patterns and shadings (8.7)

| Row | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- |
| Tiling patterns, coloured and uncoloured | Partial | `Rendering/PatternCell.cs`, `Rendering/PatternKey.cs`, `Content/CellRequest.cs` | ShadingRenderTests | Yes | Yes | [#67](https://github.com/glennawatson/PdfViewerLite/issues/67) |
| Tiling with step smaller than bounding box (overlap) | Missing (overlap lost) | `Rendering/PatternCell.cs` | none found | Yes | Yes | [#67](https://github.com/glennawatson/PdfViewerLite/issues/67) |
| Shading type 1 (function) | Done | `Graphics/Shadings/ShadingShaders.cs` | ShadingRenderTests | Yes | Yes | |
| Shading types 2 and 3 (axial, radial), `/Extend` | Partial (mixed `/Extend` drawn as both) | `Graphics/Shadings/ShadingShaders.cs` | ShadingRenderTests | Yes | Yes | [#67](https://github.com/glennawatson/PdfViewerLite/issues/67) |
| Shading types 4 to 7 (free-form, lattice, Coons, tensor) | Done | `Graphics/Shadings/MeshBuilder*.cs`, `PatchTessellator.cs` | ShadingRenderTests | Yes | Yes (`cpdf_meshstream.cpp`) | |
| Shading as a stroke paint (mesh types) | Missing (paints nothing) | `Rendering/SkiaContentDevice.Painting.cs` | none found | Yes | Yes | [#67](https://github.com/glennawatson/PdfViewerLite/issues/67) |
| Shading `/Background` | Partial (patterns only, not `sh`) | `Graphics/Shadings/PdfShading.cs` | ShadingRenderTests | Yes | Yes | [#67](https://github.com/glennawatson/PdfViewerLite/issues/67) |
| Shading `/BBox`, `/AntiAlias` | Partial (`/AntiAlias` not checked) | `Graphics/Shadings/PdfShading.cs` | ShadingRenderTests | Yes | Yes | |

### Images (8.9)

| Row | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- |
| Image XObjects, 1 to 16 bit samples | Done | `Graphics/Images/PdfImageDecoder.cs`, `SampleUnpacker.cs` | ImageDecoderTests, ImageRenderTests | Yes | Yes | |
| `/Decode` arrays | Done | `Graphics/Images/PdfImageDecoder.cs` | ImageDecoderTests, JpegImageDecoderTests | Yes | Yes | |
| Stencil masks (`/ImageMask`) | Done | `Graphics/Images/ImageAlpha.cs` | ImageRenderTests | Yes | Yes | |
| Explicit masks (`/Mask` stream), colour-key masks | Done | `Graphics/Images/ImageMasks.cs` | ImageFixTests, ImageDecoderTests | Yes | Yes | |
| Soft masks (`/SMask`), `/Matte` | Done | `Graphics/Images/ImageMasks.cs`, `MatteColor.cs` | ImageFixTests | Yes | Yes | |
| `/SMaskInData` (JPX alpha) | Done | `Graphics/Images/Jpx/JpxImageDecoder.cs` | JpxImageDecoderTests | Yes | Yes | [#57](https://github.com/glennawatson/PdfViewerLite/issues/57) |
| `/Interpolate` | Done | `Graphics/Images/PdfImageData.cs`, `Rendering/SkiaContentDevice.Painting.cs` | ImageRenderTests | Yes | Yes | |
| Image `/Alternates` | Missing | none | none found | No | No | [#77](https://github.com/glennawatson/PdfViewerLite/issues/77) |
| Image `/OC`, `/Intent`, `/Metadata`, `/StructParent` | Partial (`/OC` only) | `Content/ContentInterpreter.XObject.cs` | OptionalContentTests | Yes | Partial | |
| Inline images (abbreviations, size scan) | Done | `Content/InlineImageSize.cs` | ImageRenderTests, StructureFixTests | Yes | Yes | |
| Image decoder budget (size cap) | Done | `Graphics/Images/ImageHeader.cs` | ImageFixTests | Yes | Yes | |
| JBIG2 and JPX images | Partial (JBIG2 decoding is managed; JPX refuses mixed high-throughput mode) | `Graphics/Images/Jbig2/`, `Graphics/Images/Jpx/`, `Graphics/Images/PdfImageDecoder.cs` | Jbig2ImageDecoderTests, Jbig2DecoderTests, JpxImageDecoderTests, JpxHighThroughputTests, ImageDecoderTests | Yes | Yes | [#56](https://github.com/glennawatson/PdfViewerLite/issues/56), [#57](https://github.com/glennawatson/PdfViewerLite/issues/57) |

### XObjects and groups (8.8, 8.10, 11.6)

| Row | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- |
| Form XObjects (`/Matrix`, `/BBox`, `/Resources`) | Done | `Content/ContentInterpreter.XObject.cs` | ShapeRenderTests | Yes | Yes | |
| Transparency group XObjects, isolated | Done | `Content/ContentInterpreter.XObject.cs`, `Content/GroupInfo.cs` | TransparencyRenderTests | Yes | Yes | |
| Non-isolated groups | Partial (approximated) | `Rendering/SkiaContentDevice.cs` | TransparencyRenderTests | Yes | Yes | [#65](https://github.com/glennawatson/PdfViewerLite/issues/65) |
| Knockout groups | Partial (approximated) | `Rendering/SkiaContentDevice.cs` | TransparencyRenderTests | Yes | Partial | [#65](https://github.com/glennawatson/PdfViewerLite/issues/65) |
| Page-level `/Group` | Partial | `Rendering/PdfPageRenderer.cs` | PageRenderTests | Yes | Yes | [#65](https://github.com/glennawatson/PdfViewerLite/issues/65) |
| Reference XObjects (`/Ref`) | Missing | none | none found | No | No | [#77](https://github.com/glennawatson/PdfViewerLite/issues/77) |
| PostScript XObjects | Out of scope (deprecated in PDF 2.0) | none | none found | No | No | |

### Transparency and device-dependent output (11, 10)

| Row | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- |
| Constant alpha, blend modes, soft masks | Done | `Rendering/SkiaContentDevice*.cs`, `Rendering/SoftMaskKey.cs` | TransparencyRenderTests | Yes | Yes | |
| Overprint and overprint mode | Missing (ignored) | none | none found | No | Partial | [#66](https://github.com/glennawatson/PdfViewerLite/issues/66) |
| Rendering intents | Missing (ignored) | none | none found | No | Ignored | [#66](https://github.com/glennawatson/PdfViewerLite/issues/66) |
| Halftones, transfer functions, black generation, undercolour removal | Missing (ignored) | none | none found | No | `TR2` only | [#66](https://github.com/glennawatson/PdfViewerLite/issues/66) |
| Trapping, separations preview | Out of scope | none | none found | No | No | |
| Optional content visibility while rendering (`/OC` on XObjects, annotations and marked content) | Done | `Content/ContentInterpreter.Marked.cs`, `Content/ContentInterpreter.XObject.cs` | OptionalContentTests, PageRenderTests | Yes | Yes | |
| Annotation appearance streams (`/AP /N`, states, `/Rect` fit) | Done | `Rendering/PageRecorder.cs`, `Annotations/PdfAppearances.cs` | PageRenderTests, PageAnnotationTests | Yes | Yes | |
| Rendering API: minimum line width, progressive rendering, thumbnails, byte-bounded image and page picture caches, caches emptied on dispose | Partial (minimum line width is missing; the rest is implemented) | `Rendering/PdfPageRenderer*.cs`, `Rendering/PdfRenderOptions.cs` | ProgressiveRenderTests, ThumbnailRenderTests, ImageCacheTests, RenderLifetimeTests | Yes | Yes (`fpdf_progressive`; no full FPDF thumbnail API mapping) | [#68](https://github.com/glennawatson/PdfViewerLite/issues/68) |

## 6. Text and fonts

Clause 9. HyperPdf loads page fonts and uses them in managed rendering and text extraction for supported encodings. Requested predefined CMaps and open font faces are generated on demand from pinned PDF.js bcmap and Google Fonts sources, then cached locally. The repository contains the generator code and license notices, not generated font or CMap binaries.

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Text state parameters and rendering modes 0 to 7 (clip modes) | 9.3 | Partial | `Content/ContentInterpreter.Text.cs`, `Graphics/GraphicsState.cs` | TextRenderTests | Yes | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58) |
| Simple fonts: Type1 (embedded, FontFile) | 9.6.2 | Partial (supported embedded programs load and render; not every Type 1 variant is covered) | `Fonts/SimpleFontLoader.cs`, `Fonts/EmbeddedFontLoader.cs`, `Fonts/Programs/Type1*.cs` | Type1FontTests, FontRenderTests | Yes | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58) |
| Simple fonts: TrueType (FontFile2) | 9.6.3 | Partial (supported embedded programs load and render; not every encoding and font edge case is covered) | `Fonts/SimpleFontLoader.cs`, `Fonts/EmbeddedFontLoader.cs`, `Fonts/Programs/TrueTypeProgram*.cs`, `Fonts/Programs/TrueTypeCmap.cs` | TrueTypeFontTests, FontRenderTests, FontRenderParityTests | Yes | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58) |
| Simple fonts: Type1C and OpenType CFF (FontFile3) | 9.6.2, 9.9 | Partial (supported CFF programs load; OpenType CFF coverage remains limited) | `Fonts/SimpleFontLoader.cs`, `Fonts/EmbeddedFontLoader.cs`, `Fonts/Programs/CffProgram.cs`, `Type2Interpreter*.cs` | CffProgramTests, FontRenderTests | Yes | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58) |
| MMType1 | 9.6.2.3 | Missing | none | none found | Yes (through PDFium engine) | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58) |
| Type 3 fonts | 9.6.5 | Partial (supported glyph procedures render; unsupported content operators remain) | `Fonts/PdfType3Font.cs`, `Content/ContentInterpreter.Text.cs` | Type3FontTests, Type3RenderTests | Yes | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58) |
| Encodings: Standard, WinAnsi, MacRoman, MacExpert, `/Differences`, symbolic fonts | 9.6.6 | Partial (common encodings and glyph names are implemented; some symbolic and legacy cases remain) | `Fonts/SimpleFontLoader.cs`, `Fonts/Data/` | Type1FontTests, TrueTypeFontTests, StandardFontsTests, GlyphListTests | Yes | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58) |
| Composite fonts: Type0 with CIDFontType0 and CIDFontType2 | 9.7 | Partial (supported Identity, predefined and embedded CMaps load; some encodings and scripts remain unsupported) | `Fonts/CompositeFontLoader.cs`, `Fonts/CMaps/`, `Fonts/CidMetricsTable.cs` | CompositeFontTests, CjkFontTests, CMapTests | Yes | Yes | [#59](https://github.com/glennawatson/PdfViewerLite/issues/59) |
| `/CIDToGIDMap`, `/W`, `/DW`, CIDSystemInfo | 9.7.4 | Partial (CID-to-glyph mapping and horizontal metrics are read for supported CID fonts) | `Fonts/CompositeFontLoader.cs`, `Fonts/CidMetricReader.cs`, `Fonts/CidMetricsTable.cs` | CompositeFontTests, CjkFontTests | Yes | Yes | [#59](https://github.com/glennawatson/PdfViewerLite/issues/59) |
| Predefined CMaps (Identity, supported CJK sets, `UniJIS-UTF16`) | 9.7.5.2 | Partial (supported maps are generated on demand and cached; supplementary Unicode is preserved; not every Adobe collection is included) | `Fonts/CMaps/CMapResolver.cs`, `Fonts/PredefinedCMaps.cs`, `Fonts/Generation/FontDataGeneration.cs`, `Fonts/Generation/BinaryCMapReader.cs`, `Fonts/FontDataResources.cs` | BinaryCMapReaderTests, PackedUnicodeTests, PredefinedCMapTests, CMapTableDataTests, CompositeFontTests | Yes | Yes | [#59](https://github.com/glennawatson/PdfViewerLite/issues/59) |
| Embedded CMaps, `/UseCMap` | 9.7.5.3 | Done for parsed embedded maps | `Fonts/CMaps/CMapParser.cs`, `CMapContent.cs` | CMapTests, CompositeFontTests | Yes | Yes | |
| Vertical writing (`/WMode`, `/DW2`, `/W2`) | 9.7.4.3 | Partial (vertical metrics are read; full vertical text layout and shaping remain limited) | `Fonts/CompositeFontLoader.cs`, `Fonts/PdfFont.cs`, `Fonts/CidMetricsTable.cs` | CompositeFontTests | Yes | Yes | [#59](https://github.com/glennawatson/PdfViewerLite/issues/59) |
| ToUnicode CMaps | 9.10.3 | Done for parsed mappings, including supplementary scalars | `Fonts/CMaps/ToUnicodeMap.cs`, `Fonts/CidToUnicodeTable.cs`, `Fonts/Generation/BinaryUnicodeFallback.cs` | CMapTests, PackedUnicodeTests, BinaryCMapReaderTests, CompositeFontTests | Yes | Yes | |
| Standard 14 fonts and metrics | 9.6.2.2 | Done | `Fonts/Data/`, `Fonts/BundledFaces.cs` | StandardFontsTests, BundledFaceTests | Yes | Yes | [#60](https://github.com/glennawatson/PdfViewerLite/issues/60) |
| Open font pack used for standard 14 fonts and common aliases before system fallback | 9.8 | Partial (open faces are downloaded on demand and cached; script coverage depends on the requested family and available system fonts) | `Fonts/BundledFaces.cs`, `Fonts/Generation/FontDataGeneration.cs`, `Fonts/FontDataResources.cs` | BundledFaceTests, FontDataDemandTests, FontRenderParityTests | Yes | Yes (`core/fxge/fontdata/chromefontdata`) | [#60](https://github.com/glennawatson/PdfViewerLite/issues/60) |
| Font substitution for non-embedded fonts: named system family, then bundled generic face by flags, system fonts for scripts the bundle lacks | 9.8 | Partial: no multiple master width/weight interpolation (glyphs are narrowed or centred to `/Widths`) | `Fonts/SystemFontMatcher.cs`, `Fonts/SubstituteFace.cs` | SystemFontTests, BundledFaceTests | Yes | Yes | [#60](https://github.com/glennawatson/PdfViewerLite/issues/60) |
| Private-use symbol text (U+F020 to U+F0FF from Symbol and ZapfDingbats fonts) remapped to real Unicode in extraction | 9.10.2 | Partial: Symbol and ZapfDingbats only, not Wingdings | `Fonts/PuaSymbols.cs` | BundledFaceTests | Yes | No | [#60](https://github.com/glennawatson/PdfViewerLite/issues/60) |
| Font descriptors, flags, widths, missing width | 9.8 | Partial (implemented fields vary by font subtype; malformed widths are skipped with a repair report) | `Fonts/FontDescriptor.cs`, `Fonts/FontFlags.cs`, `Fonts/FontMetrics.cs`, `Fonts/CidMetricReader.cs` | CompositeFontTests, RepairReportTests | Yes | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58) |
| Glyph outlines to paths (fill, stroke, clip) | 9.3.6 | Done (programs) | `Fonts/Programs/GlyfOutline.cs`, `IGlyphOutlineSink.cs` | CffProgramTests, Type1ProgramTests | Yes | Yes | |

Issue [#128](https://github.com/glennawatson/PdfViewerLite/issues/128) remains open for wider partial-coverage work; the table above records the remaining font gaps.

## 7. Text extraction, search, selection and hit testing

Clause 14.8.2 and 9.10. HyperPdf answers these through the managed interpreter, font loader and text page. Coverage depends on supported font mappings and the known text gaps in section 6.

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Glyph positions and Unicode mapping from the interpreter | 9.10 | Partial (glyph data is produced for supported fonts) | `Content/GlyphEvent.cs`, `Content/GlyphPlacement.cs`, `Text/TextGlyphAssembly.cs` | TextDeviceTests, TextExtractionTests, CjkTextParityTests | Yes (HyperPdf) | Yes (`core/fpdftext/cpdf_textpage.cpp`) | [#61](https://github.com/glennawatson/PdfViewerLite/issues/61) |
| Character count, `GetText`, `GetTextBounds` | 9.10 | Partial (available for extracted glyphs; missing or unsupported mappings can leave gaps) | `Text/PdfTextPage.cs`, `Text/TextPageBuild.cs` | TextExtractionTests, TextParityTests, TextGeometryParityTests, TextCorpusParityTests | Yes (HyperPdf) | Yes | [#61](https://github.com/glennawatson/PdfViewerLite/issues/61) |
| Hit testing (`GetCharacterIndexAt`), selection | 9.10 | Partial (selection geometry follows extracted glyphs) | `Text/PdfTextPage.cs`, `src/PdfViewerLite.HyperPdf/HyperPdfDocument.Text.cs` | TextQueryTests, TextGeometryParityTests, TextParityTests | Yes (HyperPdf) | Yes | [#61](https://github.com/glennawatson/PdfViewerLite/issues/61) |
| Search (`Find`) with case and whole-word options | 9.10 | Partial (managed search works on extracted text; complex-script shaping and normalization remain limited) | `Text/PdfTextPage.Find.cs`, `src/PdfViewerLite.HyperPdf/HyperPdfDocument.Text.cs` | TextQueryTests, TextParityTests, TextCorpusParityTests | Yes (HyperPdf) | Yes (`cpdf_textpagefind.cpp`) | [#61](https://github.com/glennawatson/PdfViewerLite/issues/61) |
| Character layout (font size, weight, line grouping) for `ITextLayoutSource` | 9.10 | Partial (reported for supported fonts; reading order is heuristic) | `Text/PdfTextPage.cs`, `Text/TextLineAssembly.cs`, `src/PdfViewerLite.HyperPdf/HyperPdfDocument.Text.cs` | TextExtractionTests, TextGeometryParityTests, TextCorpusParityTests | Yes (HyperPdf) | Yes | [#61](https://github.com/glennawatson/PdfViewerLite/issues/61) |
| Reading order, hyphen and space detection | 9.10 | Partial (line grouping and generated spaces are implemented; complex layouts remain heuristic) | `Text/TextLineAssembly.cs`, `Text/TextInsertionAssembly.cs` | TextExtractionTests, TextCorpusParityTests | Yes (HyperPdf) | Yes | [#61](https://github.com/glennawatson/PdfViewerLite/issues/61) |
| `/ActualText`, `/Alt`, `/E` replacement text | 14.9 | Partial (`/ActualText` replaces covered glyphs in ordinary extraction; tagged structure also reads `/ActualText` and `/Alt`; `/E` is not implemented) | `Text/TextLineAssembly.cs`, `Structure/Tagged/StructureTreeBuilder.cs`, `Structure/Tagged/TaggedNodeBuilder.cs` | TextExtractionTests, StructureTreeTests, TaggedParityTests | Yes (`/ActualText` extraction and tagged reading) | Partial | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| Complex scripts: bidi, ligatures, combining marks, Arabic shaping, Indic | 9.10 | Missing (Unicode mapping is not script shaping or bidirectional layout) | none | none found | Partial through PDFium | Partial (`unicodenormalizationdata.cpp`) | [#62](https://github.com/glennawatson/PdfViewerLite/issues/62) |
| Web and email links in text | 12.5.6.5 | Done | `Text/PdfTextPage.Links.cs`, `src/PdfViewerLite.HyperPdf/HyperPdfDocument.Text.cs` | TextQueryTests, TextParityTests | Yes (HyperPdf) | Yes (`cpdf_linkextract.cpp`) | [#61](https://github.com/glennawatson/PdfViewerLite/issues/61) |

## 8. Known renderer gaps

These are known managed-engine limits. Each links to the relevant row above.

| Gap | Effect | Issue |
| --- | --- | --- |
| Knockout and non-isolated groups are approximated | Overlapping shapes in a knockout group may blend. | [#65](https://github.com/glennawatson/PdfViewerLite/issues/65) |
| Overprint is ignored | Overprint previews look the same as knockout. | [#66](https://github.com/glennawatson/PdfViewerLite/issues/66) |
| Rendering intent is ignored | Matches PDFium. | [#66](https://github.com/glennawatson/PdfViewerLite/issues/66) |
| Mixed `/Extend` draws as both | One-sided extends paint on both sides. | [#67](https://github.com/glennawatson/PdfViewerLite/issues/67) |
| Mesh shading used as a stroke paint paints nothing | Rare. | [#67](https://github.com/glennawatson/PdfViewerLite/issues/67) |
| Shading `/Background` used only for patterns | `sh` ignores it, as the spec says. Check against PDFium. | [#67](https://github.com/glennawatson/PdfViewerLite/issues/67) |
| Tiling overlap is lost when step is less than the bounding box | Dense overlapping tiles look wrong. | [#67](https://github.com/glennawatson/PdfViewerLite/issues/67) |
| Some annotations without `/AP` remain blank | The renderer generates appearances for common drawing and text subtypes; unsupported subtypes still need an appearance stream. | [#64](https://github.com/glennawatson/PdfViewerLite/issues/64) |
| No minimum line width | Very thin lines can vanish. | [#68](https://github.com/glennawatson/PdfViewerLite/issues/68) |
| Scanned colour books still hold about 21 MB of BGRA per cached page | Skia has no 3-byte pixel format, so a colour scan costs 4 bytes per pixel. Greyscale and bilevel images cost 1 byte. Image and picture caches are bounded by bytes (`PdfRenderOptions`). | [#68](https://github.com/glennawatson/PdfViewerLite/issues/68) |
| Progressive rendering has no app adapter | The library can render in stages, but the app uses its regular render path. | [#68](https://github.com/glennawatson/PdfViewerLite/issues/68) |
| Embedded thumbnail API has no app adapter | The library can decode supported `/Thumb` images; the app renders its own thumbnails. | [#68](https://github.com/glennawatson/PdfViewerLite/issues/68) |
| Forms: push-button captions and icons, rich text values, legacy multi-byte CMaps in field fonts | Some field appearances differ from PDFium. | [#69](https://github.com/glennawatson/PdfViewerLite/issues/69) |
| Fonts: unsupported encodings, script shaping and font interpolation | Text can be incomplete or use a substitute face for these cases. | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58), [#59](https://github.com/glennawatson/PdfViewerLite/issues/59), [#60](https://github.com/glennawatson/PdfViewerLite/issues/60) |

## 9. PDF/A archival conformance

ISO 19005-1 (PDF/A-1a, 1b), -2 (2a, 2b, 2u), -3 (3a, 3b, 3u) and -4 (4, 4e, 4f). Clause numbers below follow ISO 19005-1; later parts use the same topics with some renumbering, so check the part you target.

`PdfDocumentConformance.GetConformance(document)` returns a reading report: the PDF/A claim from XMP, PDF/UA and PDF/X identification, the output intents, and features seen without validating (encryption, JavaScript, LZW, non-embedded fonts, transparency, embedded files, external content, device colour without an output intent). It never says a file conforms. Full validation is out of scope: use veraPDF as the reference tool. Tests: `PdfConformanceReportTests`, `OutputIntentRenderTests`, `ConformanceCorpusTests` (skips files that are not cached). Not yet reported: annotation rules, odd action types, `/CIDSet`, `/Widths`, file structure, optional content in part 1. Where PDFium blocks a standard, HyperPdf follows the standard and PDFium stays the parity baseline: rendering a PDF/A file uses its output intent unless the caller sets `FixedDeviceColors`. The corpus has `pypdf-pdfa` and `pyhanko-ua-and-a` in `tests/corpus/corpus.json`.

What each part asks a reader to know:

| Part | Based on | Levels | Notes for a viewer |
| --- | --- | --- | --- |
| 1 | PDF 1.4 | a, b | No transparency, no LZW, no encryption, no optional content, no embedded files, all fonts embedded. Level a also needs tagged structure. |
| 2 | PDF 1.7 (ISO 32000-1) | a, b, u | Transparency, JPX, optional content and object streams allowed. Embedded files must be PDF/A. Level u needs Unicode text. |
| 3 | Part 2 | a, b, u | Same as part 2, but embedded files may be any format, with `/AF` and `/AFRelationship`. |
| 4 | PDF 2.0 (ISO 32000-2) | none, e, f | No a/b/u levels. Level e allows 3D and RichMedia. Level f allows any embedded file. Revision in `pdfaid:rev`. |

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| XMP packet and `pdfaid` properties read | 6.7.11 | Done | `Metadata/XmpParser.cs`, `Metadata/XmpMetadata.cs` | PdfConformanceReportTests | No | No (no XMP) | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| Claim detection: `pdfaid:part`, `conformance`, `rev`, and the part/level table | 6.7.11 | Done (`PdfDocumentConformance.GetConformance`; `PdfAClaim.IsRecognised` checks the pair) | `Conformance/PdfAClaim.cs`, `Document/PdfDocumentConformance.cs` | PdfConformanceReportTests, ConformanceCorpusTests | No | No | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80) |
| `/OutputIntents` read (`/S` `GTS_PDFA1`, `/OutputConditionIdentifier`, `/Info`) | 6.2.2 | Done (listed in the report with the profile `/N`) | `Conformance/PdfOutputIntentSummary.cs`, `Document/PdfDocumentCatalog.cs` | PdfConformanceReportTests | No | No | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80), [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| `/DestOutputProfile` ICC stream parsed | 6.2.2 | Done (component count in the report; the profile is parsed when the intent applies to rendering) | `Graphics/Colors/OutputIntentColors.cs`, `Graphics/Colors/IccProfile.cs` | PdfConformanceReportTests, OutputIntentRenderTests | No | No | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80) |
| Output intent profile used for DeviceCMYK, DeviceGray and DeviceRGB | 6.2.2, 6.2.3 | Done. HyperPdf follows ISO 19005 by default: a document with `pdfaid:part` and a usable PDF/A or PDF/X intent profile converts device colours (vector colours, images, shadings, named spaces, inline images) through it. `PdfRenderFlags.OutputIntent` adds it for PDF/X and other files; `PdfRenderFlags.FixedDeviceColors` (`RenderFlags.FixedDeviceColors` in the app API) forces the fixed PDFium conversion | `Graphics/Colors/OutputIntentColors.cs`, `Graphics/Colors/OutputIntentColorSpace.cs`, `Document/PdfDocumentOutputIntentRendering.cs`, `Rendering/PdfPageRenderer.cs` | OutputIntentRenderTests (default, flag, PDFium flag, image, shading), ColorFixTests (fixed table) | Yes (default for PDF/A) | No (own table; parity tests set `FixedDeviceColors`) | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80), [#76](https://github.com/glennawatson/PdfViewerLite/issues/76) |
| `/DefaultGray`, `/DefaultRGB`, `/DefaultCMYK` honoured | 6.2.3 | Done | `Graphics/Colors/ColorSpaceParser.cs` | ColorFixTests, ColorSpaceTests | Yes | Yes | |
| Report: fonts not embedded, missing `/CIDSet`, bad `/Widths` | 6.3 | Partial (non-embedded fonts in page, form, pattern and annotation resources; `/CIDSet` and `/Widths` not checked) | `Conformance/ConformanceScan.Pages.cs` | PdfConformanceReportTests | No | No | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80) |
| Embedded font rendering | 6.3 | Partial (supported font programs render; see section 6 for unsupported cases) | see section 6 | see section 6 | Yes (HyperPdf) | Yes | [#58](https://github.com/glennawatson/PdfViewerLite/issues/58), [#59](https://github.com/glennawatson/PdfViewerLite/issues/59) |
| Transparency rendering (parts 2 to 4) | 6.4 | Done | `Rendering/SkiaContentDevice*.cs` | TransparencyRenderTests | Yes | Yes | |
| Report: part 1 forbids `/SMask`, `ca` below 1, non-Normal blend, transparency groups | 6.4 | Done | `Conformance/ConformanceScan.Objects.cs` | PdfConformanceReportTests | No | No | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80) |
| Annotation appearances drawn (`/AP /N`, Print, Hidden, NoView) | 6.5 | Done | `Rendering/PageRecorder.cs`, `Annotations/PdfAppearances.cs` | PageRenderTests, PageAnnotationTests | Yes | Yes | |
| Report: missing `/AP`, Print flag clear, forbidden subtypes, widget `/AA` | 6.5 | Missing | none | none found | No | No | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80) |
| Report: forbidden actions (Launch, Sound, Movie, ResetForm, ImportData, JavaScript, odd Named) | 6.6 | Partial (JavaScript only; the other action types are not reported) | `Conformance/ConformanceScan.Objects.cs` (actions are read: section 4) | PdfConformanceReportTests | No | No | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80) |
| Embedded files read (any format) | 6.8 (part 3), part 4f | Done | `Document/PdfAttachment.cs`, `Document/PdfDocumentAttachments.cs` | AttachmentSignatureParityTests | Yes | Yes | |
| Report: embedded files against the claim (forbidden in 1, PDF/A only in 2, any in 3 and 4f) | 6.1.7 | Done (presence only: conflict in part 1; whether part 2 files are PDF/A is not checked) | `Conformance/ConformanceScan.cs` | PdfConformanceReportTests | No | No | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80) |
| Associated files `/AF` and `/AFRelationship` | 3: 6.8, 4: 6.8 | Done (library reads relationships on catalog, page, annotation, XObject and structure owners; conformance of each attached file is not validated) | `Document/PdfDocumentAssociatedFiles.cs` | PortfolioTests | No | No public associated-file API | [#72](https://github.com/glennawatson/PdfViewerLite/issues/72) |
| Optional content: draw and toggle; report use in part 1; `/AS` must be absent in parts 2 to 4 | 6.1.13 | Partial | `Layers/`, `Layers/OptionalContentReader.cs` | OptionalContentTests | Layer list only | Yes | [#78](https://github.com/glennawatson/PdfViewerLite/issues/78), [#80](https://github.com/glennawatson/PdfViewerLite/issues/80) |
| Report: encryption, LZW (parts 1 to 3), JavaScript, external content (external streams, `/Ref`, OPI) | 6.1 | Done (external content means a stream with `/F`, `/Ref` or `/OPI`) | `Conformance/ConformanceScan.Objects.cs` | PdfConformanceReportTests | No | No | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80), [#79](https://github.com/glennawatson/PdfViewerLite/issues/79) |
| Report: file structure (header and binary comment, data after `%%EOF`, trailer `/ID`) | 6.1 | Missing | none | none found | No | No | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80) |
| Conformance claim report (reading only), plus missing output intent with device colour | none | Done (`PdfDocumentConformance.GetConformance`; benchmark HyperPdfConformanceBenchmarks) | `Conformance/PdfConformanceReport.cs`, `Conformance/ConformanceScan.cs` | PdfConformanceReportTests, ConformanceCorpusTests | No | No | [#80](https://github.com/glennawatson/PdfViewerLite/issues/80) |
| Full PDF/A validation | none | Out of scope (use veraPDF) | none | none | No | No | |

## 10. PDF/UA accessibility conformance

ISO 14289-1 (PDF/UA-1, PDF 1.7) and ISO 14289-2 (PDF/UA-2, PDF 2.0). Both depend on tagged PDF (clause 14.7 and 14.8 of ISO 32000). The managed structure tree reader under `src/HyperPdfLibrary/Structure/Tagged` reads document structure and marked content; app exposure is conditional on tag coverage. Remaining gaps are listed below. Claim reporting is [#81](https://github.com/glennawatson/PdfViewerLite/issues/81): `PdfDocumentAccessibility.GetAccessibilityReport` (folder `src/HyperPdfLibrary/Accessibility`) reads the claim, the document entries, per-page counts and 25 plain-English finding codes. It is a reading report, not a validator: use veraPDF or PAC to check conformance. The corpus has `verapdf-ua-headings`, `verapdf-ua-tables`, `verapdf-ua-notes` and `pyhanko-ua-and-a` in `tests/corpus/corpus.json`; `AccessibilityCorpusTests` reads them (the veraPDF fail files are not cached, so hand-built PDFs cover each finding).

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| XMP `pdfuaid:part` (1 or 2) and `pdfuaid:rev` read | 14289-1 5 | Done | `Metadata/XmpMetadata.cs` (`PdfUaPart`), `Accessibility/PdfAccessibilityReader.cs` (`rev`) | XmpTests, AccessibilityDocumentTests | No | No | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| Claim detection: UA-1 versus UA-2 (UA-2 needs PDF 2.0 and structure namespaces) | 14289-1 5, 14289-2 | Done (reading only) | `Accessibility/PdfUaClaim.cs`, `PdfAccessibilityReader.cs` | AccessibilityDocumentTests, AccessibilityCorpusTests | Internal adapter method only | No | [#81](https://github.com/glennawatson/PdfViewerLite/issues/81) |
| `/MarkInfo /Marked true`, `/Suspects` | 7.1 | Done (reading only) | `Structure/Tagged/PdfStructureTree.cs`, `Accessibility/PdfAccessibilityReader.cs` | AccessibilityDocumentTests, StructureTreeTests | Yes (HyperPdf) | Yes | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| `/StructTreeRoot`, `/K`, `/ParentTree`, `/IDTree`, marked content ids | 7.1 | Partial (tree, child elements, parent and id indexes, and marked content are read; malformed or uncommon structures may be skipped) | `Structure/Tagged/PdfStructureTree.cs`, `Structure/Tagged/StructureTreeBuilder.cs`, `Structure/Tagged/MarkedContentRecorder.cs` | StructureTreeTests, MarkedContentTests, TaggedParityTests | Yes (HyperPdf, when tag coverage passes) | Yes (`core/fpdfapi/page/cpdf_structtree`) | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| `/RoleMap`, standard structure types, cycles | 7.1 | Partial (role mapping and cycle handling are implemented; some PDF 2.0 role semantics remain limited) | `Structure/Tagged/StructureRoleMapper.cs`, `Structure/Tagged/PdfStructureTypes.cs` | StructureTreeTests, AccessibilityStructureTests | Yes (HyperPdf, when tag coverage passes) | Partial | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| PDF 2.0 namespaces: `/NS`, `/Namespaces`, `/RoleMapNS` | 14289-2 | Partial (resolved by the tree reader; the report only says whether they are used) | `Structure/Tagged/StructureRoleMapper.cs`, `Accessibility/PdfUaClaim.cs` | AccessibilityDocumentTests | No | No | [#81](https://github.com/glennawatson/PdfViewerLite/issues/81) |
| `/Lang` on catalog, structure elements and spans | 7.2 | Partial (catalog `NoLanguage` finding; element `/Lang` inherited by the tree; span language changes are not detected) | `Accessibility/PdfAccessibilityReader.cs` | AccessibilityDocumentTests | No | Element `/Lang` via `FPDF_StructElement_GetLang` | [#81](https://github.com/glennawatson/PdfViewerLite/issues/81) |
| `/Alt`, `/ActualText`, `/E` | 7.3, 7.9 | Partial (`/Alt` and `/ActualText` are read in the structure tree; `/ActualText` also replaces covered glyphs in ordinary extraction; `/E` is not implemented) | `Structure/Tagged/StructureTreeBuilder.cs`, `Structure/Tagged/PdfStructureElement.cs`, `Text/TextLineAssembly.cs` | StructureTreeTests, TaggedParityTests, TextExtractionTests | Yes (tagged reading and `/ActualText` extraction) | Yes (`FPDF_StructElement_GetAltText`, `GetActualText`) | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| Tables: `TH`, `TD`, `/Scope`, `/Headers`, `/ID`, THead, TBody, TFoot | 7.5 | Partial (table types and header scope are read and checked; full relationships and reading order are not validated) | `Structure/Tagged/PdfStructureTypes.cs`, `Accessibility/AccessibilityStructureScan.cs` | AccessibilityStructureTests, StructureTreeTests | No | Types only | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| Headings (`H`, `H1` to `H6`), lists (`L`, `LI`, `Lbl`, `LBody`), figures, notes and references | 7.4, 7.6, 7.3, 7.9 | Partial (headings, list items and figures are checked; full semantic relationships remain unvalidated) | `Structure/Tagged/PdfStructureTypes.cs`, `Accessibility/AccessibilityStructureScan.cs` | AccessibilityStructureTests, StructureTreeTests | No | Types only | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| Artifacts: untagged content marked `/Artifact` (`/Type`, `/Subtype`) | 7.1 | Partial (artifacts are identified in marked-content reading; ordinary text extraction does not filter them) | `Structure/Tagged/MarkedContentRecorder.cs`, `Structure/Tagged/MarkedContentScanner.cs` | MarkedContentTests, AccessibilityDocumentTests | Yes (accessibility reading only) | Yes | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| `DisplayDocTitle` in `/ViewerPreferences`, title in XMP | 7.1 | Done (reading only) | `Accessibility/PdfAccessibilityReader.cs` | AccessibilityDocumentTests | No | No | [#81](https://github.com/glennawatson/PdfViewerLite/issues/81) |
| Page `/Tabs /S` (tab order follows structure) | 7.18.3 | Done (reading only) | `Accessibility/AccessibilityPageScan.cs` | AccessibilityDocumentTests | No | No | [#81](https://github.com/glennawatson/PdfViewerLite/issues/81) |
| Annotations in the tree: `OBJR`, `/Contents` on links, `/TU` on widgets | 7.18 | Done (reading only) | `Accessibility/AccessibilityPageScan.cs`, `AccessibilityStructureScan.cs` | AccessibilityAnnotationTests | No | No | [#81](https://github.com/glennawatson/PdfViewerLite/issues/81) |
| Report: structure faults (unmapped types, role map loops, heading order, figures without a description, tables, list items, untagged text) | 7.2 to 7.9 | Done (reading only) | `Accessibility/AccessibilityStructureScan.cs`, `AccessibilityPageScan.cs` | AccessibilityStructureTests, AccessibilityDocumentTests | No | No | [#81](https://github.com/glennawatson/PdfViewerLite/issues/81) |
| Unicode mapping for text (ToUnicode parser) | 7.21 | Done (parser) | `Fonts/CMaps/ToUnicodeMap.cs` | CMapTests | Yes | Yes | |
| Structure and reading order exposed to the app | 7.1 | Partial (tagged blocks are returned when marked text coverage passes; inferred order is available separately) | `src/PdfViewerLite.HyperPdf/HyperPdfDocument.Tagged.cs`, `Structure/Tagged/PdfReadingStructure.cs` | TaggedParityTests, NativeTaggedSource, InferredOrderTests | Yes (HyperPdf, conditional) | Yes | [#63](https://github.com/glennawatson/PdfViewerLite/issues/63) |
| Conformance claim report (reading only) | none | Done | `Accessibility/PdfAccessibilityReport.cs`, `Document/PdfDocumentAccessibility.cs`, adapter `HyperPdfDocument.Accessibility.cs` | AccessibilityDocumentTests | Internal method, not in Core yet | No | [#81](https://github.com/glennawatson/PdfViewerLite/issues/81) |
| Colour contrast and "not by colour alone" | 7.1 | Out of scope (needs human review) | none | none | No | No | |
| Full PDF/UA validation | none | Out of scope (use veraPDF) | none | none | No | No | |

## 11. PDF/R raster-only PDF

ISO 23504-1. PDF/R is a profile for scanned documents: pages hold raster images, with a limited set of image filters. The standard text was not available, so these rules are inferred or come from secondary sources. Confirmed from public sources (the PDF Association's PDF/R page and a third-party validator's documentation, not the standard): the file claims PDF/R with a `%PDF-raster-x.y` comment between the trailer and `startxref`, images may use JPEG, CCITT Group 4 or uncompressed data, and the validator's default is one full-page raster per page with no text, paths, forms or shadings. Inferred and not confirmed: the allow-list in the issue (Flate, CCITT, DCT, JBIG2, JPX), the invisible-text exception and the resolution range. `PdfDocumentRaster.GetRasterReport` reports structure only (`Raster/`): a page is raster-only when it paints images and nothing else except render mode 3 text. It reads the claim comment from the last 8 KiB and never validates it. PDFium has no PDF/R awareness. The corpus has scanned JBIG2 files (`loc-brown-v-board` and four more `loc-*` entries) that are close to PDF/R content.

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Identification marker (`%PDF-raster-x.y` comment, last 8 KiB; form from secondary sources) | ISO 23504-1 | Done (reads the marker as a claim; does not validate conformance) | `Raster/RasterClaimReader.cs` | RasterReportTests | No | No | [#82](https://github.com/glennawatson/PdfViewerLite/issues/82) |
| FlateDecode images | 7.4.4 | Done | `Filters/FlateFilter.cs` | FilterTests, ImageDecoderTests | Yes | Yes | |
| CCITTFaxDecode images | 7.4.6 | Done | `Graphics/Images/CcittFaxDecoder.cs` | CcittFaxDecoderTests | Yes | Yes | |
| DCTDecode (JPEG) images | 7.4.8 | Done | `Graphics/Images/Jpeg/` | JpegDecoderTests, JpegImageDecoderTests | Yes | Yes | |
| JBIG2Decode images | 7.4.7 | Done | `Graphics/Images/Jbig2/` | Jbig2DecoderTests, Jbig2ImageDecoderTests | Yes | Yes | [#56](https://github.com/glennawatson/PdfViewerLite/issues/56) |
| JPXDecode (JPEG 2000) images | 7.4.9 | Partial (only MIXED high-throughput mode is unsupported) | `Graphics/Images/Jpx/` | JpxDecoderTests, JpxImageDecoderTests, JpxHighThroughputTests | Yes | Yes | [#57](https://github.com/glennawatson/PdfViewerLite/issues/57) |
| Full-page image rendering, stencil masks and mask-plus-background layers | 8.9 | Done | `Graphics/Images/ImageMasks.cs`, `ImageAlpha.cs` | ImageRenderTests, ImageFixTests | Yes | Yes | |
| Filter allow-list check (Flate, CCITT, DCT, JBIG2, JPX only; list inferred from the issue) | ISO 23504-1 | Done | `Raster/RasterImageReader.cs` | RasterReportTests, RasterCorpusTests | No | No | [#82](https://github.com/glennawatson/PdfViewerLite/issues/82) |
| Raster-only page check (no path painting, shading or visible text) | ISO 23504-1 | Done | `Raster/RasterPageScanner.cs` | RasterReportTests, RasterCorpusTests | No | No | [#82](https://github.com/glennawatson/PdfViewerLite/issues/82) |
| Image resolution report (from the CTM; `/UserUnit` not applied) | ISO 23504-1 | Done | `Raster/RasterImageReader.cs` | RasterReportTests, RasterCorpusTests | No | No | [#82](https://github.com/glennawatson/PdfViewerLite/issues/82) |
| Hidden OCR text layer extraction (render mode 3) | 9.3.6, 9.10 | Partial (available when the font mapping is supported) | see section 7 | TextExtractionTests, TextParityTests | Yes (HyperPdf) | Yes | [#61](https://github.com/glennawatson/PdfViewerLite/issues/61) |
| Conformance claim report (reading only) | none | Done (structure report, not PDF/R validation) | `Raster/PdfRasterReport.cs`, `Document/PdfDocumentRaster.cs` | RasterReportTests, RasterCorpusTests | No | No | [#82](https://github.com/glennawatson/PdfViewerLite/issues/82) |

## 12. XFDF and FDF

FDF is in ISO 32000-1 clause 12.7.8. XFDF is ISO 19444-1. Both carry form field values and annotations. The managed library imports and exports both formats; the app has no import or export command. XFDF uses streaming `XmlReader` and `XmlWriter` with DTDs prohibited and a size limit. PDFium has an internal FDF parser and export, but no public import/export API for either format. Remaining integration is tracked in [#83](https://github.com/glennawatson/PdfViewerLite/issues/83).

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| FDF file syntax: `%FDF-1.2`, `/FDF`, `/Fields`, `/Annots`, `/F`, `/ID`, `/Status`, `/Encoding` | 12.7.8.2 | Partial (reader and writer cover tested fields and annotations; no full syntax conformance claim) | `Interchange/FdfReader.cs`, `FdfWriter.cs`, `FdfDictionaryReader*.cs` | FdfTests, InterchangeRoundTripTests | No | Internal only (`cfdf_document.cpp`) | [#83](https://github.com/glennawatson/PdfViewerLite/issues/83) |
| FDF field import (`/T`, `/V`, `/Ff`, `/SetFf`, `/ClrFf`, `/AP`) | 12.7.8 | Partial (tested values import; all listed flags and appearances are not established) | `Interchange/FdfDictionaryReader.Fields.cs`, `Interchange/InterchangeImporter.cs` | FdfTests, InterchangeRoundTripTests | No | Internal only | [#83](https://github.com/glennawatson/PdfViewerLite/issues/83) |
| FDF annotation import (`/Annots`, `/Page`) | 12.7.8 | Done (supported annotation types) | `Interchange/FdfDictionaryReader.Annotations.cs`, `Interchange/InterchangeAnnotationImporter.cs` | InterchangeRoundTripTests, FdfTests | No | Internal only | [#83](https://github.com/glennawatson/PdfViewerLite/issues/83) |
| FDF export | 12.7.8 | Done (supported fields and annotations) | `Interchange/FdfWriter.cs`, `Interchange/PdfInterchange.cs` | FdfTests, InterchangeRoundTripTests | No | Internal only (`ExportFormToFDFTextBuf`) | [#83](https://github.com/glennawatson/PdfViewerLite/issues/83) |
| XFDF syntax: `xfdf`, `f`, `ids`, `fields`, `field`, `value`, `annots`, `pages`, `javascript` | ISO 19444-1 | Partial (tested elements read and written; no full ISO 19444 conformance claim) | `Interchange/XfdfReader.cs`, `XfdfParser*.cs`, `XfdfWriter.cs` | XfdfReadTests, InterchangeRoundTripTests | No | No | [#83](https://github.com/glennawatson/PdfViewerLite/issues/83) |
| XFDF annotation elements (text, highlight, ink, line and others) with `page`, `rect`, `color`, `contents` | ISO 19444-1 | Done (supported annotation elements) | `Interchange/XfdfParser.Annotations.cs`, `XfdfAnnotationWriter.cs` | XfdfReadTests, InterchangeRoundTripTests | No | No | [#83](https://github.com/glennawatson/PdfViewerLite/issues/83) |
| XFDF import (fields by name, annotations by page) | ISO 19444-1 | Done (supported fields and annotations) | `Interchange/PdfInterchange.cs`, `Interchange/InterchangeImporter.cs` | XfdfReadTests, InterchangeRoundTripTests | No | No | [#83](https://github.com/glennawatson/PdfViewerLite/issues/83) |
| XFDF export | ISO 19444-1 | Done (supported fields and annotations) | `Interchange/XfdfWriter.cs`, `Interchange/PdfInterchange.cs` | InterchangeRoundTripTests | No | No | [#83](https://github.com/glennawatson/PdfViewerLite/issues/83) |
| `SubmitForm` and `ImportData` actions that use FDF or XFDF | 12.7.5, 12.6.4 | Partial (action data and host callbacks exist; the library and app do not send or import action files) | `Document/PdfDocumentActionTypes.cs`, `Navigation/PdfActionRunner.cs` | ActionDataTests, FormRuntimeTests | No | Recognised, not run | [#70](https://github.com/glennawatson/PdfViewerLite/issues/70) |
| Safe XML reading: no DTD, no entity expansion, size cap | ISO 19444-1 | Done (XFDF input) | `Interchange/XfdfReader.cs` | XfdfReadTests | No | n/a | [#83](https://github.com/glennawatson/PdfViewerLite/issues/83) |

## 13. XMP metadata

ISO 16684-1. HyperPdf reads catalog, page and object XMP packets, and edits selected document properties in both `/Info` and existing XMP. The reader uses streaming `XmlReader`, prohibits DTDs and limits decoded length. The parser stores values under namespace-qualified keys and exposes common Dublin Core, PDF, XMP, PDF/A and PDF/UA properties. It does not implement the complete RDF data model or compare XMP with `/Info`. The app still shows the information dictionary; PDFium's metadata API reads `/Info`. Remaining work is tracked in [#73](https://github.com/glennawatson/PdfViewerLite/issues/73).

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Packet scanning: `<?xpacket begin=...?>` and `<?xpacket end=...?>` wrapper, padding, UTF-8, UTF-16, UTF-32 | 16684-1 7.3 | Partial (UTF-8 and UTF-16 packets read; editor preserves wrappers and padding; UTF-32 is untested) | `Metadata/XmpParser.cs`, `Editing/XmpEncoding.cs`, `Editing/XmpTranscoder.cs` | XmpTests, XmpEditorTests | No | No | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| Metadata stream location (`/Metadata` on the catalog, `/Type /Metadata`, `/Subtype /XML`), `/EncryptMetadata` | 14.3.2 | Partial (catalog and page streams read; encryption exemption handled; stream type and subtype are not validated) | `Document/PdfDocumentMetadata.cs`, `Security/PdfSecurityHandler.cs` | XmpTests, StreamCryptTests | No | No | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| RDF/XML forms: attributes versus child elements, `rdf:parseType="Resource"`, nested structures | 16684-1 7.4 | Partial (description attributes and direct child properties read; nested resource values are not modelled) | `Metadata/XmpParser.cs` | XmpTests | No | No | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| `rdf:Alt`, `rdf:Seq`, `rdf:Bag`, `rdf:li`, `xml:lang` alternatives and `x-default` | 16684-1 7.5 | Partial (array items and `x-default` priority read; language labels are not retained in the public value map) | `Metadata/XmpParser.cs`, `Metadata/XmpMetadata.cs` | XmpTests, XmpEditorTests | No | No | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| Namespaces: `dc`, `xmp`, `pdf`, `xmpMM`, `xmpRights` | 16684-1 8 | Partial (generic namespace-qualified values read; typed accessors cover common `dc`, `xmp` and `pdf` fields) | `Metadata/XmpParser.cs`, `Metadata/XmpMetadata.cs` | XmpTests | No | No | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| Namespaces: `pdfaid`, `pdfuaid`, `pdfx`, and PDF/A extension schemas | 19005, 14289, 15930 | Partial (`pdfaid` and `pdfuaid` claims interpreted; other values remain generic strings) | `Metadata/XmpMetadata.cs`, `Conformance/PdfAClaim.cs`, `Accessibility/PdfUaClaim.cs` | XmpTests, PdfConformanceReportTests, AccessibilityDocumentTests | No | No | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73), [#80](https://github.com/glennawatson/PdfViewerLite/issues/80), [#81](https://github.com/glennawatson/PdfViewerLite/issues/81) |
| Value types: Text, Date (ISO 8601), URI, Boolean, Integer, LangAlt | 16684-1 8 | Partial (dates and claim integers parsed; other values remain strings, and language labels are not preserved) | `Metadata/XmpMetadata.cs`, `Metadata/XmpParser.cs` | XmpTests | No | No | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| Page and object level metadata (`/Metadata` on pages, form and image XObjects, annotations) | 14.3.2 | Partial (page API tested; any owner dictionary can be read, but other owner kinds lack direct tests) | `Document/PdfDocumentMetadata.cs` | XmpTests | No | No | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| XMP versus information dictionary: prefer XMP, report differences | 14.3.2 | Missing | none | none found | No | No | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| Safe streaming XML: `XmlReader` only, no DTD, size cap | 16684-1 | Done (XMP input) | `Metadata/XmpParser.cs` | XmpTests | No | n/a | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |
| XMP writing and update on save | 16684-1 | Partial (selected properties update existing well-formed XMP; no new packet is created for a document without one) | `Document/PdfDocumentMetadataEditing.cs`, `Editing/XmpEditor.cs` | MetadataEditTests, XmpEditorTests | No | No | [#73](https://github.com/glennawatson/PdfViewerLite/issues/73) |

## 14. Optimising

`PdfOptimizer` writes a smaller copy of a document. It works on a private copy over the same file, so the open document never changes. Objects go to the destination as they are written, and images and fonts are re-encoded one at a time. PDFium has no optimiser; the PDFium column says whether PDFium reads the output. These rows are not in the counts below.

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Presets: Smaller (150 ppi, JPEG 70), Balanced (200 ppi, JPEG 80), KeepQuality (lossless); every setting overridable | n/a | Done | `Optimizing/PdfOptimizeOptions.cs` | ImageOptimizationTests | No | n/a | |
| Report: sizes, savings by category, actions, warnings, skipped items with reasons; progress and cancellation; async form | n/a | Done | `Optimizing/PdfOptimizer.cs`, `PdfOptimizeReport.cs`, `OptimizeSession.cs` | SafetyTests | No | n/a | |
| Effective image resolution from the largest use, through forms; images in patterns, soft masks, annotations and Type 3 glyphs keep their resolution | 8.9, 8.10 | Done | `Optimizing/ContentUsageScanner.cs` | ImageOptimizationTests | No | n/a | |
| Downsampling (box filter, then Mitchell cubic) and JPEG for grey and RGB images, including ICC N 1 or 3; never bigger | 8.9.5 | Done | `Optimizing/ImageRecoder.cs`, `ImagePixels.cs` | ImageOptimizationTests, OptimizerPdfiumTests | No | Reads it | |
| Black-and-white images as CCITT Group 4 or Flate, bit for bit | 7.4.6 | Done | `Graphics/Images/CcittG4Encoder.cs`, `Optimizing/BilevelEncoder.cs` | CcittG4EncoderTests, ImageOptimizationTests, OptimizerPdfiumTests | No | Reads it | |
| Masks, colour-keyed, /Matte, CMYK, indexed and Lab images kept lossless; JBIG2 and JPEG 2000 left alone | 8.9.6, 11.6.5 | Done (JBIG2 and JPX are never re-encoded) | `Optimizing/ImageRecoder.cs`, `ImageColor.cs` | ImageOptimizationTests | No | Reads it | |
| Stream recompression at zlib level 9, predictors kept, LZW and ASCII filters replaced | 7.4 | Done | `Optimizing/StreamRecompressor.cs`, `Compat/ZLibSmallest.cs` | StructureOptimizationTests | No | Reads it | |
| Duplicate streams, fonts, descriptors, graphics states and colour spaces merged; unreachable objects dropped | 7.3.10 | Done | `Optimizing/DuplicateFinder.cs`, `OptimizerGraph.cs` | StructureOptimizationTests | No | Reads it | |
| Object streams and cross-reference stream, written progressively; classic table for PDF/A-1 | 7.5.7, 7.5.8 | Done | `Optimizing/OptimizedFileWriter.cs` | StructureOptimizationTests, SafetyTests | No | Reads it | |
| TrueType subsetting with glyph numbers kept (sparse /glyf), composite parts kept; fonts used by forms, annotations and patterns kept whole | 9.6.4, 9.9 | Partial (CFF and OpenType programs are not subset; subset tags are not added to /BaseFont) | `Optimizing/TrueTypeSubsetter.cs`, `FontSubsetPlanner.cs`, `GlyphClosure.cs`, `SfntWriter.cs` | FontSubsetTests, OptimizerPdfiumTests | No | Reads it | |
| Cleanup (opt-in): thumbnails, /PieceInfo, unused page resources, empty /Annots | 14.5, 14.7 | Done | `Optimizing/CleanupPass.cs` | CleanupTests | No | Reads it | |
| Structure tree, parent tree, /StructParents and MCIDs kept through renumbering | 14.7 | Done | `Optimizing/OptimizerGraph.cs` | AccessibilityOptimizationTests | No | Reads it | |
| /Lang, title, /DisplayDocTitle and /MarkInfo filled in when missing | 14.9, 12.2 | Done | `Optimizing/AccessibilityPass.cs` | AccessibilityOptimizationTests | No | n/a | |
| Inferred tags for untagged pages: Document, Sect, H1 to H6, P, Figure; marked as inferred in XMP | 14.7, 14.8 | Partial (no lists or tables; figures need alternative text; form XObjects without text become artifacts) | `Optimizing/InferredTagger.cs`, `MarkedContentRewriter.cs`, `InferredTagsXmp.cs` | AccessibilityOptimizationTests | No | Reads it | |
| Text layers from a recognition hook on image-only pages | 9.3.6 | Done | `Optimizing/OcrLayerPass.cs` | SafetyTests | No | Reads it | |
| Safety: signed files copied or appended to incrementally; encryption kept or removed on request; PDF/A claims limit changes; version never lowered | 12.8, 7.6, 19005 | Done | `Optimizing/OptimizeSession.cs`, `WritePlan.cs` | SafetyTests, OptimizerPdfiumTests | No | Reads it | |
| App adapter optimises the open document: unsaved edits (annotations, form values) are in the copy, and annotations removed but kept are left out as saving leaves them out and stay undoable; the snapshot is taken under the edit gate and the run needs no lock | n/a | Done | adapter `HyperPdfDocument.Optimizing.cs`, `HyperPdfAnnotations.Optimizing.cs` | OptimizeCopyTests | Core `IDocumentOptimizer` | Reads it | |

## 15. Page content objects and redaction

`PdfPageContent` reads a page's content stream (or a form's) into objects: text, paths, images, shadings and forms, each with its matrix, clip, colours, marked-content marks and bounds. Unchanged objects keep their original bytes when the content is written again, so `/MCID` marks and the structure tree stay valid. `PdfRedactor` builds on it. These rows are not in the counts below.

| Row | Clause | Status | HyperPdf | Tests | App | PDFium | Issue |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Content parsed into text, path, image (XObject and inline), shading and form objects with matrix, clip chain, fill and stroke colour, line width, marks and bounds; forms read on demand | 8, 9.4 | Done | `PageObjects/PageContentParser*.cs`, `PdfPageContent.cs`, `PdfPageObject.cs` and the object types | PageObjectParseTests | Via redaction | Yes (`FPDFPage_*Obj*`) | [#84](https://github.com/glennawatson/PdfViewerLite/issues/84) |
| Text objects: font, size, render mode, spacing, code bytes, per-glyph origin, advance and box; glyphs removed with the text after them kept in place by `TJ` numbers | 9.4 | Done | `PageObjects/PdfTextObject.cs`, `PdfPageContent.Text.cs` | PageObjectEditTests | Via redaction | Yes | [#84](https://github.com/glennawatson/PdfViewerLite/issues/84) |
| Delete, move and recolour objects, written with `q cm Q`, colour operators or a text matrix; a removed path keeps its clip | 8.4 | Done | `PageObjects/PdfPageContent.Regenerate.cs` | PageObjectEditTests | Via redaction | Yes (`FPDFPage_GenerateContent`) | [#84](https://github.com/glennawatson/PdfViewerLite/issues/84) |
| Images: raw and decoded data, filters, replacement pixels and soft masks, replacement stencil masks | 8.9 | Done | `PageObjects/PdfImageObject.cs`, `ImageStreams.cs` | PageObjectEditTests | Via redaction | Yes | [#84](https://github.com/glennawatson/PdfViewerLite/issues/84) |
| Regenerate keeps the original bytes (`Preserve`) or rewrites every object from the model (`Rewrite`); round trip of cached corpus pages is pixel-identical (`Preserve`) | n/a | Done | `PageObjects/PdfRegenerateMode.cs` | PageContentRoundTripTests | No | n/a | [#84](https://github.com/glennawatson/PdfViewerLite/issues/84) |
| Edited pages saved incrementally and compactly reopen in HyperPdf and PDFium | 7.5 | Done | `Document/PdfDocumentPageContent.cs` | PageObjectEditTests, PageObjectPdfiumTests | No | Reads it | [#84](https://github.com/glennawatson/PdfViewerLite/issues/84) |
| Redaction: text glyphs, invisible text option, image modes (remove, blank pixels, unless invisible), line art modes, links, widgets and other annotations, overlay, `/ActualText` and `/Alt` of removed text, thumbnails, unused resources, `/ToUnicode` entries, optional metadata scrub | 12.5.6.23 | Done (fonts named by a remaining `Tf` stay in the resources; embedded glyph programs are not trimmed) | `Redaction/` | RedactionTextTests, RedactionContentTests, RedactionApplyTests, RedactionCorpusTests | Yes | Subtype only | [#85](https://github.com/glennawatson/PdfViewerLite/issues/85) |
| Apply always writes a compact file; an incremental update after applying is refused | 7.5.6 | Done | `Objects/PdfObjectStore.Redaction.cs`, `Redaction/PdfRedactor.cs` | RedactionTextTests | Yes | n/a | [#85](https://github.com/glennawatson/PdfViewerLite/issues/85) |
| Viewer: mark area or text, review in the comment list, apply with a cannot-undo warning and confirmation, save a redacted copy | n/a | Done | `src/PdfViewerLite.App/ViewModels/RedactionViewModel.cs`, `Views/RedactionWindow.axaml` | RedactionViewModelTests, RedactionWindowTests | Yes | n/a | [#85](https://github.com/glennawatson/PdfViewerLite/issues/85) |

## Counts

Counts cover all status rows in sections 1 to 13, including the 10 ExtGState rows in section 5. A status with a note counts under its first status phrase. Sections 9 to 13 contain 74 rows: 40 Done, 28 Partial, 3 Missing and 3 Out of scope. Sections 14 and 15 are excluded.

| Status | Rows |
| --- | --- |
| Done | 174 |
| Partial | 72 |
| Missing | 19 |
| Read only | 1 |
| Compat (PDFium) | 2 |
| Out of scope | 5 |

Issues #54 to #83 track the remaining gaps. Wider partial-coverage cleanup remains tracked by [#128](https://github.com/glennawatson/PdfViewerLite/issues/128). An open issue can also cover app integration or cases beyond the tested library path, so an issue link alone does not determine a row's status.
