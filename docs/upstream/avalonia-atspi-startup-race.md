# Avalonia bug: main window missing from AT-SPI when startup is slower than 100 ms

Ready to file at https://github.com/AvaloniaUI/Avalonia/issues.

## Summary

On Linux (X11), the main window of an app never shows up in the AT-SPI tree when the UI thread is busy for more
than 100 ms after the platform starts, which happens with almost any real app. Orca and other screen readers then see
only an empty "Avalonia Application" node. Windows opened later, such as dialogs, are registered correctly.

## Version

- Avalonia 12.1.3 (`Avalonia.X11`, `Avalonia.FreeDesktop.AtSpi`), .NET 10.
- Seen with both a Native AOT build (fails every time) and a JIT build (fails or works depending on timing).

## Cause

`AvaloniaX11Platform.Initialize` ends with `X11AtSpiAccessibility.Initialize()`, which starts `InitializeAsync`:

1. `WaitForUiThreadSettleAsync` awaits
   `Dispatcher.UIThread.InvokeAsync(() => {}, DispatcherPriority.ContextIdle).GetTask().WaitAsync(100 ms)`.
   This runs inside `AppBuilder.SetupUnsafe`, before the dispatcher loop and before any `SynchronizationContext`
   is installed, so the await captures no context.
2. If the app's `Initialize` and `OnFrameworkInitializationCompleted` take longer than 100 ms, the wait times out
   (logged as "AT-SPI startup wait timed out before UI thread reached idle"). The rest of `InitializeAsync` then runs
   on a thread-pool thread.
3. `TryStartServerAsync` awaits `AtSpiServer.StartAsync()`, then loops over `_trackedWindows` and calls
   `TryGetWindowPeer`, which reads `window.InputRoot.FocusRoot` and creates the automation peer. Off the UI thread,
   this throws `InvalidOperationException: The calling thread cannot access this object because a different thread
   owns it`. The exception is caught and logged as "AT-SPI could not resolve window automation peer yet", and the
   window is skipped for good.
4. `X11Window.Show` only calls `AtSpiServer.AddWindow` when the server already exists. A window shown before the
   server finishes starting is therefore only ever added by the loop in step 3, and in this case that loop fails.
   Showing the window again does nothing, because `Window.ShowCore` returns early once `_shown` is set.

So the window is lost whenever it is shown after the 100 ms timeout but before `StartAsync` completes. A faster
startup (Native AOT) makes this the usual case.

`_trackedWindows` is also read and written from two threads without a lock.

## Reproduction

1. Make an app whose `OnFrameworkInitializationCompleted` sets `desktop.MainWindow` after about 150 ms of work
   (for example `Thread.Sleep(150)`), and publish it with Native AOT.
2. Start an accessibility bus (`at-spi-bus-launcher`) with `org.a11y.Status.IsEnabled` set to true, then run the app.
3. Walk the tree from `/org/a11y/atspi/accessible/root` on the app's a11y bus connection: the application node has no
   children, and the registry desktop does not list the app.
4. With `LogToTextWriter(Console.Error, LogEventLevel.Debug, "X11Platform")` the two messages from steps 2 and 3
   above appear.

`scripts/check-screen-reader.sh` in this repository automates steps 2 and 3.

## Suggested fix

Marshal the remainder of `InitializeAsync`, or at least the `_trackedWindows` loop in `TryStartServerAsync`, to
the UI thread with `Dispatcher.UIThread.InvokeAsync(...)`. Then make `X11Window.Show` and the loop agree under one
lock, or run both on the UI thread, so a window shown during `StartAsync` is added exactly once.

## Workaround in PdfViewerLite

`App.OnFrameworkInitializationCompleted` posts its startup at `DispatcherPriority.ApplicationIdle` on Linux. That
priority is lower than the `ContextIdle` wait, so the wait always completes first, on the UI thread, and every later
continuation stays there. The main window is then shown from that posted job.

## Related: buttons named after their content's type

`ContentControlAutomationPeer` falls back to the content's `ToString()` when the content is not a string. A button
holding an icon, or a `StackPanel` with an icon and a label, is announced as "Avalonia.Controls.Shapes.Path" or
"Avalonia.Controls.StackPanel". Using the text of a `TextBlock` inside the content, or the tooltip, would be a
better fallback. PdfViewerLite sets `AutomationProperties.Name` on every such button, and `AccessibilityTests`
rejects names that start with `Avalonia.`.

## Related: heading levels and list positions are not passed to AT-SPI

`AutomationProperties.HeadingLevel`, `PositionInSet` and `SizeOfSet` reach UI Automation on Windows, but the AT-SPI
bridge (`AtSpiNode.ToAtSpiRole` and `GetAttributesAsync` in `Avalonia.FreeDesktop.AtSpi` 12.1.3) never reports
the `heading` role or a `level` attribute, and has no list position attributes. Orca therefore cannot jump between
headings or announce "item 2 of 5". A fix would map a non-zero heading level to `ATSPI_ROLE_HEADING` with a `level`
attribute, and add `posinset` and `setsize` attributes. PdfViewerLite works around it on Linux by putting the role
into the help text, which AT-SPI exposes as the description ("Heading level 2", "List item 2 of 5").
