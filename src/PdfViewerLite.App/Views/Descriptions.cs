// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.App.Views;

/// <summary>
/// What each control does, in short plain English. Each text is both the tooltip and the screen reader help text
/// (see <see cref="ControlHelp"/>). A shortcut, when there is one, comes after the explanation and matches
/// <see cref="Shortcuts"/>.
/// </summary>
public static class Descriptions
{
    /// <summary>The Back button.</summary>
    public static readonly string Back = $"Go back to where you were before a jump, like Back in a web browser. Jumps come from links, the outline, search results and page numbers.{Shortcut}Alt+Left";

    /// <summary>The Forward button.</summary>
    public static readonly string Forward = $"Go forward again after going Back, like Forward in a web browser.{Shortcut}Alt+Right";

    /// <summary>The Sidebar toggle.</summary>
    public static readonly string Sidebar = $"Show or hide the sidebar with page pictures, the outline and search results.{Shortcut}F9";

    /// <summary>The previous page button.</summary>
    public static readonly string PreviousPage = $"Go to the previous page.{Shortcut}Page Up";

    /// <summary>The next page button.</summary>
    public static readonly string NextPage = $"Go to the next page.{Shortcut}Page Down";

    /// <summary>The page number box.</summary>
    public static readonly string PageNumber = $"Type a page number, then press Enter to go to that page.{Shortcut}Ctrl+L";

    /// <summary>The zoom out button.</summary>
    public static readonly string ZoomOut = $"Make the pages smaller.{Shortcut}Ctrl+Minus";

    /// <summary>The zoom in button.</summary>
    public static readonly string ZoomIn = $"Make the pages bigger.{Shortcut}Ctrl+Plus";

    /// <summary>The zoom menu button, which shows the current zoom.</summary>
    public static readonly string ZoomMenu = "Shows the current zoom. Choose to fit the width, fit the whole page or pick a set size.";

    /// <summary>The fit width zoom.</summary>
    public static readonly string FitWidth = $"Make the page fill the width of the window.{Shortcut}Ctrl+2";

    /// <summary>The fit page zoom.</summary>
    public static readonly string FitPage = $"Make the whole page fit in the window.{Shortcut}Ctrl+1";

    /// <summary>The 50% zoom.</summary>
    public static readonly string Zoom50 = "Show the pages at half their real size.";

    /// <summary>The 75% zoom.</summary>
    public static readonly string Zoom75 = "Show the pages at three quarters of their real size.";

    /// <summary>The 100% zoom.</summary>
    public static readonly string Zoom100 = $"Show the pages at their real size.{Shortcut}Ctrl+0";

    /// <summary>The 125% zoom.</summary>
    public static readonly string Zoom125 = "Show the pages a quarter bigger than their real size.";

    /// <summary>The 150% zoom.</summary>
    public static readonly string Zoom150 = "Show the pages one and a half times their real size.";

    /// <summary>The 200% zoom.</summary>
    public static readonly string Zoom200 = "Show the pages twice their real size.";

    /// <summary>The 400% zoom.</summary>
    public static readonly string Zoom400 = "Show the pages four times their real size.";

    /// <summary>The Save button and menu item.</summary>
    public static readonly string Save = $"Save your annotations and form entries to the file.{Shortcut}Ctrl+S";

    /// <summary>The Print button.</summary>
    public static readonly string Print = $"Choose the pages and settings, then print.{Shortcut}Ctrl+P";

    /// <summary>The Annotate toggle.</summary>
    public static readonly string Annotate = "Show the tools to highlight, underline, draw, and add notes and text.";

    /// <summary>The Fill and Sign toggle.</summary>
    public static readonly string FillSign = "Show the tools to fill in forms and sign the document.";

    /// <summary>The Find toggle.</summary>
    public static readonly string Find = $"Search for words in this document.{Shortcut}Ctrl+F";

