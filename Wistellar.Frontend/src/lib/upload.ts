import axios from "axios";
import { api } from "./api.ts";

// Imports files through the upload endpoint the WiGLE app uses, POST /api/v2/file/upload. The
// server picks an importer by file name; see StreamImporter.ImportFileStreamAsync.

const API_URL = "/api/v2";

/** For the file picker's `accept`. `isSupported` has the exact rules. */
export const acceptedFileTypes = ".csv,.gz,.zip,.kml,.kmz,.sqlite";

/** The part of the upload response the SPA reads. The rest mirrors the WiGLE API. */
interface UploadResponse {
    /** Whether anything in the upload was imported. */
    success: boolean;
    /** The warnings of every file in the upload, one per line. */
    warning: string;
}

/** The server took the upload but imported nothing from it. The message is its warning. */
export class NothingImportedError extends Error {}

/**
 * Whether the file name identifies a supported import format or archive. Decompression and
 * format detection happen on the server, including when a supported file is GZip-compressed.
 */
export function isSupported(file: File): boolean {
    return /\.(csv|zip|kml|kmz|sqlite)(\.gz)?$/i.test(file.name);
}

/** Drops repeated lines: an archive of unreadable files has the same warning for each one. */
function uniqueLines(text: string): string {
    return [...new Set(text.split(/\r?\n/).filter((line) => line.trim()))].join("\n");
}

/**
 * Uploads and imports one file, and resolves to the server's warnings, which are empty if it read
 * everything. Rejects with NothingImportedError when the server could not read any of it.
 */
export async function uploadFile(
    file: File,
    options: { signal: AbortSignal; onProgress: (fraction: number) => void },
): Promise<string> {
    // Keep the original bytes and name; the backend unpacks KMZ, ZIP and GZip streams.
    const data = new FormData();
    data.append("file", file);

    const response = await api.post<UploadResponse>(`${API_URL}/file/upload`, data, {
        signal: options.signal,
        onUploadProgress: (e) => options.onProgress(e.progress ?? 0),
    });

    const warning = uniqueLines(response.data.warning ?? "");
    if (!response.data.success) throw new NothingImportedError(warning);
    return warning;
}

/**
 * Recomputes each network's observation count, dwell time and range, which imports don't update.
 * The server does it when the WiGLE app polls its upload status after uploading, so this polls too.
 */
export async function updateNetworkStatistics(signal?: AbortSignal) {
    await api.get(`${API_URL}/file/transactions`, { params: { pagestart: 0 }, signal });
}

export function uploadErrorMessage(error: unknown): string {
    if (error instanceof NothingImportedError) {
        return error.message || "The server found nothing to import in this file.";
    }
    if (axios.isAxiosError(error)) {
        if (!error.response) return "Server not reachable. Check your connection and try again.";
        switch (error.response.status) {
            case 401:
                return "Your session has expired.";
            case 413:
                return "The file is too large for the server.";
            case 429:
            case 503:
                // Uploads are accepted one at a time, from the WiGLE app as well.
                return "The server is busy with another upload. Please try again in a moment.";
            case 500:
                return "The server failed to import this file.";
        }
    }
    console.error("Upload failed", error);
    return "Upload failed. Please try again.";
}
