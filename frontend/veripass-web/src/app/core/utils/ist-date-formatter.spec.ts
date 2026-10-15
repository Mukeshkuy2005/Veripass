import { formatActivityTime, formatIstDateTime, formatRelativeTime, parseUtcDate } from './ist-date-formatter';

describe('IST Date & Time Formatter', () => {
  it('correctly converts UTC timestamp to Asia/Kolkata (IST)', () => {
    // 2026-10-14T17:00:00Z -> IST is +05:30 -> 22:30 on 14 Oct 2026
    const res = formatIstDateTime('2026-10-14T17:00:00Z');
    expect(res.datePart).toContain('14');
    expect(res.datePart).toContain('Oct');
    expect(res.datePart).toContain('2026');
    expect(res.timePart).toContain('10:30');
    expect(res.timePart).toContain('PM');
    expect(res.timePart).toContain('IST');
  });

  it('correctly handles date boundary crossing into next day in IST', () => {
    // 2026-10-14T20:00:00Z -> +05:30 -> 01:30 AM on 15 Oct 2026
    const res = formatIstDateTime('2026-10-14T20:00:00Z');
    expect(res.datePart).toContain('15');
    expect(res.datePart).toContain('Oct');
    expect(res.datePart).toContain('2026');
    expect(res.timePart).toContain('01:30');
    expect(res.timePart).toContain('AM');
    expect(res.timePart).toContain('IST');
  });

  it('correctly handles UTC midnight', () => {
    // 2026-10-14T00:00:00Z -> +05:30 -> 05:30 AM on 14 Oct 2026
    const res = formatIstDateTime('2026-10-14T00:00:00Z');
    expect(res.datePart).toContain('14');
    expect(res.datePart).toContain('Oct');
    expect(res.datePart).toContain('2026');
    expect(res.timePart).toContain('05:30');
    expect(res.timePart).toContain('AM');
    expect(res.timePart).toContain('IST');
  });

  it('handles null, undefined and invalid dates gracefully', () => {
    expect(formatIstDateTime(null).datePart).toBe('—');
    expect(formatIstDateTime(undefined).timePart).toBe('—');
    expect(formatIstDateTime('invalid-date').fullFormatted).toBe('—');
  });

  describe('Live Relative Time Calculations', () => {
    const baseNow = new Date('2026-10-14T17:30:00Z');

    it('formats 30 seconds ago as Just now', () => {
      const past = new Date(baseNow.getTime() - 30 * 1000);
      expect(formatRelativeTime(past, baseNow)).toBe('Just now');
    });

    it('formats 1 minute ago', () => {
      const past = new Date(baseNow.getTime() - 65 * 1000);
      expect(formatRelativeTime(past, baseNow)).toBe('1 minute ago');
    });

    it('formats 5 minutes ago', () => {
      const past = new Date(baseNow.getTime() - 5 * 60 * 1000);
      expect(formatRelativeTime(past, baseNow)).toBe('5 minutes ago');
    });

    it('formats 59 minutes ago', () => {
      const past = new Date(baseNow.getTime() - 59 * 60 * 1000);
      expect(formatRelativeTime(past, baseNow)).toBe('59 minutes ago');
    });

    it('formats 1 hour ago', () => {
      const past = new Date(baseNow.getTime() - 65 * 60 * 1000);
      expect(formatRelativeTime(past, baseNow)).toBe('1 hour ago');
    });

    it('formats 23 hours ago', () => {
      const past = new Date(baseNow.getTime() - 23 * 3600 * 1000);
      expect(formatRelativeTime(past, baseNow)).toBe('23 hours ago');
    });

    it('formats 1 day ago as Yesterday', () => {
      const past = new Date(baseNow.getTime() - 25 * 3600 * 1000);
      expect(formatRelativeTime(past, baseNow)).toBe('Yesterday');
    });

    it('formats 3 days ago', () => {
      const past = new Date(baseNow.getTime() - 3 * 24 * 3600 * 1000);
      expect(formatRelativeTime(past, baseNow)).toBe('3 days ago');
    });

    it('formats 7 days ago', () => {
      const past = new Date(baseNow.getTime() - 7 * 24 * 3600 * 1000);
      expect(formatRelativeTime(past, baseNow)).toBe('1 week ago');
    });

    it('recalculates dynamically as reference time advances', () => {
      const past = new Date(baseNow.getTime() - 5 * 60 * 1000); // 5 min ago at baseNow
      expect(formatRelativeTime(past, baseNow)).toBe('5 minutes ago');

      // 60 seconds later
      const advancedNow = new Date(baseNow.getTime() + 60 * 1000);
      expect(formatRelativeTime(past, advancedNow)).toBe('6 minutes ago');
    });

    it('formats future dates within 1 minute as Just now', () => {
      const futureNear = new Date(baseNow.getTime() + 20 * 1000);
      expect(formatRelativeTime(futureNear, baseNow)).toBe('Just now');
    });

    it('formats future dates within hours and days', () => {
      const futureHours = new Date(baseNow.getTime() + 2 * 3600 * 1000);
      expect(formatRelativeTime(futureHours, baseNow)).toBe('In 2 hours');

      const tomorrow = new Date(baseNow.getTime() + 26 * 3600 * 1000);
      expect(formatRelativeTime(tomorrow, baseNow)).toBe('Tomorrow');

      const futureDays = new Date(baseNow.getTime() + 4 * 24 * 3600 * 1000);
      expect(formatRelativeTime(futureDays, baseNow)).toBe('In 4 days');
    });
  });

  describe('Timezone Offsets & Edge Case Formats', () => {
    it('handles negative offset (-05:00 Eastern US) correctly into IST (+05:30)', () => {
      // 2026-10-14T10:00:00-05:00 is 15:00:00 UTC -> 20:30:00 IST on 14 Oct 2026
      const res = formatIstDateTime('2026-10-14T10:00:00-05:00');
      expect(res.datePart).toContain('14');
      expect(res.datePart).toContain('Oct');
      expect(res.datePart).toContain('2026');
      expect(res.timePart).toContain('08:30');
      expect(res.timePart).toContain('PM');
      expect(res.timePart).toContain('IST');
    });

    it('handles explicit positive IST offset (+05:30)', () => {
      const res = formatIstDateTime('2026-10-14T20:30:00+05:30');
      expect(res.datePart).toContain('14');
      expect(res.datePart).toContain('Oct');
      expect(res.timePart).toContain('08:30');
      expect(res.timePart).toContain('PM');
      expect(res.timePart).toContain('IST');
    });

    it('handles SQL space-separated datetime string without offset', () => {
      const res = formatIstDateTime('2026-10-14 17:00:00');
      expect(res.datePart).toContain('14');
      expect(res.timePart).toContain('10:30');
      expect(res.timePart).toContain('PM');
    });
  });

  describe('formatActivityTime: Relative vs Absolute Switching', () => {
    const baseNow = new Date('2026-10-15T12:00:00Z');

    it('returns relative time for recent items (< 7 days)', () => {
      const twoHoursAgo = new Date(baseNow.getTime() - 2 * 3600 * 1000);
      const res = formatActivityTime(twoHoursAgo, baseNow);
      expect(res.relativeOrDate).toBe('2 hours ago');
      expect(res.exactDateTime).toContain('IST');
    });

    it('returns absolute date for older items (>= 7 days)', () => {
      const eightDaysAgo = new Date(baseNow.getTime() - 8 * 24 * 3600 * 1000);
      const res = formatActivityTime(eightDaysAgo, baseNow);
      expect(res.relativeOrDate).toContain('Oct');
      expect(res.relativeOrDate).toContain('2026');
      expect(res.exactDateTime).toContain('IST');
    });

    it('returns absolute date for far future items (>= 7 days)', () => {
      const tenDaysFuture = new Date(baseNow.getTime() + 10 * 24 * 3600 * 1000);
      const res = formatActivityTime(tenDaysFuture, baseNow);
      expect(res.relativeOrDate).toContain('Oct');
      expect(res.relativeOrDate).toContain('2026');
      expect(res.exactDateTime).toContain('IST');
    });

    it('safely handles null, undefined, and invalid values in formatActivityTime', () => {
      expect(formatActivityTime(null).relativeOrDate).toBe('—');
      expect(formatActivityTime(null).exactDateTime).toBe('Timestamp unavailable');

      expect(formatActivityTime(undefined).relativeOrDate).toBe('—');
      expect(formatActivityTime(undefined).exactDateTime).toBe('Timestamp unavailable');

      expect(formatActivityTime('not-a-valid-date').relativeOrDate).toBe('—');
      expect(formatActivityTime('not-a-valid-date').exactDateTime).toBe('Timestamp unavailable');
    });
  });
});
