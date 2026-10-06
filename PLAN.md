# Hungarian Desktop Learning Widget — Build Plan

**Status:** Expanded 500-word build complete.
**Date:** 5 October 2026

The first plan targeted WinUI 3. This PC has no Visual Studio/Windows SDK workload, so the first build uses native WPF on .NET 10 to keep the floating desktop experience and make a runnable app without installing a multi-gigabyte IDE. The view, data model, audio, and interaction requirements stay the same.

## Product goal

A small, attractive Hungarian learning card that stays available on the Windows desktop. It should be worthwhile to glance at, and offer quick ways to hear pronunciation, move to fresh material, or take a short quiz without opening a separate app window.

## Main interaction

1. Each card always shows one Hungarian word, its English meaning, an example sentence using the word, and that sentence's English translation.
2. Separate speaker buttons play the Hungarian word or its example sentence. Audio never starts automatically.
3. **Reroll** at the top replaces the current word with a different word from the remaining unseen cards in enabled categories.
4. **Quiz** expands the same window vertically and runs a short, three-question test. Questions use cards already encountered and alternate between Hungarian-to-English and English-to-Hungarian. Answers are selected in the widget; the result shows the score and corrections.
5. The card remains in place until the user rerolls or the app starts a new daily cycle. It will not change on a timer while the user is reading it.

The default experience has no required sign-in, notifications, points, or forced study session. A five-minute session is a useful target, while casual glances and rerolls remain available at any time.

## No-repeat and review behavior

Reroll should not show a word already seen in the current daily rotation. If the unseen pool runs out, the widget should say so and offer a deliberate new cycle. A separate spaced-review queue can bring old words back on later days or in quizzes; those are intentional review items, not accidental reroll duplicates.

Each category can be enabled or disabled from the **⋯** menu. All categories start enabled. Rerolls and quizzes use the selected categories; no word repeats within the active rotation. Deliberate new cycles and spaced reviews can bring words back later.

## Visual design

- A compact, rounded, Fluent-inspired card with generous spacing and clear contrast.
- Top row: a visible draggable handle, **Reroll**, and **Quiz**.
- Main card: a large Hungarian word with its English meaning, followed by a Hungarian example sentence and its translation; each has a speaker control.
- **⋯ → Categories** holds category checkboxes. All categories are on by default; category choices persist locally and filter both rerolls and quizzes.
- Quiz mode grows the same window downward; it does not open another page or window.
- Support light and dark appearance, dragging from the dotted title handle, and remember the widget’s screen position.
- Always-on-top by default, with a simple option to turn it off. Keep a normal close control and a way to show it again from the system tray.

## Recommended technology

| Area | Choice | Reason |
| --- | --- | --- |
| Language/runtime | C# on .NET 10 LTS | Maintained LTS runtime; Microsoft lists support through November 2028. |
| UI | WPF with XAML on .NET 10 | Native Windows desktop UI that can be built with the SDK available on this PC; avoids a browser-based UI. |
| Desktop window | WPF `Window` | Supports a compact custom window, `Topmost`, resizing, and an inline expanded quiz without the Widgets Board. |
| Local progress | SQLite via `Microsoft.Data.Sqlite` | Keeps seen-card history, quiz results, review dates, and preferences locally and reliably. |
| Content format | Versioned JSON content pack plus audio files | Separates vocabulary additions from app code and makes future packs easy to add. |
| Audio | Pre-generated, bundled Hungarian WAV clips using Piper | Works offline and does not depend on which Windows speech voices are installed. The Hungarian voice model is used only during content preparation, not at runtime. Keep model attribution/license notes with the audio pack. |
| Install/startup | Self-contained single-file Windows executable; optional per-user Start with Windows setting | No separate runtime installation or administrator account for normal use. A per-user startup entry is created only when enabled. |
| Network/account | None at runtime | Cards, progress, and audio remain on the device. |

The app will store progress under the user's local application data directory and embed its initial content/audio assets in the executable. The development-only .NET SDK and Piper model stay outside the shipped app.

## App structure

```text
WPF desktop window
├── Card view and quiz view
├── Learning controller
│   ├── unseen-card rotation
│   └── spaced-review schedule
├── Local content pack (JSON + Hungarian audio)
└── SQLite store (progress + settings)
```

Suggested card fields: stable ID, Hungarian text, English meaning, card type (word or sentence), level/topic tags, linked target vocabulary, optional short usage note, and audio-file ID. Progress stores first/last-seen time, current rotation, quiz outcomes, and next review date. Keep content separate from progress so content updates do not erase learning history.

## Learning content

- Start with a broad beginner pack of 500 word cards across practical categories. Every card pairs its headword and English meaning with a short natural sentence containing that word and the sentence translation.
- Provide distinct pronunciation controls for the isolated word and complete example sentence.
- Review translations and usage carefully; preserve Hungarian accents and punctuation.
- Include an audio clip for every card in the first pack. Expand through additional content packs after the interaction feels right.

## Build sequence and delivery

1. Confirm the product rules and select a Windows-native desktop stack.
2. Build the compact floating WPF window with light/dark themes, topmost toggle, saved position, and tray controls.
3. Add bilingual cards, offline audio, a no-repeat rotation, local progress, and spaced review.
4. Add the inline three-question quiz with feedback and review scheduling.
5. Prepare 500 word cards across 18 categories, each with a linked sentence, translations, and audio for the word and sentence.
6. Package as a self-contained Windows x64 executable with optional per-user startup and run instructions.

## Definition of done

- The widget is visible over other windows and remains easy to move, hide, and restore.
- Each card shows a word, its meaning, an example sentence using it, and the sentence translation; separate controls play the word or sentence pronunciation offline.
- Reroll never repeats a card within the current rotation; the pool-exhausted state is clear.
- Quiz expands in place, uses already-seen material, and shows a useful result.
- Progress survives closing and reopening the app, and the app needs no account or separate browser/app window for study.

## Assumptions and open choices

- The current workspace machine is Windows build 26200, so this plan targets a modern Windows 11 desktop. Confirm the actual target PC before selecting the final minimum OS.
- Recommended no-repeat rule: no repeats within one daily rotation, with deliberate spaced review later.
- Recommended answer format: tap-to-answer multiple choice for the first small quiz; typed answers can be considered after the compact interaction is established.
- Recommended startup: offer Start with Windows as an explicit setting rather than silently enabling it.

## Implemented in the first build

- Windows 11 WPF widget with light and dark themes, always-on-top setting, drag positioning, and system tray hide/restore.
- 500 word cards across 18 practical categories, each paired with a sentence and English translations for both, with local synthesized Hungarian audio for both pronunciations.
- A visible dotted title handle for moving the borderless widget by dragging.
- 18 topic filters under the three-dot menu, all enabled on first launch and saved locally when changed.
- Persistent no-repeat rotation, a deliberate new-rotation action, a short inline multiple-choice quiz, and spaced review.
- Local SQLite progress and preferences. No account or network access is needed while studying.
- Self-contained .NET 10 Windows x64 publish; the packaged app runs without a separate .NET runtime install.

## Microsoft references

- [WPF overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)
- [.NET releases and support](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)
- [Piper voice catalog](https://github.com/rhasspy/piper/blob/master/VOICES.md)