    /// <summary>The Focus toggle.</summary>
    public static readonly string Focus = $"Show only the text, in one calm column, without the page layout.{Shortcut}Ctrl+4";

    /// <summary>The Read Aloud toggle.</summary>
    public static readonly string ReadAloud = $"Read the document aloud, starting from this page.{Shortcut}Ctrl+Shift+Y";

    /// <summary>The View menu button.</summary>
    public static readonly string ViewMenu = "Open the View menu to turn pages, change the page layout, read or present.";

    /// <summary>The rotate left menu item.</summary>
    public static readonly string RotateLeft = $"Turn the pages a quarter turn to the left.{Shortcut}Ctrl+Left";

    /// <summary>The rotate right menu item.</summary>
    public static readonly string RotateRight = $"Turn the pages a quarter turn to the right.{Shortcut}Ctrl+Right";

    /// <summary>The single page layout menu item.</summary>
    public static readonly string SinglePage = "Show one page in each row. Scroll down for the next page.";

    /// <summary>The dual page layout menu item.</summary>
    public static readonly string DualPage = "Show two pages side by side: pages 1 and 2, then 3 and 4. Next page moves to the next pair.";

    /// <summary>The dual page with cover layout menu item.</summary>
    public static readonly string DualPageCover = "Show the first page alone like a book cover, then pages side by side: 2 and 3, then 4 and 5.";

    /// <summary>The Read Mode menu item.</summary>
    public static readonly string ReadMode = $"Put the tool bars and sidebar away so the pages fill the window.{Shortcut}Ctrl+H";

    /// <summary>The Present menu item.</summary>
    public static readonly string Present = $"Show one page at a time on the full screen. Press Esc to stop.{Shortcut}Shift+F5";

    /// <summary>The caret navigation menu item.</summary>
    public static readonly string CaretNavigation = $"Move through the text with the arrow keys. Hold Shift to select text.{Shortcut}F7";

    /// <summary>The page by page menu item.</summary>
    public static readonly string PageByPage = "Show one page, or one pair of pages, at a time instead of scrolling through all the pages.";

    /// <summary>The Document menu button.</summary>
    public static readonly string DocumentMenu = "Open the Document menu to save a copy, reload, find the file or see its details.";

    /// <summary>The Save As menu item.</summary>
    public static readonly string SaveAs = $"Save a copy of the document with a new name or in a new place.{Shortcut}Ctrl+Shift+S";

    /// <summary>The Reload menu item.</summary>
    public static readonly string Reload = $"Load the file again from the disk.{Shortcut}F5";

    /// <summary>The Show in Folder menu item.</summary>
    public static readonly string ShowInFolder = "Open the folder that holds this file.";

    /// <summary>The Properties menu item.</summary>
    public static readonly string Properties = $"See details about this document, such as its title, author and size.{Shortcut}Alt+Enter";

    /// <summary>The Tools menu button.</summary>
    public static readonly string ToolsMenu = "Open the Tools menu to measure the page or recognise scanned text.";

    /// <summary>The Measure menu item.</summary>
    public static readonly string Measure = $"Measure distances, lengths and areas on the page.{Shortcut}Ctrl+Shift+M";

    /// <summary>The Recognise Text menu item.</summary>
    public static readonly string RecognizeText = "Find the words in scanned pages so you can search, select and hear them.";

    /// <summary>The language bar's Not Now button.</summary>
    public static readonly string CloseLanguageBar = "Hide this bar. Nothing on the pages has been changed, and any download in progress stops.";

    /// <summary>The language bar's recognise button.</summary>
    public static readonly string RecognizeInLanguage = "Find the words in this document in the chosen language. A language not on this computer downloads first and stays for next time.";

    /// <summary>The language bar's language list.</summary>
    public static readonly string DocumentLanguage = "Choose the language the scanned pages are written in.";

    /// <summary>The Preferences Stop Download button.</summary>
    public static readonly string StopLanguageDownload = "Stop the language download. Languages that already finished are kept.";

