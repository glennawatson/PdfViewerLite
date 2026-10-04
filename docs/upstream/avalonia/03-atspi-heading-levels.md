# Heading levels and list positions

Avalonia 12.1.3 drops heading levels and list positions on Linux AT-SPI. Windows UI Automation receives heading levels. The list position properties have no callers in Avalonia's platform bridges.

Set `AutomationProperties.HeadingLevel="2"` on a TextBlock. Its AT-SPI role remains `label`, with no `level` attribute. `PositionInSet` and `SizeOfSet` are also absent.

The bridge should expose the heading role and `level` attribute. List positions also need peer support for `posinset` and `setsize`. The app includes this information in Linux help text.

An independent app confirms the missing bus metadata. A normal label provides the control case. The [AT-SPI handler](https://github.com/AvaloniaUI/Avalonia/blob/main/src/Avalonia.FreeDesktop.AtSpi/Handlers/AtSpiAccessibleHandler.cs) omits these values. This check does not measure spoken screen reader output.
