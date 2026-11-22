// Small choices about the page's own controls (an open or closed panel), kept in cookies for a year.

const maxAge = 365 * 24 * 60 * 60;

/** The cookie's value, or undefined when it isn't set or there is no document (prerendering). */
export function readCookie(name: string): string | undefined {
    if (typeof document === "undefined") return undefined;
    for (const pair of document.cookie.split(";")) {
        const [key, ...value] = pair.trim().split("=");
        if (key === name) return decodeURIComponent(value.join("="));
    }
    return undefined;
}

export function writeCookie(name: string, value: string) {
    if (typeof document === "undefined") return;
    document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${maxAge}; SameSite=Lax`;
}

/** Whether the time filter's ruler is open on large screens, where it opens by default. */
export const timeRulerCookie = "time_ruler";
/** Whether the map's attribution is collapsed, where MapLibre lets it collapse (narrow maps). */
export const attributionCookie = "map_attribution";