    /// <summary>A language's tick box in Preferences.</summary>
    public static readonly string UseLanguage = "Tick to find words in this language. Tick more than one for documents that mix languages.";

    /// <summary>A language's Download button in Preferences.</summary>
    public static readonly string DownloadLanguage = "Download this language and keep it on this computer, so words in it can be found.";

    /// <summary>A language's Remove button in Preferences.</summary>
    public static readonly string RemoveLanguage = "Delete this language from this computer to free space. You can download it again later.";

    /// <summary>The Recognise Text in Another Language menu item.</summary>
    public static readonly string RecognizeTextInLanguage = "Choose the language a scanned document is written in, then find its words. Other languages download in one click.";

    /// <summary>The Leave Read Mode button.</summary>
    public static readonly string LeaveReadMode = $"Bring back the tool bars and sidebar.{Shortcut}Esc or Ctrl+H";

    /// <summary>The button that keeps a measurement.</summary>
    public static readonly string MeasureKeep = "Keep the finished measurement on the page as an annotation.";

    /// <summary>The button that clears a measurement.</summary>
    public static readonly string MeasureClear = $"Clear the measurement you are making.{Shortcut}Esc";

    /// <summary>The button that closes the measuring tools.</summary>
    public static readonly string MeasureDone = "Put the measuring tools away.";

    /// <summary>The distance tool.</summary>
    public static readonly string Distance = "Click two points on the page to measure the distance between them.";

    /// <summary>The perimeter tool.</summary>
    public static readonly string Perimeter = "Click points along a path to measure its length. Double-click to finish.";

    /// <summary>The area tool.</summary>
    public static readonly string Area = "Click the corners of a shape to measure its area. Double-click to finish.";

    /// <summary>The measuring scale box.</summary>
    public static readonly string MeasureScale = "Type how the page relates to real size, such as 1 cm = 2 m or 1:100.";

    /// <summary>The annotation Undo button.</summary>
    public static readonly string Undo = $"Remove the last annotation you added.{Shortcut}Ctrl+Z";

    /// <summary>The button that closes the annotation tools.</summary>
    public static readonly string AnnotateDone = "Put the annotation tools away.";

    /// <summary>The select tool.</summary>
    public static readonly string SelectTool = "Select text, or click an annotation to pick it.";

    /// <summary>The highlight tool.</summary>
    public static readonly string HighlightTool = "Select text to highlight it.";

    /// <summary>The underline tool.</summary>
    public static readonly string UnderlineTool = "Select text to underline it.";

    /// <summary>The strike out tool.</summary>
    public static readonly string StrikeTool = "Select text to draw a line through it.";

    /// <summary>The draw tool.</summary>
    public static readonly string DrawTool = "Drag on the page to draw freehand.";

    /// <summary>The note tool.</summary>
    public static readonly string NoteTool = "Click on the page to add a sticky note.";

    /// <summary>The text tool.</summary>
    public static readonly string TextTool = "Click on the page to write text on it.";

    /// <summary>The shape button.</summary>
    public static readonly string Shape = "Choose a shape, then drag on the page to draw it.";

    /// <summary>The rectangle shape.</summary>
    public static readonly string Rectangle = "Draw rectangles. Drag on the page to draw one.";

    /// <summary>The ellipse shape.</summary>
    public static readonly string Ellipse = "Draw ovals and circles. Drag on the page to draw one.";

    /// <summary>The arrow shape.</summary>
    public static readonly string Arrow = "Draw arrows. Drag on the page from the tail to the point.";

    /// <summary>The line shape.</summary>
    public static readonly string Line = "Draw straight lines. Drag on the page to draw one.";

    /// <summary>The stamp button.</summary>
    public static readonly string Stamp = "Choose a stamp, then click on the page to place it.";

    /// <summary>The Approved stamp.</summary>
    public static readonly string StampApproved = "Use the Approved stamp. Click on the page to place it.";

