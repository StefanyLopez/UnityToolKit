# Steff's Unity Toolkit

A modular collection of reusable Unity Editor tools, runtime systems, and project templates.

## Packages

| Package | Description | Status |
| --- | --- | --- |
| `com.steff.toolkit.editor` | Editor dashboard and workflow tools | Initial foundation |

## Requirements

- Unity 6 (6000.x)

# Quick Scene Access

Jump between scenes in a click. Search them, star your favorites, color-code them, and open any scene straight from the Scene view, without digging through the Project window.

Part of **Steff's Unity Toolkit** (`com.steff.toolkit.editor`). It's Editor-only, so nothing ends up in your build. Developed on Unity 6.3 LTS.

---

## Get started

- **Window:** `Window > Steff's Toolkit > Quick Scene Access`
- **Scene view overlay:** look for the **Quick Scenes** bar at the bottom of the Scene view. Not there? Press `` ` `` with the cursor over the Scene view, open the Overlays menu and turn on **Quick Scenes**.

The window and the overlay share the same favorites, colors and settings, so what you do in one shows up in the other.

---

## What you can do

**Find a scene fast.** Type part of a name, folder or color. With several words, all of them have to match (`lvl menu` finds `Menu` inside `Levels`). Press **Enter** to open the top result.

**Open it your way.**

| Action | What happens |
|---|---|
| Click | Opens the scene |
| Shift + Click | Opens it additively, keeping the current one |
| Right-click | Open, open additively, favorite, color, ping in Project, show in Explorer/Finder |
| Click the active scene | Pings it in the Project window |

**Pin your favorites.** Click the star on any scene. Favorites go to the top of the list and become one-click buttons in the Scene view.

**Color-code your scenes.** Right-click a scene and pick a **Color**. You'll see a dot in the list and a colored edge on its overlay button, and you can even search by color name.

**Tame a long list.** The **Group** menu organizes scenes by folder or by color. Click a section header to collapse it (the tool remembers), or use Collapse All / Expand All.

**Pick up where you left off.** The last scenes you opened show up under **Recent**.

**See what's what at a glance.**

- **Active**: the scene you're in. **Loaded**: open additively next to it.
- `#2`: it's in your Build Settings with that build index. `off`: it's there but disabled.
- The status bar shows the scene count and a **Build Profiles** shortcut.

---

## The Scene view overlay

It reorganizes itself to fit wherever you dock it:

- **Top or bottom:** a single row.
- **Left or right edge:** a compact column.
- **Floating or in a column:** a small panel.

It has a dropdown with your current scene (a `*` means unsaved changes), a star for the active scene (right-click it to set a color), a **save-mode button** and one button per favorite (Shift+Click to open additively, right-click for more).

---

## Unsaved changes: you choose

The **Save** menu in the window toolbar, or the save-mode button in the overlay, controls what happens when you switch scenes with unsaved changes:

- **Ask** (default): Unity's usual **Save / Don't Save / Cancel** prompt.
- **Auto-save**: saves for you, then switches.
- **Discard**: switches and throws the changes away. Handy for quick experiments.

Auto-save and Discard ask you to confirm first, and the control turns orange while they're on so you don't forget.

---

## Safe by design

- Scene switching is turned off in Play Mode.
- The tool only reads your project. It never edits project settings or Build Settings, and it only saves scenes if you turn on Auto-save.
- Scenes are tracked by GUID, so a clicked scene is always the right file, even after renames or moves.

---

## Where your stuff is saved

Favorites, recents, colors, grouping and your Save mode live in:

```
<project>/UserSettings/SteffsToolkit/QuickSceneAccess.json
```

It's per project and per user, and Unity's default `.gitignore` leaves it out, so everyone on the team keeps their own setup. Want a fresh start? Close the window and delete the file. If it ever gets corrupted, a `.corrupt` backup is kept next to it.

---

## It keeps itself up to date

Add, rename, move or delete a scene and the list updates on its own. If it ever looks stale, the Refresh button rescans the project.

---

## Under the hood

Everything is in `Editor/QuickSceneAccess/`, namespace `SteffsToolkit.Editor`, with UI, data and scene-opening kept apart:

- `QuickSceneAccessWindow.cs`, `QuickSceneOverlay.cs`, `SceneRowView.cs`, `QuickSceneAccess.uss`: the UI (UI Toolkit).
- `SceneListBuilder.cs`: pure logic that turns scenes + favorites + colors + search into the list.
- `SceneCatalog.cs`: finds scenes once, caches them, and refreshes when assets change.
- `SceneStatus.cs`: active, loaded and build info.
- `SceneOpener.cs` and `UnsavedChangesMode.cs`: the safe opening logic and the save modes.
- `SceneColor.cs`: the color palette.
- `QuickSceneAccessState.cs`: favorites, colors and settings, saved as JSON.

Handy values to tweak (all constants in the source): folders scanned (`Assets`), recents stored/shown (8/5), overlay buttons per layout, row height (36 px). Window colors live in the `.uss` file and adapt to the dark and light themes.

---

## Good to know

- Only scenes under `Assets/` are listed.
- Build info comes from the global Build Settings scene list, not per-profile overrides.
- Not here yet: arrow-key navigation, thumbnails, closing additive scenes, sharing colors with the team.

## If something looks off

- **No overlay?** Enable it from the Overlays menu (`` ` `` over the Scene view).
- **Unstyled window?** Keep `QuickSceneAccess.uss` in the same folder as `QuickSceneAccessWindow.cs`.
- **Scene missing?** It has to live under `Assets/`, and its section might be collapsed. Hit Refresh.
- **Can't switch scenes?** Exit Play Mode.