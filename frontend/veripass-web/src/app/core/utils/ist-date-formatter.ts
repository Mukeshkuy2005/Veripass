/**
 * Authoritative IST (Asia/Kolkata) Date & Time Formatter and Live Relative Time Utility
 * Conforms strictly to V7 Verification History requirements:
 * - Real UTC timestamps converted to Asia/Kolkata without manual 5:30 arithmetic.
 * - Absolute timestamp formatting (e.g., "14 Oct 2026", "10:42 PM IST").
 * - Live relative time calculations (e.g., "8 minutes ago", "Yesterday", "3 days ago").
 */

const IST_TIMEZONE = 'Asia/Kolkata';

// Date formatter for Indian Standard Time (Day Mon Year)
const istDateFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: IST_TIMEZONE,
  day: '2-digit',
  month: 'short',
  year: 'numeric'
});

// Time formatter for Indian Standard Time (hh:mm AM/PM)
const istTimeFormatter = new Intl.DateTimeFormat('en-US', {
  timeZone: IST_TIMEZONE,
  hour: '2-digit',
  minute: '2-digit',
  hour12: true
});

export interface FormattedIstDateTime {
  datePart: string;     // e.g., "14 Oct 2026"
  timePart: string;     // e.g., "10:42 PM IST"
  fullFormatted: string;// e.g., "14 Oct 2026, 10:42 PM IST"
  rawDate: Date | null;
}

/**
 * Parses any date representation into a valid Date object.
 * Appends 'Z' if missing on an ISO string without timezone offset.
 */
export function parseUtcDate(value: string | Date | null | undefined): Date | null {
  if (!value) return null;
  if (value instanceof Date) {
    return isNaN(value.getTime()) ? null : value;
  }

  let str = String(value).trim();
  if (!str) return null;

  // If space-separated datetime without offset (e.g. "2026-10-15 14:30:00"), normalize to ISO
  if (/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(\.\d+)?$/.test(str)) {
    str = str.replace(' ', 'T') + 'Z';
  } else if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?$/.test(str)) {
    // ISO string without offset or Z, assume UTC
    str += 'Z';
  }

  const d = new Date(str);
  return isNaN(d.getTime()) ? null : d;
}

/**
 * Formats a UTC timestamp into Asia/Kolkata date and time representations.
 */
export function formatIstDateTime(value: string | Date | null | undefined): FormattedIstDateTime {
  const d = parseUtcDate(value);
  if (!d) {
    return {
      datePart: '—',
      timePart: '—',
      fullFormatted: '—',
      rawDate: null
    };
  }

  try {
    const datePart = istDateFormatter.format(d);
    const timePartRaw = istTimeFormatter.format(d);
    const timePart = `${timePartRaw} IST`;
    const fullFormatted = `${datePart}, ${timePart}`;

    return {
      datePart,
      timePart,
      fullFormatted,
      rawDate: d
    };
  } catch {
    return {
      datePart: '—',
      timePart: '—',
      fullFormatted: '—',
      rawDate: null
    };
  }
}

/**
 * Formats relative time from a given UTC timestamp relative to referenceTime (default: new Date()).
 * Example outputs: "Just now", "2 minutes ago", "1 hour ago", "Yesterday", "3 days ago", "In 2 hours", "Tomorrow".
 */
export function formatRelativeTime(
  value: string | Date | null | undefined,
  referenceTime: Date = new Date()
): string {
  const d = parseUtcDate(value);
  if (!d) return '';

  const diffMs = referenceTime.getTime() - d.getTime();

  // Future-dated handling
  if (diffMs < 0) {
    const futureMs = -diffMs;
    // Up to 60 seconds in future: treat as just now / clock skew
    if (futureMs < 60000) {
      return 'Just now';
    }
    const futureSec = Math.floor(futureMs / 1000);
    const futureMin = Math.floor(futureSec / 60);
    if (futureMin < 60) {
      return futureMin === 1 ? 'In 1 minute' : `In ${futureMin} minutes`;
    }
    const futureHours = Math.floor(futureMin / 60);
    if (futureHours < 24) {
      return futureHours === 1 ? 'In 1 hour' : `In ${futureHours} hours`;
    }
    const futureDays = Math.floor(futureHours / 24);
    if (futureDays === 1) {
      return 'Tomorrow';
    }
    if (futureDays < 7) {
      return `In ${futureDays} days`;
    }
    return formatIstDateTime(d).datePart;
  }

  // Past handling
  const diffSeconds = Math.floor(diffMs / 1000);
  if (diffSeconds < 45) {
    return 'Just now';
  }

  const diffMinutes = Math.floor(diffSeconds / 60);
  if (diffMinutes < 60) {
    return diffMinutes === 1 ? '1 minute ago' : `${diffMinutes} minutes ago`;
  }

  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) {
    return diffHours === 1 ? '1 hour ago' : `${diffHours} hours ago`;
  }

  const diffDays = Math.floor(diffHours / 24);
  if (diffDays === 1) {
    return 'Yesterday';
  }
  if (diffDays < 7) {
    return `${diffDays} days ago`;
  }
  if (diffDays < 30) {
    const weeks = Math.floor(diffDays / 7);
    return weeks === 1 ? '1 week ago' : `${weeks} weeks ago`;
  }

  const months = Math.floor(diffDays / 30);
  return months === 1 ? '1 month ago' : `${months} months ago`;
}

export interface ActivityTimeDisplay {
  relativeOrDate: string;
  exactDateTime: string;
}

/**
 * Standardized activity timestamp formatter:
 * - Returns relative time ("Just now", "2 minutes ago", "3 hours ago", "4 days ago") for items under 7 days.
 * - Returns formatted full date ("14 Oct 2026") for items older than 7 days or far future.
 * - Always provides the exact Asia/Kolkata date & time string for the tooltip.
 * - Safely handles null, undefined, or invalid timestamps.
 */
export function formatActivityTime(
  value: string | Date | null | undefined,
  referenceTime: Date = new Date()
): ActivityTimeDisplay {
  const d = parseUtcDate(value);
  if (!d) {
    return {
      relativeOrDate: '—',
      exactDateTime: 'Timestamp unavailable'
    };
  }

  const full = formatIstDateTime(d);
  const diffMs = referenceTime.getTime() - d.getTime();
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  // If item is >= 7 days in the past or >= 7 days in the future, display absolute date
  if (diffDays >= 7 || diffDays <= -7) {
    return {
      relativeOrDate: full.datePart,
      exactDateTime: full.fullFormatted
    };
  }

  return {
    relativeOrDate: formatRelativeTime(d, referenceTime),
    exactDateTime: full.fullFormatted
  };
}