    /// <summary>The Reviewed stamp.</summary>
    public static readonly string StampReviewed = "Use the Reviewed stamp. Click on the page to place it.";

    /// <summary>The Draft stamp.</summary>
    public static readonly string StampDraft = "Use the Draft stamp. Click on the page to place it.";

    /// <summary>The Confidential stamp.</summary>
    public static readonly string StampConfidential = "Use the Confidential stamp. Click on the page to place it.";

    /// <summary>The Final stamp.</summary>
    public static readonly string StampFinal = "Use the Final stamp. Click on the page to place it.";

    /// <summary>The Not Approved stamp.</summary>
    public static readonly string StampNotApproved = "Use the Not Approved stamp. Click on the page to place it.";

    /// <summary>The annotation colour button.</summary>
    public static readonly string AnnotationColour = "Choose the colour for new highlights, lines and notes, and for the annotation you picked.";

    /// <summary>The yellow annotation colour.</summary>
    public static readonly string Yellow = "Use yellow for new annotations and the one you picked.";

    /// <summary>The green annotation colour.</summary>
    public static readonly string Green = "Use green for new annotations and the one you picked.";

    /// <summary>The blue annotation colour.</summary>
    public static readonly string Blue = "Use blue for new annotations and the one you picked.";

    /// <summary>The red annotation colour.</summary>
    public static readonly string Red = "Use red for new annotations and the one you picked.";

    /// <summary>The button that closes the Fill and Sign tools.</summary>
    public static readonly string FillSignDone = "Put the Fill & Sign tools away.";

    /// <summary>The Draw Signature button.</summary>
    public static readonly string DrawSignature = "Draw your signature, then drag on the page where it should go.";

    /// <summary>The Type Signature button.</summary>
    public static readonly string TypeSignature = "Type your name as a signature, then click on the page where it should go.";

    /// <summary>The Sign with Certificate button.</summary>
    public static readonly string CertificateSign = "Sign a copy of the document with your digital certificate, a .p12 or .pfx file.";

    /// <summary>The Check Signatures button.</summary>
    public static readonly string CheckSignatures = "Check who signed this document and whether it changed after signing. This may go online.";

    /// <summary>The button that stops text recognition.</summary>
    public static readonly string StopRecognition = "Stop finding the words in scanned pages.";

    /// <summary>The button that closes Read Aloud.</summary>
    public static readonly string CloseReadAloud = $"Stop reading and close the Read Aloud bar.{Shortcut}Ctrl+Shift+Y";

    /// <summary>The previous sentence button.</summary>
    public static readonly string PreviousSentence = "Go back one sentence and read it again.";

    /// <summary>The Play button.</summary>
    public static readonly string Play = "Carry on reading from where it stopped.";

    /// <summary>The Pause button.</summary>
    public static readonly string Pause = "Pause reading. Your place is kept.";

    /// <summary>The next sentence button.</summary>
    public static readonly string NextSentence = "Skip to the next sentence.";

    /// <summary>The voice list.</summary>
    public static readonly string Voice = "Choose the voice that reads aloud.";

    /// <summary>The speed list.</summary>
    public static readonly string Speed = "Choose how fast the voice reads.";

    /// <summary>The Read Aloud options button.</summary>
    public static readonly string ReadAloudOptions = "Choose how the text being read is marked on the page.";

    /// <summary>The check box that marks each word as it is read.</summary>
    public static readonly string WordMark = "Mark each word as it is read aloud.";

    /// <summary>The check box that dims text away from what is read.</summary>
    public static readonly string FocusBand = "Dim the text away from the part being read aloud.";

    /// <summary>The Download Voice button.</summary>
    public static readonly string DownloadVoice = "Download this voice so it can read aloud on this computer.";

    /// <summary>A button that hides a message.</summary>
    public static readonly string DismissMessage = "Hide this message.";

    /// <summary>The Reload button on the changed file bar.</summary>
    public static readonly string ReloadChanged = "Load the changed file from the disk.";

