# Avalonia issues found by PdfViewerLite

Each file is written so it can be pasted into a new issue at https://github.com/AvaloniaUI/Avalonia/issues. All
were found with Avalonia 12.1.3 while making PdfViewerLite work with screen readers; each describes the workaround
the app uses until a fix ships.

| Note | Platform | Effect | Workaround in PdfViewerLite |
|---|---|---|---|
| [01-atspi-startup-race.md](01-atspi-startup-race.md) | Linux (X11, AT-SPI) | The main window never reaches screen readers when startup takes longer than 100 ms, which Native AOT builds hit every time | Startup is posted at `ApplicationIdle` on Linux so the bridge settles first |
| [02-content-type-names.md](02-content-type-names.md) | All | Buttons holding an icon or a panel are announced as "Avalonia.Controls.StackPanel" | Every such button sets `AutomationProperties.Name`; a test rejects type-name fallbacks |
| [03-atspi-heading-levels.md](03-atspi-heading-levels.md) | Linux (AT-SPI) | Heading levels and list positions never reach Orca | On Linux the help text (the AT-SPI description) says "Heading level 2" or "List item 2 of 5" |
