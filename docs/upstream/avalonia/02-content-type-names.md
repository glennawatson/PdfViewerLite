# Button names

Avalonia 12.1.3. `ContentControlAutomationPeer` uses `ToString()` when content is not a string. A button with an icon and label can be announced as `Avalonia.Controls.StackPanel`.

Reproduce with a Button containing a StackPanel and TextBlock. `ControlAutomationPeer.CreatePeerForElement(button).GetName()` returns the panel's type name.

The app sets `AutomationProperties.Name` on these buttons. Access tests reject type-name fallbacks. The framework could use the visible label or tooltip instead.