    /// <summary>The Dismiss button on the changed file bar.</summary>
    public static readonly string DismissReload = "Keep what is on screen now and hide this message.";

    /// <summary>The find box.</summary>
    public static readonly string SearchBox = "Type the words to find. Press Enter for the next result or Shift+Enter for the one before.";

    /// <summary>The match case option.</summary>
    public static readonly string MatchCase = "Only find text with the same capital and small letters.";

    /// <summary>The whole words option.</summary>
    public static readonly string WholeWords = "Only find whole words, not parts of longer words.";

    /// <summary>The previous result button.</summary>
    public static readonly string PreviousResult = $"Go to the previous result.{Shortcut}Shift+F3";

    /// <summary>The next result button.</summary>
    public static readonly string NextResult = $"Go to the next result.{Shortcut}F3";

    /// <summary>The button that closes the find bar.</summary>
    public static readonly string CloseFind = $"Close the find bar.{Shortcut}Esc";

    /// <summary>The thumbnails sidebar tab.</summary>
    public static readonly string Thumbnails = "Show a small picture of each page. Choose one to go to that page.";

    /// <summary>The outline sidebar tab.</summary>
    public static readonly string Outline = "Show the document's headings. Choose one to go to it.";

    /// <summary>The search results sidebar tab.</summary>
    public static readonly string SearchResults = "Show every place your search found. Choose one to go to it.";

    /// <summary>The annotations sidebar tab.</summary>
    public static readonly string Annotations = "Show the highlights, notes and other annotations in this document.";

    /// <summary>The attachments sidebar tab.</summary>
    public static readonly string Attachments = "Show the files attached to this document.";

    /// <summary>The layers sidebar tab.</summary>
    public static readonly string Layers = "Show the document's layers so you can hide or show them.";

    /// <summary>The Save Attachment button.</summary>
    public static readonly string SaveAttachment = "Save the chosen attachment as a file.";

    /// <summary>The splitter beside the sidebar.</summary>
    public static readonly string SidebarSplitter = "Drag to make the sidebar wider or narrower.";

    /// <summary>A layer's check box.</summary>
    public static readonly string Layer = "Show or hide this layer on screen. The file does not change.";

    /// <summary>The form field editor.</summary>
    public static readonly string FormField = "Type into this form field. Press Tab for the next field or Esc to cancel.";

    /// <summary>The document password box.</summary>
    public static readonly string DocumentPassword = "Type the document's password, then press Enter.";

    /// <summary>The Unlock button.</summary>
    public static readonly string Unlock = "Open the document with this password.";

    /// <summary>The Back to Pages button in Focus Mode.</summary>
    public static readonly string BackToPages = $"Leave Focus Mode and show the pages again.{Shortcut}Ctrl+4";

    /// <summary>The smaller text button in Focus Mode.</summary>
    public static readonly string SmallerText = "Make the text smaller.";

    /// <summary>The larger text button in Focus Mode.</summary>
    public static readonly string LargerText = "Make the text larger.";

    /// <summary>The Text Settings button in Focus Mode.</summary>
    public static readonly string TextSettings = "Change the text size, spacing, width, colour and typeface.";

    /// <summary>The text size slider.</summary>
    public static readonly string TextSize = "Change the size of the text.";

    /// <summary>The line spacing slider.</summary>
    public static readonly string LineSpacing = "Change the space between lines.";

    /// <summary>The paragraph spacing slider.</summary>
    public static readonly string ParagraphSpacing = "Change the space between paragraphs.";

    /// <summary>The text width slider.</summary>
    public static readonly string TextWidth = "Change how wide the column of text is.";

    /// <summary>The Focus Mode page colour list.</summary>
    public static readonly string FocusPageColour = "Choose the colour behind the text.";

    /// <summary>The typeface list.</summary>
    public static readonly string Typeface = "Choose the typeface for the text.";

