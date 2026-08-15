import { browser } from "$app/environment";
import axios from "axios";
import { decodeJwt } from "jose";

export const prerender = false;
export const ssr = false;

const API_URL = "/api/v2";

class AuthService {
    private tokenKey = "access_token"; // Store token key

    // Login and get token
    async login(username: string, password: string) {
        try {
            // The activate endpoint mirrors the WiGLE app's form shape, so the SPA and the
            // Android app authenticate through exactly the same route.
            const params = new URLSearchParams();
            params.append("credential_0", username);
            params.append("credential_1", password);
            params.append("type", "WEB");

            const response = await axios.post(`${API_URL}/activate`, params);

            const token = response.data.token;
            this.storeToken(token);
            return token;
        } catch (error) {
            this.logout();
            console.error("Login failed", error);
            throw error;
        }
    }

    // Store token securely
    storeToken(token: string) {
        localStorage.setItem(this.tokenKey, token);
    }

    isAuthorized(): boolean | undefined {
        if (!browser)
            return undefined;

        const token = localStorage.getItem(this.tokenKey);
        if (!token) return false;

        const { exp } = decodeJwt(token) ?? {};
        const now = Math.floor(Date.now() / 1000);

        if (!exp || exp < now) return false; // token expired or malformed

        return true;
    }

    /**
     * Returns the stored token, null if unauthorized, or undefined while the state is not yet
     * known (during SSR, where localStorage is unavailable).
     */
    getToken(): string | null | undefined {
        const isAuth = this.isAuthorized();
        if (!isAuth)
            return isAuth == false ? null : isAuth;
        if (!browser)
            return undefined;

        return localStorage.getItem(this.tokenKey);
    }

    // Remove token on logout
    logout() {
        localStorage.removeItem(this.tokenKey);
    }
}

export default new AuthService();
