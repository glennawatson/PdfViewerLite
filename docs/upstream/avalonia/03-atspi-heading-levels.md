# Heading levels and list positions

Avalonia 12.1.3, Linux AT-SPI. The bridge omits heading levels and list positions. Screen readers cannot jump by heading or announce an item's position. Windows UI Automation receives these values.

Set `AutomationProperties.HeadingLevel="2"` on a TextBlock. Its AT-SPI role remains `label`, with no `level` attribute. `PositionInSet` and `SizeOfSet` are also absent.

The bridge should expose the heading role and `level`, `posinset` and `setsize` attributes. The app puts this information in Linux help text until then.
