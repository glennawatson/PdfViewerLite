# Comfort design rules

PdfViewerLite follows a set of design rules for people with ADHD and autism, adapted from the "Eleven" rules. The
aim is a viewer that stays quiet, predictable and gentle on the eyes. These rules apply to every new piece of UI.

## The rules, as they apply here

1. **Quiet by default.** Nothing moves, flashes, beeps or pops up unless you did something. The app has no sounds,
   no animations it starts by itself, no badges, and no "tips".
2. **Nothing moves unless you move it.** The layout only changes when you ask. Opening find does not switch the
   sidebar panel. The thumbnail list only scrolls when the current page leaves its view, and never while you are
   scrolling it yourself. Transitions are short (Fluent's ≤ 333 ms) and turn off completely with reduced motion.
3. **No flashing for more than 5 seconds.** The text cursor is steady by default. It blinks only if you, or the
   desktop's `CursorBlinkRate`, ask for it.
4. **One signal means one thing.** Each icon tint stands for one kind of action:
   - slate: navigation (back, forward, pages, fit, sidebar, find)
   - sage: add or open
   - sand: edit (rotate, annotate)
   - clay: remove or close

   Icon shapes differ too, so colour is never the only cue. The current search hit has an outline as well as a
   fill.
5. **Soft, not harsh.** There is no pure white and nothing saturated in Calm. Pages use a page tone, a paper and
   ink colour pair applied while rendering, instead of glaring white or an inverted night mode. Search hits are a
   soft sand fill. Selection is a soft slate fill.
6. **Readable contrast**, checked by `ColorSchemeContrastTests` for every built-in scheme:
   - text at least 7:1 (Calm stays below 9.5:1 so it does not glare)
   - inactive text at least 4.5:1
   - icons, borders and the accent at least 3:1
7. **Words, not just pictures.** The main tool bar actions have text beside their icons. Tooltips only add detail,
   such as the shortcut.
8. **Ask before anything you cannot easily undo.** Closing two or more tabs at once asks first. Cancel is the
   default button and Esc cancels. Tabs closed together reopen together with Ctrl+Shift+T.
9. **Messages wait for you.** Status messages and the "file changed" bar stay until you dismiss them. They use the
   header colour, not alarm colours.
10. **Choice, not one "calm" look.** Every comfort setting is in Preferences (Ctrl+,) and applies at once:

| Setting | Choices | Default |
|---|---|---|
| Colour scheme | Follow the desktop, Calm, High contrast, Dark, Light | Follow the desktop; Calm when there is none |
| Page colour | Match the colour scheme, White, Soft paper, Calm night, Dark | Match the colour scheme |
| Tool bar | Text beside icons, Icons only | Text beside icons |
| When a file changes on disk | Reload automatically, Show a Reload bar | Reload automatically |
| Motion | Follow the desktop (`AnimationDurationFactor`), Reduced, Normal | Follow the desktop |
| Text cursor | Follow the desktop (`CursorBlinkRate`), Steady, Blinking | Follow the desktop; steady when unset |
| Interface text size | Follow the desktop, 9 to 16 pt | Follow the desktop |

Ctrl+I switches between the comfort page colour and plain white pages.

## Checklist for new UI

- Does anything move, appear or change without the user asking? It should not.
- Does every icon tint match the action kind above, and is there a shape or text cue as well?
- Are the colours taken from the theme resources (`AppForeground`, `AppIconNav`, `AppHitBrush` and so on), never
  hard-coded?
- Do main actions have a text label, and do context menus show only what applies to the current selection?
- Is a destructive action confirmed, or can it be undone?
- Does a message stay until dismissed, without alarm colours?
- Does it still work with reduced motion, a steady caret, icons only and each colour scheme?

## Where it lives

- Schemes, tints and contrast maths: `src/PdfViewerLite.Core/Theming` (`ColorSchemes`, `ThemeResolver`,
  `ColorMath`).
- Page tones: `src/PdfViewerLite.Core/Rendering/PageTone.cs`. A per-channel lookup table runs on the render
  thread, with no allocation per tile.
- Applying a theme: `src/PdfViewerLite.App/Theming/DesktopThemeApplier.cs`. It sets resources and the Fluent
  palette, and adds the reduced motion, steady caret and icons-only styles.
- Settings: `AppSettings`. `AppServices.Theme` republishes the resolved theme when the settings or the desktop
  palette change.
