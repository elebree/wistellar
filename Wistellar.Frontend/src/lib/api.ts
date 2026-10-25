import axios from "axios";
import { auth } from "./auth.svelte.ts";

/** HTTP client for the `/geo` endpoints: sends the bearer token and signs out on a 401. */
export const api = axios.create();

api.interceptors.request.use((config) => {
    const token = auth.token;
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
});

api.interceptors.response.use(undefined, (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) auth.expire();
    return Promise.reject(error);
});
