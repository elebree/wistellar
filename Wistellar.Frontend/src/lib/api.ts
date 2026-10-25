import axios from "axios";
import type { ErrorEvent, RequestTransformFunction } from "maplibre-gl";
import { auth } from "./auth.svelte.ts";

// The one place that authorizes requests to the Wistellar API, for axios and for MapLibre's tile
// requests, and that signs out when the API answers 401.

/** Whether `url` goes to the API, which is served from the same origin as the SPA. */
function isApiUrl(url: string): boolean {
    const { origin, pathname } = new URL(url, location.href);
    return origin === location.origin && /^\/(api|geo)\//.test(pathname);
}

function authHeaders(): Record<string, string> {
    const token = auth.token;
    return token ? { Authorization: `Bearer ${token}` } : {};
}

/** HTTP client for the API. */
export const api = axios.create();

api.interceptors.request.use((config) => {
    config.headers.set(authHeaders());
    return config;
});

api.interceptors.response.use(undefined, (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) auth.expire();
    return Promise.reject(error);
});

/** MapLibre's `transformRequest`: authorizes tile requests to the API and leaves other URLs alone. */
export const transformRequest: RequestTransformFunction = (url) =>
    isApiUrl(url) ? { url, headers: authHeaders() } : undefined;

/**
 * MapLibre `error` listener: a 401 on an API tile signs out, like a 401 from any other API call.
 * Returns false for errors it leaves to the caller.
 */
export function handleMapError(ev: ErrorEvent): boolean {
    const { status, url } = ev.error as { status?: number; url?: string };
    if (status !== 401 || !url || !isApiUrl(url)) return false;
    auth.expire();
    return true;
}
