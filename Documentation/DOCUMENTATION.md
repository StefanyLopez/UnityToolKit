# Quick Scene Access

Package `com.steff.quicksceneaccess`

Quick Scene Access is an Editor tool for finding and opening the scenes of a Unity project without digging through the Project window. It has three parts that share the same data:

- **A window** with search, favorites, recents, color labels, grouping, scene sets and build info.
- **A Scene view overlay** ("Quick Scenes") with a scene dropdown and one-click buttons for your favorite scenes and scene sets. It reorganizes itself depending on where you dock it.
- **A quick search popup** opened with a keyboard shortcut.

> **Status:** requires Unity 6 (6000.0 or newer), developed on Unity 6.3 LTS. The tool is Editor-only and adds no runtime code to your game.

---

## Contents

1. [Getting started](#getting-started)
2. [The window](#the-window)
3. [Colors and grouping](#colors-and-grouping)
4. [The Scene view overlay](#the-scene-view-overlay)
5. [Scene sets](#scene-sets)
6. [Quick search](#quick-search)
7. [Play from here](#play-from-here)
8. [Unsaved changes and safety](#unsaved-changes-and-safety)
9. [Where your data is stored](#where-your-data-is-stored)
10. [How the list stays up to date](#how-the-list-stays-up-to-date)
11. [Architecture](#architecture)
12. [Customizing](#customizing)
13. [Known limitations](#known-limitations)
14. [Troubleshooting](#troubleshooting)
15. [Manual test checklist](#manual-test-checklist)

---

## Getting started

| What | How |
|---|---|
| **Window** | `Window > Quick Scene Access`. It can be docked like any other editor window; the minimum size is 330 x 260 px. |
| **Overlay** | Open the Scene view and look for the "Quick Scenes" toolbar at the bottom. If it is not visible, press the `` ` `` key with the cursor over the Scene view, open the Overlays menu, and enable **Quick Scenes**. Overlays can be dragged to any edge of the Scene view. |
| **Quick search** | **Ctrl+Shift+O** (**Cmd+Shift+O** on macOS), or `Window > Quick Scene Search`. |

---

## The window

### Layout

| Area | What it contains |
|---|---|
| Toolbar | Search field, **Group** menu, **Sets** button, **Save** menu, Refresh button |
| Banner | Appears only in Play Mode: "Scene switching is disabled in Play Mode." |
| List | Scenes, organized in sections |
| Status bar | Scene count, scenes in build, an orange `▶ SceneName ✕` button while [Play from here](#play-from-here) is on, and the **Build Profiles** button |

### Sections

The list is built from these sections, in order:

1. **Favorites**: scenes you starred, in the order you starred them.
2. **Recent**: the last scenes you opened through the tool (up to 5 shown, favorites excluded). Hidden while a search is active.
3. **All scenes**: every scene under `Assets/`, sorted by folder and then name. The "All scenes" header only appears when Favorites or Recent are visible.

The **Group** menu changes how the main list is organized (see [Colors and grouping](#colors-and-grouping)). A scene can appear in more than one section (for example in Favorites and in the full list). This is intentional.

### Scene rows

Each row shows a color dot, the scene icon, its name, and its folder. On the right:

| Element | Meaning |
|---|---|
| Color dot and tint | The color label of the scene, also shown as a soft tint across the row (none if unset) |
| `▶` after the name | The scene is the [Play from here](#play-from-here) start scene |
| **Active** chip | The currently active scene (the row is also highlighted and bold) |
| **Loaded** chip | Loaded additively alongside the active scene |
| `#2` chip | The scene is enabled in the Build Settings scene list, with that build index |
| `off` chip | The scene is in the Build Settings list but disabled |
| Star | Click to add or remove it from favorites (visible on hover, always visible when starred) |

Hover a row for a tooltip with the full asset path and the available shortcuts.

### Searching

- Type part of a scene name, folder or color name. Words are separated by spaces and **every word must match** the name, the folder or the color (case-insensitive). Example: `lvl menu`, or `blue menu`.
- Results are ordered: names that start with what you typed, then names containing all words, then matches through the folder or color.
- While searching, collapsed sections are shown expanded so no match is hidden.
- When nothing matches, an empty-state message is shown.

### Mouse and keyboard

| Action | Result |
|---|---|
| Click a row | Open the scene (replaces the current scene) |
| Shift + Click | Open additively (keeps the current scene loaded) |
| Click the active scene | Pings it in the Project window |
| Click the star | Toggle favorite (does not open the scene) |
| Click a section header | Collapse or expand that section |
| Right-click a row | Menu: Open, Open Additively, Add/Remove from Favorites, Color, Sets, Play From This Scene, Ping in Project, Show in Explorer / Reveal in Finder |
| Enter in the search field | Open the first scene in the list |
| Shift + Enter | Open the first scene additively |
| Esc in the search field | Clear the search |

### Status bar

Shows `N scenes`, or `X of N scenes` while filtering, followed by `· K in build` when the Build Settings list has enabled scenes. **Build Profiles** opens the Build Profiles window (`File > Build Profiles`, with a fallback to the classic Build window if that menu item is not found).

---

## Colors and grouping

### Color labels

Give any scene a color to spot it faster: right-click a scene and choose **Color**. The palette has Red, Orange, Yellow, Green, Teal, Blue, Purple, Pink and Gray, plus **None** to clear it.

**Automatic colors for favorites:** when you star a scene that has no color yet, it is given the palette color used by the fewest scenes, so your favorites are easy to tell apart without any setup. A color you chose yourself is never replaced, and removing a favorite keeps its color. You can always change or clear it from the **Color** menu.

The color appears as a dot and a soft tint across the row in the window and as a colored edge on the favorite buttons in the overlay. The scene you are currently in is shown **filled with its full color** in the overlay, both on its favorite button and on the scene dropdown, with black or white text chosen for contrast, so you always know where you are at a glance. You can also type a color name in the search field. Colors are stored per user (see below), so they are not shared with your team.

### Grouping (useful when you have many scenes)

The **Group** menu in the toolbar has three modes:

| Mode | Result |
|---|---|
| **No Grouping** | One flat list |
| **By Folder** | One section per folder (without the leading `Assets/`; scenes directly in `Assets/` are under "Assets") |
| **By Color** | One section per color in palette order, then "No color" |

Section headers show the number of scenes they contain. Click a header to collapse or expand it, or use **Collapse All Groups** / **Expand All Groups** in the same menu. Which sections are collapsed is remembered. Favorites and Recent stay at the top in every mode.

---

## The Scene view overlay

The overlay has these parts, in this order:

1. **Scene dropdown.** Shows the active scene name (`Untitled` if it has none, and a trailing `*` when it has unsaved changes) with its color. Click it for a menu with:
   - your favorites,
   - recent scenes,
   - every other scene (listed directly when the project has 12 scenes or fewer; otherwise inside an **All Scenes** submenu organized by folder),
   - **Play From '<active scene>'** (or **Stop Playing From This Scene**),
   - **Scene Sets**: your sets, and **Save Loaded Scenes as New Set...**,
   - **Search Scenes...** and **Open Quick Scene Access Window**.

   The active scene has a check mark. Scenes with the same name in different folders get the folder appended to disambiguate.
2. **Star button.** Adds or removes the active scene from favorites. Right-click it to set the active scene's color. It only appears when the active scene is a saved asset under `Assets/`.
3. **Save-mode button.** Shows what happens with unsaved changes when you switch scenes (`Ask`, `Auto-save` or `Discard`) and lets you change it with one click. It turns orange when it is not on `Ask`.
4. **Play-from button.** Only shown while [Play from here](#play-from-here) is on: an orange `▶ SceneName` button; click it to turn the feature off.
5. **Favorite buttons.** One button per favorite (the rest are always available in the dropdown). Long names are shortened; hover for the full path. The active scene is shown in bold and filled with its color.
   - Click: open the scene.
   - Shift + Click: open additively.
   - Right-click: Open, Open Additively, Color, Play From This Scene, Remove from Favorites, Ping in Project.
6. **Set buttons.** One `Set: <name>` button per [scene set](#scene-sets); click to load all its scenes. Right-click for Load Set, Update From Loaded Scenes, Rename and Delete. A set whose scenes are exactly the loaded ones is shown in bold.

### Layouts

The overlay adapts to where you dock it:

| Where | Layout | Favorite buttons | Set buttons |
|---|---|---|---|
| Top or bottom of the Scene view | One horizontal row | Up to 6, names up to 18 characters | Up to 3 |
| Left or right edge | One compact vertical column | Up to 8, names up to 8 characters | Up to 4 |
| Floating or in a column | A panel with outlined, evenly spaced buttons; the star and save-mode button sit side by side | Up to 12, names up to 24 characters | Up to 8 |

In Play Mode the favorite buttons, set buttons and dropdown items are disabled. Problems (for example "That scene no longer exists") appear as a notification in the Scene view.

The window and the overlay share favorites, recents, colors, sets and settings, so changes in one are reflected in the other.

---

## Scene sets

A scene set is a saved group of scenes you can load together with one click (for example *Level 1 + UI + Audio*). The first scene opens normally and the rest are added to it, then the set's active scene is made active.

- **Create a set:** load the scenes you want, then use **Sets > Save Loaded Scenes as New Set...** in the window toolbar (or **Scene Sets > Save Loaded Scenes as New Set...** in the overlay dropdown) and type a name. The loaded scenes are saved in order, and the current active scene is remembered as the set's active scene. Unsaved or non-project scenes are left out, with a notice.
- **Or build one scene by scene:** right-click any scene and use **Sets > New Set With This Scene...**, or tick/untick it in an existing set under **Sets**.
- **Load a set:** **Sets** menu in the window, **Scene Sets** in the overlay dropdown, or the `Set: <name>` buttons in the overlay. A set whose scenes are exactly the ones loaded right now is shown checked or in bold.
- **Manage:** in the **Sets** menu (or by right-clicking a set button in the overlay): **Update From Loaded Scenes** (asks before replacing), **Rename** and **Delete** (asks first; your scenes are never touched).
- **Safety:** loading a set replaces the open scenes, so it uses the same unsaved-changes mode as opening a single scene (Ask / Auto-save / Discard). It is blocked in Play Mode. Scenes that were deleted since the set was saved are skipped and reported.
- Sets are stored by scene GUID, per project and user, so they survive renames and moves. Set names are unique; a `/` in a name is replaced by `-`.

---

## Quick search

A small search popup for opening a scene without touching the mouse.

- **Open it:** press **Ctrl+Shift+O** (**Cmd+Shift+O** on macOS), or use `Window > Quick Scene Search`, or **Search Scenes...** in the overlay dropdown. Pressing the shortcut again closes it.
- **Change the shortcut:** `Edit > Shortcuts` (on macOS, `Unity > Shortcuts`), search for "Quick Scene Search". If another tool already uses Ctrl+Shift+O, Unity will flag the conflict there.
- **Use it:** start typing. The same matching as the window applies (every word must match the name, folder or color).
  - Up / Down (and Page Up / Page Down) move the selection.
  - **Enter** opens the selected scene, **Shift+Enter** opens it additively, **Esc** closes the popup.
  - Click a result to open it; right-click for the same menu as the window (favorite, color, sets, play from here...).
- **Order:** with an empty search you see favorites first, then recent scenes, then everything else, each scene once. While typing, the best matches come first and favorites win ties. Collapsed sections in the window don't hide anything here.
- It closes by itself when it loses focus, and opens scenes using the current unsaved-changes mode. Scene sets are not included in it.

---

## Play from here

Make the Play button start the game from a specific scene (for example your Menu or a bootstrap scene) while you keep editing the scene you are in.

- **Turn it on:** right-click a scene in the window (or a favorite button in the overlay) and choose **Play From This Scene**. In the overlay dropdown, use **Play From '<active scene>'**.
- **While it is on:** the scene shows a `▶` after its name, the status bar shows an orange `▶ SceneName ✕` button, and the overlay shows an orange `▶ SceneName` button. Click either to turn it off.
- **Turn it off:** click one of those buttons or choose **Stop Playing From This Scene**.
- It uses Unity's own *Play Mode start scene* setting (`EditorSceneManager.playModeStartScene`), so it affects Play Mode only, never builds.
- It is opt-in. If the project already has a start scene set outside this tool, you are asked before it is replaced. Turning the feature off clears the setting; it does not restore a previous one.
- The chosen scene is remembered per project and user, and re-applied after a restart. If that scene is deleted, the feature turns itself off.
- It cannot be changed while in Play Mode.
- Unity may ask you to save modified scenes when you press Play with a start scene set.

---

## Unsaved changes and safety

- **Unsaved changes are never discarded silently by default.** Opening a scene in single mode uses Unity's standard Save / Don't Save / Cancel prompt. Cancel leaves everything as it was.
- **You can change that behavior** with the **Save** menu in the window toolbar, or the save-mode button in the overlay:

  | Mode | What happens when you switch scenes |
  |---|---|
  | **Ask before switching** (default) | Unity's Save / Don't Save / Cancel prompt, only if there are changes |
  | **Auto-save changes** | Modified scenes are saved automatically, then the switch happens. An untitled scene opens Save As; cancelling it cancels the switch |
  | **Discard changes** | The switch happens without saving; unsaved changes are lost |

  Enabling Auto-save or Discard asks for confirmation first. While either is active, the control turns orange so it is hard to forget. Additive opening is not affected.
- **No switching in Play Mode.** The UI is disabled and a notice is shown.
- **Scenes are resolved by GUID** at the moment you click, so a stale list cannot open the wrong file. If the scene was deleted, the list refreshes and you get a notice.
- **What the tool changes.** It only reads your scenes, project settings and Build Settings. The exceptions are opt-in: Auto-save saves scenes you have already modified, and Play from here sets Unity's Play Mode start scene. The only other file it writes is its own settings file (see below).

---

## Where your data is stored

Favorites, recents, colors, grouping, collapsed sections, the save mode, the play-from scene and scene sets are saved per project, per user, in:

```
<project>/UserSettings/QuickSceneAccess/QuickSceneAccess.json
```

```json
{
  "favorites": ["<scene GUID>", "..."],
  "recents":   ["<scene GUID>", "..."],
  "colors":    [{ "guid": "<scene GUID>", "color": 6 }],
  "collapsed": ["folder:Scenes/Levels"],
  "groupMode": 0,
  "unsavedMode": 0,
  "playFromGuid": "",
  "sets": [
    { "id": "<id>", "name": "Level 1", "guids": ["<scene GUID>", "..."], "activeGuid": "<scene GUID>" }
  ]
}
```

- Scenes are stored by **GUID**, so favorites, colors and sets survive renames and moves.
- Up to 8 recents are kept (5 are displayed).
- `groupMode`: 0 none, 1 folder, 2 color. `unsavedMode`: 0 ask, 1 auto-save, 2 discard. `playFromGuid` is empty when Play from here is off.
- `UserSettings/` is excluded by Unity's default `.gitignore`, so each team member has their own settings.
- Data for deleted scenes stays in the file but is not shown; it reappears if the scene is restored.
- If the file cannot be parsed, a copy is saved next to it as `QuickSceneAccess.json.corrupt`, a warning is logged, and the state starts empty.
- To reset everything, close the window and delete the file.

---

## How the list stays up to date

- **Scene discovery** queries the AssetDatabase once and caches the result. It runs again only after the cache is invalidated.
- **Asset changes:** an asset postprocessor invalidates the cache when a `.unity` file is imported, deleted, renamed or moved, or when a folder changes (since folders may contain scenes). Updates are deferred and coalesced so a burst of changes causes one refresh.
- **Editor events** refresh the status (active/loaded/build info) without rescanning: scene opened, closed, created, saved, marked dirty, active scene changed, Build Settings scene list changed, Play Mode changes.
- **Refresh button:** forces a full rescan.

---

## Architecture

All files live in `Editor/QuickSceneAccess/` inside the `Steff.QuickSceneAccess.Editor` assembly (namespace `Steff.QuickSceneAccess.Editor`). UI, data and scene-opening logic are kept separate.

| File | Responsibility |
|---|---|
| `QuickSceneAccessWindow.cs` | The EditorWindow. Builds the UI Toolkit layout and wires events. Public entry point: `QuickSceneAccessWindow.ShowWindow()`. |
| `QuickSceneOverlay.cs` | The Scene view overlay (horizontal, vertical and panel layouts), its dropdown, favorites, set and play-from buttons, and `QuickSceneEvents` (a single event that fires when anything displayed may have changed). |
| `QuickSceneSearchWindow.cs` | The quick search popup (keyboard shortcut, arrow-key navigation). |
| `SceneRowView.cs` | One reusable list row (scene or section header), including the right-click menu. |
| `SceneListBuilder.cs` | Pure logic: catalog + favorites/recents/colors + filter -> list of rows (sections, grouping, collapsing) or a flat list for the quick search. No UI, no AssetDatabase calls. |
| `SceneCatalog.cs` | `SceneEntry` (a scene: GUID, path, name, folder), the cached scene list, and the postprocessor that invalidates it. |
| `SceneStatus.cs` | Snapshot of active scene, loaded scenes and Build Settings info. |
| `SceneOpener.cs` | Safe opening of a scene or a set (respecting the unsaved-changes mode), ping and reveal. Returns an `OpenResult` instead of showing UI. |
| `SceneSetActions.cs` | Scene set operations (create, load, update, rename, delete) and the **Sets** menu, shared by the window, overlay and search. |
| `NamePromptWindow.cs` | A tiny modal window that asks for a name. |
| `PlayFromScene.cs` | "Play from here": sets and keeps in sync Unity's Play Mode start scene. |
| `UnsavedChangesMode.cs` | The Ask / Auto-save / Discard modes and the confirmation shown when enabling the last two. |
| `SceneColor.cs` | The color palette plus helpers shared by the window and the overlay (menus, markers, fills). |
| `QuickSceneAccessState.cs` | Favorites, recents, colors, sets, grouping, collapsed sections, save mode and play-from scene, persisted as JSON. Raises `Changed`. |
| `QuickSceneAccess.uss` | Styles. Theme colors are variables defined for dark and light editor themes. |

### Data flow

```
AssetDatabase --> SceneCatalog (cache) --\
QuickSceneAccessState (JSON) ------------>-- SceneListBuilder --> rows --> window list / quick search
SceneStatus (active/loaded/build) ------/
                                                  user clicks -> SceneOpener -> EditorSceneManager
```

The window, the overlay and the quick search all read from `SceneCatalog`, `QuickSceneAccessState` and `SceneStatus`, and open scenes only through `SceneOpener`.

### Compatibility notes

- Editor-only; it must stay inside an Editor assembly definition. No runtime dependencies are added.
- The `.uss` file is located next to the window script at runtime, so the folder can be moved without breaking styles.
- Most types are `internal`. The public surface is `QuickSceneAccessWindow` and `QuickSceneOverlay`.

---

## Customizing

Current options are constants in the source:

| Setting | Where | Default |
|---|---|---|
| Folders scanned for scenes | `SceneCatalog.SearchFolders` | `Assets` |
| Recents stored | `QuickSceneAccessState.MaxRecents` | 8 |
| Recents shown | `SceneListBuilder.MaxRecentsShown` | 5 |
| Row height | `QuickSceneAccessWindow.RowHeight` | 36 px |
| Favorite buttons / label length per overlay layout | `QuickSceneFavoritesStrip.MaxButtons` / `MaxLabelLength` | 6/18, 8/8, 12/24 |
| Set buttons per overlay layout | `QuickSceneFavoritesStrip.AddSetButtons` (`maxSets`) | 3, 4, 8 |
| Overlay flat-menu limit | `QuickSceneDropdown.FlatListLimit` | 12 |
| Quick search popup size | `QuickSceneSearchWindow.PopupSize` | 460 x 380 px |
| Quick search shortcut | `[Shortcut]` in `QuickSceneSearchWindow.cs` (or `Edit > Shortcuts`) | Ctrl/Cmd+Shift+O |
| Color palette | `SceneColorUtil.ToColor` | 9 colors |

Colors and spacing of the window are in `QuickSceneAccess.uss`.

---

## Known limitations

- Only scenes under `Assets/` are listed (scenes inside packages are not).
- Build info reflects the global Build Settings scene list, not per-profile scene overrides.
- Scene sets are not shown in the quick search, cannot be reordered, and only include saved scenes under `Assets/`.
- Not implemented: scene thumbnails, closing additive scenes, reordering favorites, adding scenes to the build from the tool, sharing colors, favorites or sets with the team.
- The favorite star uses the characters ★ and ☆, and the section arrow uses ">"; if your editor font lacks the star characters they may not render.
- The overlay's panel layout (floating or in a column) reuses the toolbar-style buttons, so it is the plainest of the three layouts.

---

## Troubleshooting

| Problem | What to try |
|---|---|
| The overlay is not visible | Press `` ` `` over the Scene view and enable **Quick Scenes** in the Overlays menu. |
| The list looks unstyled and the Console shows "Could not find QuickSceneAccess.uss" | Make sure `QuickSceneAccess.uss` is in the same folder as `QuickSceneAccessWindow.cs`. |
| A scene is missing from the list | It must be under `Assets/`. Press Refresh. A collapsed section can also hide it: expand it from the Group menu. |
| Favorites, colors or sets disappeared | Check `UserSettings/QuickSceneAccess/QuickSceneAccess.json` (and whether a `.corrupt` copy exists). |
| Cannot switch scenes | Exit Play Mode. |
| Play starts from the wrong scene | Look for the orange `▶` button in the status bar or overlay and turn Play from here off. |
| The quick search shortcut does nothing | Another tool may use it. Check `Edit > Shortcuts` for conflicts and reassign it. |
| A set loads fewer scenes than expected | Some of its scenes were deleted. Load the scenes you want and use **Update From Loaded Scenes**. |
| **Build Profiles** does nothing | Open it from `File > Build Profiles` instead and report your Unity version. |

---

## Manual test checklist

1. Open the window; check both the dark and light editor themes.
2. Search by part of a name, by folder, by color and with two words. Press Enter, then Esc.
3. Click a scene; the **Active** chip moves. Shift+Click another; it gets a **Loaded** chip.
4. Try each save mode with a modified scene: Ask shows the prompt, Auto-save saves silently, Discard switches without saving.
5. Star a scene, close and reopen the window; it is still starred and has a color. Check the same star in the overlay.
6. Give several scenes colors; check the dots in the window and the edges and fill in the overlay.
7. Try each Group mode, collapse sections, close and reopen the window (collapse state is kept), then use Collapse All / Expand All.
8. Create, rename, move and delete a scene (and a folder containing scenes); the list updates by itself.
9. Add, disable and remove scenes in Build Settings; the chips and the "in build" count update.
10. Enter Play Mode; the banner appears and clicking does nothing.
11. Test an empty project (no scenes) and a large one (200+ scenes).
12. Drag the overlay to the bottom, a side edge and the floating position; check that its layout changes each time and that the dropdown, star, save-mode, favorite and set buttons work in all of them.
13. Create a set from several loaded scenes, load another scene, then load the set: all scenes return and the right one is active. Update, rename and delete it; delete one of its scenes and load it again to see the notice.
14. Press Ctrl/Cmd+Shift+O: type, move with the arrow keys, open with Enter and Shift+Enter, close with Esc, and press the shortcut twice to toggle it.
15. Turn on Play from here for another scene, press Play, and check it starts there; then turn it off. Also try it with a start scene already set by hand to see the confirmation.