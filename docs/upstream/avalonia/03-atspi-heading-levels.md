# Avalonia issue: heading levels and list positions are not passed to AT-SPI

`AutomationProperties.HeadingLevel`, `PositionInSet` and `SizeOfSet` reach UI Automation on Windows, but the AT-SPI
bridge (`AtSpiNode.ToAtSpiRole` and `GetAttributesAsync` in `Avalonia.FreeDesktop.AtSpi` 12.1.3) never reports
the `heading` role or a `level` attribute, and has no list position attributes. Orca therefore cannot jump between
headings or announce "item 2 of 5". A fix would map a non-zero heading level to `ATSPI_ROLE_HEADING` with a `level`
attribute, and add `posinset` and `setsize` attributes. PdfViewerLite works around it on Linux by putting the role
into the help text, which AT-SPI exposes as the description ("Heading level 2", "List item 2 of 5").

## Version

Avalonia 12.1.3, `Avalonia.FreeDesktop.AtSpi`.

## Reproduction

Set `AutomationProperties.HeadingLevel="2"` on a `TextBlock`, or `PositionInSet`/`SizeOfSet` on list items, and walk
the AT-SPI tree: the role stays "label" and no `level`, `posinset` or `setsize` attributes appear, so Orca cannot jump
between headings or say "item 2 of 5".
