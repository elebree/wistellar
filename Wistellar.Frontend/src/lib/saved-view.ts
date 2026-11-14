// The map view and the filter, kept across launches. A browser tab keeps them in its URL, but the
// installed app always starts at the manifest's start_url, which has no query string. So the page
// also saves them here, and falls back to them when the URL has none.

const storageKey = "map_view";

export interface MapView {
    lat: number;
    lon: number;
    zoom: number;
}

interface Saved {
    view?: MapView;
    filter?: string;
}

function read(): Saved {
    try {
        const saved = JSON.parse(localStorage.getItem(storageKey) ?? "{}");
        return saved && typeof saved === "object" ? saved : {};
    } catch {
        return {};
    }
}

function write(change: Saved) {
    try {
        localStorage.setItem(storageKey, JSON.stringify({ ...read(), ...change }));
    } catch {
        // Storage is full or blocked. Only the next launch loses out.
    }
}

export function loadSavedView(): MapView | undefined {
    const { view } = read();
    return view && [view.lat, view.lon, view.zoom].every(Number.isFinite) ? view : undefined;
}

export function loadSavedFilter(): string | undefined {
    const { filter } = read();
    return typeof filter === "string" ? filter : undefined;
}

export const saveView = (view: MapView) => write({ view });
export const saveFilter = (filter: string) => write({ filter });
