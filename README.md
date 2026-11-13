# Steff's Unity Toolkit

A modular collection of reusable Unity Editor tools, runtime systems, and project templates.

## Packages

| Package | Description | Status |
| --- | --- | --- |
| `com.steff.toolkit.editor` | Editor dashboard and workflow tools | Initial foundation |

## Requirements

- Unity 6 (6000.x)

# Quick Scene Access

Jump between scenes in a click. Search them, star your favorites, and open any scene straight from the Scene view, without digging through the Project window.

Part of **Steff's Unity Toolkit** (`com.steff.toolkit.editor`). It's Editor-only, so nothing ends up in your build. Developed on Unity 6.3 LTS.

---

## Get started

- **Window:** `Window > Steff's Toolkit > Quick Scene Access`
- **Scene view overlay:** look for the **Quick Scenes** bar at the bottom of the Scene view. Not there? Press `` ` `` with the cursor over the Scene view, open the Overlays menu and turn on **Quick Scenes**. You can drag it wherever you like.

The window and the overlay share the same favorites, so what you do in one shows up in the other.

---

## What you can do

**Find a scene fast.** Type part of a name or folder. With several words, all of them have to match (`lvl menu` finds `Menu` inside `Levels`). Press **Enter** to open the top result.

**Open it your way.**

| Action | What happens |
|---|---|
| Click | Opens the scene |
| Shift + Click | Opens it additively, keeping the current one |
| Right-click | Open, open additively, favorite, ping in Project, show in Explorer/Finder |
| Click the active scene | Pings it in the Project window |

**Pin your favorites.** Click the star on any scene. Favorites go to the top of the list and become one-click buttons in the Scene view (up to 6; the rest live in the overlay's dropdown).

**Pick up where you left off.** The last scenes you opened show up under **Recent**.

**See what's what at a glance.**

- **Active**: the scene you're in. **Loaded**: open additively next to it.
- `#2`: it's in your Build Settings with that build index. `off`: it's there but disabled.
- **Group** in the toolbar organizes the list by folder. The status bar shows the scene count and a **Build Profiles** shortcut.

The overlay's dropdown shows your current scene (with a `*` when it has unsaved changes) and lists favorites, recents and everything else.

---

## Safe by design

- By default, switching scenes with unsaved changes opens Unity's usual **Save / Don't Save / Cancel** prompt. Cancel and nothing changes.
- Doing quick experiments? The **Unsaved** menu in the toolbar lets you switch to **Auto-save** (saves for you) or **Discard** (throws changes away). Both ask you to confirm first, and the label turns orange while they're on so you don't forget.
- Scene switching is turned off in Play Mode.
- The tool only reads your project. It never edits project settings or Build Settings, and it only saves scenes if you turn on Auto-save.
- Scenes are tracked by GUID, so a clicked scene is always the right file, even after renames or moves.

---

## Where your stuff is saved

Favorites, recents, the Group toggle and your Unsaved mode live in:

```
<project>/UserSettings/SteffsToolkit/QuickSceneAccess.json
```

It's per project and per user, and Unity's default `.gitignore` leaves it out, so everyone on the team keeps their own favorites. Want a fresh start? Close the window and delete the file. If it ever gets corrupted, a `.corrupt` backup is kept next to it.

---

## It keeps itself up to date

Add, rename, move or delete a scene and the list updates on its own. If it ever looks stale, the Refresh button rescans the project.

---

## Under the hood

Everything is in `Editor/QuickSceneAccess/`, namespace `SteffsToolkit.Editor`, with UI, data and scene-opening kept apart:

- `QuickSceneAccessWindow.cs`, `QuickSceneOverlay.cs`, `SceneRowView.cs`, `QuickSceneAccess.uss`: the UI (UI Toolkit).
- `SceneListBuilder.cs`: pure logic that turns scenes + favorites + search into the list.
- `SceneCatalog.cs`: finds scenes once, caches them, and refreshes when assets change.
- `SceneStatus.cs`: active, loaded and build info.
- `SceneOpener.cs`: the safe opening logic.
- `QuickSceneAccessState.cs`: favorites and recents, saved as JSON.

Handy values to tweak (all constants in the source): folders scanned (`Assets`), recents stored/shown (8/5), overlay favorite buttons (6), row height (36 px). Colors live in the `.uss` file and adapt to the dark and light themes.

---

## Good to know

- Only scenes under `Assets/` are listed.
- Build info comes from the global Build Settings scene list, not per-profile overrides.
- Not here yet: arrow-key navigation, collapsible folders, thumbnails, closing additive scenes.
- The overlay is designed for horizontal docking (top or bottom).

## If something looks off

- **No overlay?** Enable it from the Overlays menu (`` ` `` over the Scene view).
- **Unstyled window?** Keep `QuickSceneAccess.uss` in the same folder as `QuickSceneAccessWindow.cs`.
- **Scene missing?** It has to live under `Assets/`. Hit Refresh.
- **Can't switch scenes?** Exit Play Mode.