# Adventure Mapper — user guide

Adventure Mapper draws the map for a tabletop adventure: rooms on numbered
floors, links between them, and the puzzles that guard the way. Maps are saved in
one file and can be printed or exported for the table.

## Requirements

| Device | Version |
| --- | --- |
| iPad | iPadOS 18 or newer |
| Android tablet | Android 12L (API 32) or newer |
| Mac | macOS 26 or newer |
| Windows | Windows 11 (build 22000 or newer) |

The editor is designed for a tablet or desktop screen. On a phone the same tabs
are available in a compact layout.

## Opening the app

* **Tablet** — a tab bar at the bottom switches between **Map** and **Details**.
* **Mac / Windows** — the workspace shows the menu bar, canvas, inspector and
  status bar at once.

A new map starts with one room, **Entrance**. The status bar along the bottom
shows the latest message on the left and the file/autosave state on the right.

## The map canvas

Rooms are drawn as cards. Each card shows the room name, its reference
(`layer-room`, for example `0-3`), the description and the room kind. A **1
puzzle** note appears when the room has puzzles.

* **Tap a room** to select it. Eight compass handles appear around it.
* **Drag a room** to move it. With snap enabled it lands on the grid.
* **Scroll** the canvas when the map is larger than the screen.

### Floors

Rooms live on numbered layers. Layer `0` is the ground floor, positive numbers
are above it and negative numbers below it. The toolbar shows the current floor
and has previous/next floor buttons; the **Details** tab also has **Floor above**
and **Floor below**.

A room linked to another floor shows a small `Up 1-9` or `Down 0-3` badge under
its card, so you can see which floor the stairs lead to.

### Adding rooms

Select a room, then use a direction in the toolbar or the **Map** menu:

* `N`, `NE`, `E`, `SE`, `S`, `SW`, `W`, `NW` — add a room next door and connect
  both ways.
* `Up floor`, `Down floor` — add a room one layer above or below and connect it.
* `Add room` — add a detached room; drag it into place and connect it manually.

The canvas grows as needed and scrolls to the new room.

### Links

Links are drawn between room cards. A two-way link has no arrowhead. Tap a link
to make it **one-way**: it turns blue and gains an arrowhead showing the direction
of travel. Tap it again to restore the return leg.

Drag from a compass handle onto another room to connect two existing rooms in
that direction. The inspector can also add a link by target reference
(`layer-room`).

### Bridges and pits

A **bridge** is a room whose walkway sits one layer above the floor it spans.
Set the room kind to *Bridge* and choose the floor and room underneath. Stepping
onto the bridge raises the player; **Down** returns them to the spanned room,
unless that room is a **pit**, in which case the drop is a fall. Map checks flag a
bridge over a pit.

## Details tab / inspector

The inspector edits the selected room and the map:

* **Map** — title and author.
* **Layers** — choose a floor, rename it, or add the floor above/below.
* **Room** — name, description (127 characters), kind, hidden flag. Hidden rooms
  are drawn with a dashed border and stay secret in play.
* **Connections** — every exit with its direction and target; remove one with
  `✕`, or add a new target.
* **Puzzles** — add or edit a puzzle: title, kind (lock, riddle, mechanism, trap,
  item, other), the prompt, the solution and an optional reward.
* **Auto save** — the interval (off, 30 s, 1, 2, 5 or 10 minutes), the last save
  time and **Save now**.
* **Map checks** — broken links, duplicate directions, floor/room mismatches and
  bridge-over-pit hazards, updated as you edit.

Descriptions and puzzles open in centred dialogs.

## Files

Maps use the `.ama` extension and contain plain JSON:

```json
{ "format": "adventure-mapper", "formatVersion": 1, "title": "…" }
```

* **New** starts a fresh map.
* **Open…** picks an `.ama` file. On iPad and Mac, double-clicking an `.ama` file
  in Files/Finder also opens it.
* **Save** writes the current file; **Save as…** asks where to put it.
* **Autosave** writes a draft every minute. Maps that have never been saved go to
  `Documents/Adventure Mapper`. Drafts are replaced by the real file after the
  first Save.

Writes go through a temporary file and are moved into place, so an interrupted
save cannot destroy an existing map.

## Export and print

| Format | Contents |
| --- | --- |
| JPEG | The whole map sheet, one panel per layer |
| PDF | The map sheet plus a room and puzzle index |
| JSON | The `.ama` source |

**Print** sends the same PDF to the platform print service: the macOS/iPadOS print
panel, Android's print service, or the Windows print action. It is on the toolbar
and under **File → Print map…** on desktop.

## Desktop menus and shortcuts

| Command | Where |
| --- | --- |
| New / Open / Save / Save as… | File menu, toolbar |
| Print map… | File menu, toolbar |
| Export JPEG / PDF / JSON | File menu, toolbar **Export…** |
| Undo / Redo | Map menu, toolbar, and the native Edit menu (⌘Z / ⇧⌘Z) |
| Snap to grid | Map menu, toolbar |
| Add room in a direction | Map menu, toolbar |
| Floor above / below | Map menu, Layer menu, toolbar |
| Delete room | Map menu, inspector |
| About / Map format | Help menu |

Text fields keep their normal undo behaviour; the Edit-menu Undo/Redo applies to
room positions and layout resets.

## Troubleshooting

* **The map is empty** — a new map contains only *Entrance*. Add a room with a
  direction button.
* **A link is missing** — check the target reference is `layer-room`, for example
  `0-3` or `-1-2`. Map checks lists invalid targets.
* **The wrong floor is shown** — use the floor buttons or the layer picker in
  Details.
* **A draft is not where I saved it** — drafts live in
  `Documents/Adventure Mapper` until the first Save.
* **Printing does nothing** — check a printer is available to the system print
  service; export a PDF as a fallback.

## Language and privacy

The interface follows the device language (English and German are included).
Adventure Mapper does not collect data and makes no network requests. See
[Privacy.md](Privacy.md).
