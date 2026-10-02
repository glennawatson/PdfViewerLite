# Framework access issues

These notes describe accessibility behaviour in Avalonia 12.1.3, its reproduction and the app workaround.

| Issue | Effect | App workaround |
|---|---|---|
| [AT-SPI startup](01-atspi-startup-race.md) | Linux screen readers miss the main window | Start at `ApplicationIdle` |
| [Button names](02-content-type-names.md) | Icons and panels are announced as type names | Set an accessible name |
| [Headings and lists](03-atspi-heading-levels.md) | Linux loses heading levels and list positions | Put them in help text |

Startup is tracked in [issue 22376](https://github.com/AvaloniaUI/Avalonia/issues/22376) and [PR 22379](https://github.com/AvaloniaUI/Avalonia/pull/22379). Button names are tracked in [issue 22377](https://github.com/AvaloniaUI/Avalonia/issues/22377) and [PR 22378](https://github.com/AvaloniaUI/Avalonia/pull/22378).

Linux zoom flicker has no isolated framework cause. See the [milestone goals](../../MILESTONES.md) for access checks.
