# Avalonia issue: buttons with icon or panel content are named after the content's type

`ContentControlAutomationPeer` falls back to the content's `ToString()` when the content is not a string. A button
holding an icon, or a `StackPanel` with an icon and a label, is announced as "Avalonia.Controls.Shapes.Path" or
"Avalonia.Controls.StackPanel". Using the text of a `TextBlock` inside the content, or the tooltip, would be a
better fallback. PdfViewerLite sets `AutomationProperties.Name` on every such button, and `AccessibilityTests`
rejects names that start with `Avalonia.`.
## Version

Avalonia 12.1.3, `ContentControlAutomationPeer` in `Avalonia.Controls`. Seen with Orca on Linux and in headless
automation peer tests.

## Reproduction

```xml
<Button>
  <StackPanel Orientation="Horizontal">
    <Path Data="M0,0 L10,10" />
    <TextBlock Text="Save" />
  </StackPanel>
</Button>
```

`ControlAutomationPeer.CreatePeerForElement(button).GetName()` returns "Avalonia.Controls.StackPanel", which screen
readers read out.
