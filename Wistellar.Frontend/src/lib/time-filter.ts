// The time range in the filter query: `time[gt]` and `time[lt]` bound a network's last-seen time.
// The server reads each value as a duration back from now (`1d`, `2M3d`; units y M d h m), an ISO
// date (UTC unless it says otherwise) or unix seconds, in that order: see DateExtensions.TryParseDate.

import { decode, getFilterParam, setFilterParam } from "./filter-query.ts";

const GT = "time[gt]";
const LT = "time[lt]";

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** `date` moved by whole months in UTC, keeping the day where the month has it (Mar 31 → Feb 28), as .NET's AddMonths does. */
function addMonths(date: Date, months: number): Date {
  const result = new Date(date);
  const day = result.getUTCDate();
  result.setUTCDate(1);
  result.setUTCMonth(result.getUTCMonth() + months);
  const lastDay = new Date(Date.UTC(result.getUTCFullYear(), result.getUTCMonth() + 1, 0)).getUTCDate();
  result.setUTCDate(Math.min(day, lastDay));
  return result;
}

/** A filter time value as the server reads it, or `undefined` where the server would ignore it. */
export function parseFilterTime(value: string | undefined, now: Date): Date | undefined {
  if (!value) return undefined;
  const text = decode(value).trim();

  const units = [...text.matchAll(/(\d+)([yMdhm])/g)];
  if (units.length > 0) {
    let date = new Date(now);
    for (const [, count, unit] of units) {
      const n = Number(count);
      if (unit === "y") date = addMonths(date, -12 * n);
      else if (unit === "M") date = addMonths(date, -n);
      else date = new Date(date.getTime() - n * { d: DAY, h: HOUR, m: MINUTE }[unit as "d" | "h" | "m"]);
    }
    return date;
  }

  if (/^\d{4}-\d{2}-\d{2}/.test(text)) {
    // JavaScript reads a date and time without an offset as local time; the server takes UTC.
    const withZone = /T.*(Z|[+-]\d{2}:?\d{2})$/i.test(text) || !text.includes("T") ? text : text + "Z";
    const time = Date.parse(withZone);
    return Number.isNaN(time) ? undefined : new Date(time);
  }

  if (/^\d+$/.test(text)) return new Date(Number(text) * 1000);
  return undefined;
}

/** The time range the filter selects, if it has a usable lower bound. Without `time[lt]` it ends now. */
export function timeRangeFromFilter(filter: string, now: Date): { from: Date; to: Date } | undefined {
  const from = parseFilterTime(getFilterParam(filter, GT), now);
  const to = parseFilterTime(getFilterParam(filter, LT), now) ?? now;
  return from && from < to ? { from, to } : undefined;
}

/** The filter's time parameters as one string, to tell whether they changed. */
export function timeParamsOf(filter: string): string {
  return `${getFilterParam(filter, GT) ?? ""}&${getFilterParam(filter, LT) ?? ""}`;
}


/**
 * `from` as a duration before `to`, in the largest unit that fits exactly: "Last 12 months" is
 * `1y`, "Last 30 days" `30d`. Calendar units allow for a daylight-saving change on the way.
 */
export function durationBefore(to: Date, from: Date): string | undefined {
  const span = to.getTime() - from.getTime();
  if (span < MINUTE) return undefined;

  // The slider counts days and months in local time, so across a daylight-saving change its
  // "Last 7 days" is an hour longer or shorter than 7 × 24 hours.
  const dst = from.getTimezoneOffset() !== to.getTimezoneOffset();
  const sameOrDst = (a: number, b: number) => a === b || (dst && Math.abs(a - b) === HOUR);

  const years = Math.round(span / (365.2425 * DAY));
  if (years > 0 && sameOrDst(addMonths(to, -12 * years).getTime(), from.getTime())) return `${years}y`;

  const days = Math.round(span / DAY);
  if (days > 0 && sameOrDst(days * DAY, span)) return `${days}d`;

  const months = Math.round(span / (30.436875 * DAY));
  if (months > 0 && sameOrDst(addMonths(to, -months).getTime(), from.getTime())) return `${months}M`;

  if (span % HOUR === 0) return `${span / HOUR}h`;
  return `${Math.round(span / MINUTE)}m`;
}

const iso = (date: Date) => date.toISOString().replace(/\.000Z$/, "Z");

/**
 * The filter with its time range replaced. A range that ends now ("Last 24 hours") becomes a
 * duration, `time[gt]=1d` and no `time[lt]`, so it still means the last day when the installed
 * app starts tomorrow. Any other range is written as two ISO times in UTC.
 */
export function filterWithTimeRange(filter: string, from: Date, to: Date, now: Date): string {
  const endsNow = Math.abs(to.getTime() - now.getTime()) <= 2 * MINUTE;
  const duration = endsNow ? durationBefore(to, from) : undefined;
  if (duration) return setFilterParam(setFilterParam(filter, GT, duration), LT, undefined);
  return setFilterParam(setFilterParam(filter, GT, iso(from)), LT, iso(to));
}
