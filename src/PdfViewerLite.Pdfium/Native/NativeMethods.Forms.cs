// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>PDFium entry points for interactive forms.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>Native <c>FPDFDOC_InitFormFillEnvironment</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="formInfo">The <c>FPDF_FORMFILLINFO</c>, which must outlive the environment.</param>
    /// <returns>The form handle, or 0 when the document has no form.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFDOC_InitFormFillEnvironment(PdfiumDocumentHandle document, void* formInfo);

    /// <summary>Native <c>FPDFDOC_ExitFormFillEnvironment</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    [LibraryImport(Library)]
    internal static partial void FPDFDOC_ExitFormFillEnvironment(nint form);

    /// <summary>Native <c>FPDF_GetFormType</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <returns>0 for no form, 1 for AcroForm, 2 or 3 for XFA.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_GetFormType(PdfiumDocumentHandle document);

    /// <summary>Native <c>FPDFDoc_GetJavaScriptActionCount</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The document-level JavaScript actions, or -1 on error.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFDoc_GetJavaScriptActionCount(PdfiumDocumentHandle document);

    /// <summary>Native <c>FORM_OnAfterLoadPage</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="form">The form handle.</param>
    [LibraryImport(Library)]
    internal static partial void FORM_OnAfterLoadPage(PdfiumPageHandle page, PdfiumFormHandle form);

    /// <summary>Native <c>FORM_OnBeforeClosePage</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="form">The form handle.</param>
    [LibraryImport(Library)]
    internal static partial void FORM_OnBeforeClosePage(PdfiumPageHandle page, PdfiumFormHandle form);

    /// <summary>Native <c>FPDF_FFLDraw</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="page">The page.</param>
    /// <param name="startX">The left of the page in the bitmap.</param>
    /// <param name="startY">The top of the page in the bitmap.</param>
    /// <param name="sizeX">The page width in pixels.</param>
    /// <param name="sizeY">The page height in pixels.</param>
    /// <param name="rotate">The rotation in quarter turns.</param>
    /// <param name="flags">The render flags.</param>
    [LibraryImport(Library)]
    internal static partial void FPDF_FFLDraw(PdfiumFormHandle form, nint bitmap, PdfiumPageHandle page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    /// <summary>Native <c>FPDF_SetFormFieldHighlightColor</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="fieldType">The field type, or 0 for all.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    [LibraryImport(Library)]
    internal static partial void FPDF_SetFormFieldHighlightColor(PdfiumFormHandle form, int fieldType, CULong color);

    /// <summary>Native <c>FPDF_SetFormFieldHighlightAlpha</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="alpha">The opacity.</param>
    [LibraryImport(Library)]
    internal static partial void FPDF_SetFormFieldHighlightAlpha(PdfiumFormHandle form, byte alpha);

    /// <summary>Native <c>FORM_SetFocusedAnnot</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="annotation">The widget.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FORM_SetFocusedAnnot(PdfiumFormHandle form, nint annotation);

    /// <summary>Native <c>FORM_ForceToKillFocus</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FORM_ForceToKillFocus(PdfiumFormHandle form);

    /// <summary>Native <c>FORM_SelectAllText</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="page">The page.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FORM_SelectAllText(PdfiumFormHandle form, PdfiumPageHandle page);

    /// <summary>Native <c>FORM_ReplaceSelection</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="page">The page.</param>
    /// <param name="text">The null terminated UTF-16LE text.</param>
    [LibraryImport(Library)]
    internal static partial void FORM_ReplaceSelection(PdfiumFormHandle form, PdfiumPageHandle page, char* text);

    /// <summary>Native <c>FORM_OnChar</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="page">The page.</param>
    /// <param name="character">The character code.</param>
    /// <param name="modifiers">The key modifiers.</param>
    /// <returns>Non-zero when handled.</returns>
    [LibraryImport(Library)]
    internal static partial int FORM_OnChar(PdfiumFormHandle form, PdfiumPageHandle page, int character, int modifiers);

    /// <summary>Native <c>FORM_SetIndexSelected</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="page">The page.</param>
    /// <param name="index">The option index.</param>
    /// <param name="selected">Whether it is selected.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FORM_SetIndexSelected(PdfiumFormHandle form, PdfiumPageHandle page, int index, int selected);

    /// <summary>Native <c>FPDFAnnot_GetFormFieldType</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="annotation">The widget.</param>
    /// <returns>The field type.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_GetFormFieldType(PdfiumFormHandle form, nint annotation);

    /// <summary>Native <c>FPDFAnnot_GetFormFieldFlags</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="annotation">The widget.</param>
    /// <returns>The field flags.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_GetFormFieldFlags(PdfiumFormHandle form, nint annotation);

    /// <summary>Native <c>FPDFAnnot_GetFormAdditionalActionJavaScript</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="annotation">The widget.</param>
    /// <param name="formEvent">The event: 12 keystroke, 13 format, 14 validate, 15 calculate.</param>
    /// <param name="buffer">The UTF-16LE output buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The needed length in bytes.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAnnot_GetFormAdditionalActionJavaScript(PdfiumFormHandle form, nint annotation, int formEvent, void* buffer, CULong length);

    /// <summary>Native <c>FPDFAnnot_GetFormFieldName</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="annotation">The widget.</param>
    /// <param name="buffer">The UTF-16LE buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The required length in bytes.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAnnot_GetFormFieldName(PdfiumFormHandle form, nint annotation, void* buffer, CULong length);

    /// <summary>Native <c>FPDFAnnot_GetFormFieldValue</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="annotation">The widget.</param>
    /// <param name="buffer">The UTF-16LE buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The required length in bytes.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAnnot_GetFormFieldValue(PdfiumFormHandle form, nint annotation, void* buffer, CULong length);

    /// <summary>Native <c>FPDFAnnot_IsChecked</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="annotation">The widget.</param>
    /// <returns>Non-zero when checked.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_IsChecked(PdfiumFormHandle form, nint annotation);

    /// <summary>Native <c>FPDFAnnot_GetOptionCount</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="annotation">The widget.</param>
    /// <returns>The number of options, or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_GetOptionCount(PdfiumFormHandle form, nint annotation);

    /// <summary>Native <c>FPDFAnnot_GetOptionLabel</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="annotation">The widget.</param>
    /// <param name="index">The option index.</param>
    /// <param name="buffer">The UTF-16LE buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The required length in bytes.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAnnot_GetOptionLabel(PdfiumFormHandle form, nint annotation, int index, void* buffer, CULong length);

    /// <summary>Native <c>FPDFAnnot_IsOptionSelected</c> entry point.</summary>
    /// <param name="form">The form handle.</param>
    /// <param name="annotation">The widget.</param>
    /// <param name="index">The option index.</param>
    /// <returns>Non-zero when selected.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_IsOptionSelected(PdfiumFormHandle form, nint annotation, int index);

    /// <summary>Native <c>FPDFAnnot_GetFontSize</c> entry point: the font size a field's text is drawn at, 0 for auto.</summary>
    /// <param name="form">The form.</param>
    /// <param name="annotation">The widget.</param>
    /// <param name="value">Receives the size.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_GetFontSize(PdfiumFormHandle form, nint annotation, out float value);

    /// <summary>Native <c>FPDFAnnot_GetNumberValue</c> entry point: reads a number from the annotation's dictionary.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The null terminated key.</param>
    /// <param name="value">Receives the number.</param>
    /// <returns>Non-zero when the key holds a number.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_GetNumberValue(nint annotation, byte* key, out float value);
}
