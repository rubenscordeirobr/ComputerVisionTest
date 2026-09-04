// Helpers for the live-view pages (SPEC-22): remember the last layout in this
// browser and toggle fullscreen on the stage or on a single tile.

export function load(key) {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

export function save(key, value) {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Private mode / storage disabled: the URL still carries the selection.
  }
}

export function toggleFullscreen(element) {
  if (document.fullscreenElement) {
    document.exitFullscreen().catch(() => {});
  } else if (element?.requestFullscreen) {
    element.requestFullscreen().catch(() => {});
  }
}