    /// <summary>The tab finder button.</summary>
    public static readonly string TabFinder = $"Find an open tab by part of its name, title or folder.{Shortcut}Ctrl+Shift+A";

    /// <summary>The tab finder box.</summary>
    public static readonly string TabFinderBox = "Type part of a name, title or folder. Press Enter to go to the first tab found.";

    /// <summary>The Open button and menu item.</summary>
    public static readonly string Open = $"Choose PDF files to open.{Shortcut}Ctrl+O";

    /// <summary>The tabs and settings menu button.</summary>
    public static readonly string TabsMenu = "Open the menu for tabs and settings, such as Preferences, page colour and closed tabs.";

    /// <summary>The Reopen Closed Tabs menu item.</summary>
    public static readonly string ReopenClosedTabs = $"Open the tabs you closed again.{Shortcut}Ctrl+Shift+T";

    /// <summary>The Search in Folder menu item.</summary>
    public static readonly string SearchFolder = $"Search for words in every PDF in a folder.{Shortcut}Ctrl+Shift+F";

    /// <summary>The comfort page colour menu item.</summary>
    public static readonly string ComfortPageColour = $"Show the pages in a softer colour that is easier on the eyes.{Shortcut}Ctrl+I";

    /// <summary>The Preferences menu item.</summary>
    public static readonly string Preferences = $"Change how the app looks and works.{Shortcut}Ctrl+Comma";

    /// <summary>The Close All Tabs menu item.</summary>
    public static readonly string CloseAllTabs = "Close every open tab.";

    /// <summary>The close button on a tab.</summary>
    public static readonly string CloseTab = $"Close this tab.{Shortcut}Ctrl+W";

    /// <summary>The colour scheme list.</summary>
    public static readonly string ColourScheme = "Choose the colours of the app, such as Calm, Dark or High contrast.";

    /// <summary>The PDF page colour list.</summary>
    public static readonly string PageColour = "Choose the colour of the pages on screen. Printed pages keep their own colours.";

    /// <summary>The tool bar style list.</summary>
    public static readonly string ToolbarStyle = "Choose whether tool bar buttons show their names beside their icons.";

    /// <summary>The file change list.</summary>
    public static readonly string FileChange = "Choose what happens when an open file is changed by another program.";

    /// <summary>The motion list.</summary>
    public static readonly string Motion = "Choose whether the app uses movement, such as smooth scrolling.";

    /// <summary>The text cursor list.</summary>
    public static readonly string TextCursor = "Choose whether the text cursor stays steady or blinks.";

    /// <summary>The interface text size list.</summary>
    public static readonly string InterfaceTextSize = "Choose the size of the text in the app's buttons, menus and lists.";

    /// <summary>The opening zoom list.</summary>
    public static readonly string OpeningZoom = "Choose the zoom for new documents. Fit page shows the whole page. Fit width fills the width.";

    /// <summary>The Read Aloud voice service list.</summary>
    public static readonly string SpeechEngine = "Choose which voice service reads aloud.";

    /// <summary>The Azure Speech key box.</summary>
    public static readonly string AzureKey = "Type a key from your Azure Speech resource. Azure voices need it.";

    /// <summary>The Azure region box.</summary>
    public static readonly string AzureRegion = "Type the region of your Azure Speech resource, such as uksouth.";

    /// <summary>The timestamp server box.</summary>
    public static readonly string TimestampServer = "Optional. Type the address of a server that records a trusted time when you sign.";

    /// <summary>A Close button on a window that has nothing to save.</summary>
    public static readonly string CloseWindow = $"Close this window.{Shortcut}Esc";

    /// <summary>The Close button on Preferences.</summary>
    public static readonly string ClosePreferences = $"Close Preferences. Your changes are already kept.{Shortcut}Esc";

    /// <summary>A Cancel button.</summary>
    public static readonly string Cancel = $"Close this window without making any changes.{Shortcut}Esc";

