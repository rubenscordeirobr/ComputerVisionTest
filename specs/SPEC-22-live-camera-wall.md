# SPEC-22 — Live camera wall in the web app

Extends SPEC-03 (camera management) and SPEC-14 (multi-tenancy). Replaces the
anonymous stream viewer (`client/index.html`, published as
`view-camera.vemlogo.com`) with a signed-in "Ao vivo" page that shows the
tenant's cameras on a configurable grid.

## Problem

Live streams were watched on a static HTML page served by Caddy with no
authentication: anyone with the URL could watch every camera, and the page
listed camera names hard-coded in the file. The management app already knows
which cameras belong to the signed-in user, so the wall belongs there — with
layouts that fit how people actually watch (one main camera plus a few small
ones, two rows of different sizes, plain grids) rather than a single auto-fit
grid.

## Design

- **Access**: `/live` is a normal authenticated page (nav "Ao vivo", plus
  "Ver ao vivo" on the dashboard camera card). Cameras come from
  `ICameraRepository.GetAllAsync(tenantFilter)` — the tenant's own cameras, or
  all of them for the SuperAdmin — filtered to enabled cameras with a stream
  URL. The public viewer site and its Caddy block / compose volume are gone.
- **Layout catalog** (`CameraVision.Core.Live`): `LiveLayouts.All` holds the
  templates, each a `LiveLayout` with column/row weights (`fr`) and one
  `LiveSlot` (column, row, spans) per camera. Templates exist for 1 to
  `MaxCameras` (6) cameras, several per count — equal grids, rows and columns,
  a highlighted camera on the left/right/top/bottom with the rest beside it,
  two rows of different counts (2+3, 3+2), a corner highlight with five around
  it. Highlight cells use 2:1 / 3:1 weights so every cell keeps the 16:9 ratio.
  Names are PT-BR (user-facing); keys are stable URL identifiers.
- **Choosing**: the options panel has "Câmeras na tela" (1 to
  min(6, available)), the templates for that count as clickable thumbnails
  (numbered cells), and one "Posição N" select per slot. Picking a camera that
  is already shown swaps the two slots, so a camera never appears twice.
  Changing the count keeps the assignments and auto-fills new slots with the
  cameras not shown yet.
- **State**: `LiveViewSelection` (layout key + camera ids per slot) is parsed
  from and written to `?layout=…&cams=…` (history entry replaced), and the
  same string is stored in `localStorage` (`cameravision.live`) so the next
  visit without a query restores it. `Resolve(availableIds)` makes any stale
  selection displayable: unknown/duplicate ids become empty slots, an unknown
  key falls back to the default template for the slot count, slots are
  trimmed/padded, and gaps are filled with unused cameras.
- **Rendering**: `LiveGrid` is a CSS grid built from the layout's inline
  style; each cell hosts a `LiveTile` that plays `annotated/{name}` with the
  existing `live-player.js` (WebRTC → HLS fallback) and shows a name/status
  badge. Cells are keyed by camera id, so reassigning slots moves the
  `<video>` element instead of reconnecting. The stage gets the layout's
  overall aspect ratio (sum of column weights × 16 : sum of row weights × 9)
  bounded by the viewport height, so no cell is letterboxed.
- **Other tab / fullscreen**: "Abrir em nova aba" links to
  `/live/full?layout=…&cams=…`, rendered with `FullscreenLayout` (no app bar or
  drawer, black stage filling the viewport, still authenticated). "Tela cheia"
  puts the stage in browser fullscreen; double-clicking a tile does the same
  for that camera alone.

## Non-goals

- Per-user layouts stored in the database (the browser remembers the last one;
  the URL shares it).
- More than six cameras per screen — beyond that, tiles get too small to be
  useful; users open several tabs instead.
- Authentication of the MediaMTX WebRTC/HLS endpoints themselves: the page is
  behind the sign-in, but `/webrtc/*` and `/hls/*` on the proxied hosts still
  answer anyone who knows a stream path.
