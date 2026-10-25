import { browser } from "$app/env";
import axios from "axios";
import { decodeJwt } from "jose";

const API_URL = "/api/v2";
const tokenKey = "access_token";
const sessionExpired = "Your session has expired. Please sign in again.";

// The server writes ClaimTypes.Name under its full URI; the short forms are fallbacks.
const nameClaims = [
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
    "unique_name",
    "name",
    "sub",
];

/** Reads the claims the UI needs, or undefined if the token is malformed. */
function readToken(token: string): { userName?: string; expired: boolean } | undefined {
    try {
        const claims = decodeJwt(token);
        const name = nameClaims.map((c) => claims[c]).find((v) => typeof v === "string");
        const now = Math.floor(Date.now() / 1000);
        return { userName: name as string | undefined, expired: !claims.exp || claims.exp < now };
    } catch {
        return undefined;
    }
}

/**
 * Shared sign-in state. The base map works signed out; network data needs a signed-in user.
 */
class Auth {
    signedIn = $state(false);
    userName = $state<string>();
    /** Whether the sign-in dialog is open, and an optional notice saying why it opened. */
    dialog = $state<{ open: boolean; notice?: string }>({ open: false });

    #token: string | undefined;

    /** The bearer token for API requests, or undefined when signed out. */
    get token() {
        return this.#token;
    }

    /** Restores the session from localStorage. Call once, in the browser. */
    restore() {
        if (!browser) return;

        const token = localStorage.getItem(tokenKey);
        const claims = token ? readToken(token) : undefined;
        if (token && claims && !claims.expired) {
            this.#accept(token, claims.userName);
        } else if (token) {
            // Drop a malformed or expired token instead of sending it with every request.
            localStorage.removeItem(tokenKey);
            if (claims?.expired) this.openSignIn(sessionExpired);
        }
    }

    async signIn(username: string, password: string) {
        // The activate endpoint mirrors the WiGLE app's form shape, so the SPA and the
        // Android app authenticate through exactly the same route.
        const params = new URLSearchParams();
        params.append("credential_0", username);
        params.append("credential_1", password);
        params.append("type", "WEB");

        const response = await axios.post<{ token: string }>(`${API_URL}/activate`, params);

        const token = response.data.token;
        const claims = readToken(token);
        if (!claims || claims.expired) throw new Error("The server returned an invalid token.");

        localStorage.setItem(tokenKey, token);
        this.#accept(token, claims.userName ?? username);
    }

    signOut() {
        localStorage.removeItem(tokenKey);
        this.#token = undefined;
        this.signedIn = false;
        this.userName = undefined;
    }

    /**
     * Handles a 401 from any API call: signs out and reopens the sign-in dialog. Several
     * requests usually fail at once (one per tile), so calls while signed out do nothing.
     */
    expire() {
        if (!this.signedIn) return;
        this.signOut();
        this.openSignIn(sessionExpired);
    }

    openSignIn(notice?: string) {
        this.dialog = { open: true, notice };
    }

    closeSignIn() {
        if (this.dialog.open) this.dialog = { open: false };
    }

    #accept(token: string, userName: string | undefined) {
        this.#token = token;
        this.userName = userName;
        this.signedIn = true;
    }
}

export const auth = new Auth();