    /// <summary>The print destination list.</summary>
    public static readonly string PrintDestination = "Choose a printer, save as a PDF file or use the system print dialog.";

    /// <summary>The pages to print list.</summary>
    public static readonly string PrintPages = "Choose which pages to print: all, the current page or your own list.";

    /// <summary>The page range box.</summary>
    public static readonly string PageRange = "Type the pages to print, such as 1-3, 7.";

    /// <summary>The copies box.</summary>
    public static readonly string Copies = "Choose how many copies to print.";

    /// <summary>The print colour list.</summary>
    public static readonly string PrintColour = "Choose to print in colour or in black and white.";

    /// <summary>The two-sided printing check box.</summary>
    public static readonly string TwoSided = "Print on both sides of the paper.";

    /// <summary>The page turning edge list.</summary>
    public static readonly string TurnPages = "Choose which edge the paper turns on when printing on both sides.";

    /// <summary>The print layout list.</summary>
    public static readonly string PrintLayout = "Choose normal pages, a folded booklet or a poster made from several sheets.";

    /// <summary>The pages per sheet list.</summary>
    public static readonly string PagesPerSheet = "Choose how many pages to fit on each sheet of paper.";

    /// <summary>The paper size list.</summary>
    public static readonly string PaperSize = "Choose the size of the paper.";

    /// <summary>The print annotations check box.</summary>
    public static readonly string PrintAnnotations = "Print the highlights, notes and other annotations too.";

    /// <summary>The print button in the print window.</summary>
    public static readonly string PrintNext = "Go on to print with these settings.";

    /// <summary>The system print dialog button.</summary>
    public static readonly string SystemPrintDialog = $"Print with your computer's own print window instead.{Shortcut}Ctrl+Shift+P";

    /// <summary>The certificate file box.</summary>
    public static readonly string CertificateFile = "Type where your certificate file is, a .p12 or .pfx file.";

    /// <summary>The button that chooses a certificate file.</summary>
    public static readonly string ChooseCertificate = "Choose your certificate file from a folder.";

    /// <summary>The certificate password box.</summary>
    public static readonly string CertificatePassword = "Type the password for your certificate.";

    /// <summary>The signing reason box.</summary>
    public static readonly string SigningReason = "Optional. Type why you are signing, such as Approved.";

    /// <summary>The signing location box.</summary>
    public static readonly string SigningLocation = "Optional. Type where you are signing.";

    /// <summary>The Sign button.</summary>
    public static readonly string Sign = "Choose where to save the signed copy, then sign it.";

    /// <summary>The confirm button in a question window.</summary>
    public static readonly string Confirm = "Go ahead with what this window asks about.";

    /// <summary>The text box in a question window.</summary>
    public static readonly string PromptInput = "Type your answer here.";

    /// <summary>The confirm button in a question window with a text box.</summary>
    public static readonly string PromptConfirm = "Use what you typed.";

    /// <summary>The choose folder button.</summary>
    public static readonly string ChooseFolder = "Choose the folder to search.";

    /// <summary>The folder box.</summary>
    public static readonly string FolderBox = "Type the folder to search.";

    /// <summary>The words to find in a folder.</summary>
    public static readonly string FolderQuery = "Type the words to find, then press Enter.";

    /// <summary>The Search button in folder search.</summary>
    public static readonly string SearchFolderStart = "Search every PDF in the folder for the words.";

    /// <summary>The Stop button in folder search.</summary>
    public static readonly string StopSearch = "Stop searching the folder.";

    /// <summary>The include subfolders check box.</summary>
    public static readonly string Subfolders = "Also search the folders inside this folder.";

    /// <summary>The pages, read out by screen readers only so hovering the pages shows no tooltip.</summary>
    public static readonly string Pages = "The document's pages. Page Up and Page Down move a page at a time. F7 turns on a text cursor for the arrow keys. The value is the current page's text.";

    /// <summary>Joins an explanation to its shortcut.</summary>
    private const string Shortcut = " Shortcut: ";
}
