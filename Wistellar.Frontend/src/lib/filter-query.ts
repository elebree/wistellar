// The filter query's `name=value` pairs, read and changed one parameter at a time. Names and values
// stay as typed: `ssid=_%` must not be URL-decoded.

export function decode(text: string) {
  try {
    return decodeURIComponent(text);
  } catch {
    return text;
  }
}

function split(filter: string): [string, string][] {
  return filter
    .split("&")
    .filter((part) => part !== "")
    .map((part) => {
      const i = part.indexOf("=");
      return i < 0 ? [part, ""] : [part.slice(0, i), part.slice(i + 1)];
    });
}

/**
 * The filter as a URL query, every name and value percent-encoded. The values are plain text, so a
 * `%` in a pattern has to reach the server as a `%`: sent as it is, the `%be` in `%beacon%` would
 * arrive as an escaped byte.
 */
export function toQueryString(filter: string): string {
  return filterParams(filter)
    .map(([name, value]) => `${encodeURIComponent(name)}=${encodeURIComponent(value)}`)
    .join("&");
}

/** The parameters as name and value pairs: the names decoded, the values as typed. */
export function filterParams(filter: string): [string, string][] {
  return split(filter).map(([key, value]) => [decode(key), value]);
}

export function getFilterParam(filter: string, name: string): string | undefined {
  return split(filter).find(([key]) => decode(key) === name)?.[1];
}

/** Sets or, with `undefined`, removes a parameter, keeping the others and their order. */
export function setFilterParam(filter: string, name: string, value: string | undefined): string {
  const params = split(filter);
  const at = params.findIndex(([key]) => decode(key) === name);
  const rest = params.filter(([key]) => decode(key) !== name);
  if (value !== undefined) rest.splice(at < 0 ? rest.length : at, 0, [name, value]);
  return rest.map(([key, v]) => `${key}=${v}`).join("&");
}
