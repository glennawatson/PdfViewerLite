# AT-SPI startup race

Avalonia 12.1.3, Linux X11. Screen readers can miss the main window when startup takes longer than 100 ms. Later dialogs appear normally. This occurs in JIT and Native AOT builds.

The bridge starts before the UI loop. Its idle wait can time out. It then reads tracked windows on a worker thread. Creating an automation peer there throws, so the window is skipped. The tracked-window list is also accessed across threads without a lock.

## Reproduce

1. Delay main-window setup by about 150 ms in `OnFrameworkInitializationCompleted`.
2. Start an accessibility bus and enable access support.
3. Run the app and inspect its AT-SPI tree. The application has no window child.

`scripts/check-screen-reader.sh` automates the bus and tree checks. Debug logs show the idle timeout and failed peer lookup.

## Fix and workaround

The bridge should register tracked windows on the UI thread. Window registration must happen once even during server startup.

The app posts Linux startup at `DispatcherPriority.ApplicationIdle`. The bridge's `ContextIdle` wait completes first. See [upstream issues](https://github.com/AvaloniaUI/Avalonia/issues).
