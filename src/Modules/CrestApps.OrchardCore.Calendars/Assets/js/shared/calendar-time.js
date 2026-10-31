// @ts-check
// Draws FullCalendar in a time zone without a time zone plugin. FullCalendar is set to UTC and given "calendar dates":
// Dates whose UTC components are the wall-clock time in the zone, so it shows them as they are. These helpers turn
// instants and wall-clock times into calendar dates and back.

/**
 * Returns the browser's IANA time zone, or an empty string when it is unknown.
 * @returns {string}
 */
export function browserTimeZone() {
    try {
        return Intl.DateTimeFormat().resolvedOptions().timeZone || '';
    } catch {
        return '';
    }
}

/**
 * Returns the wall-clock time of an instant in a time zone, as a local Date with those components, which is what date
 * inputs that work in that zone expect.
 * @param {Date} instant
 * @param {string} timeZone
 * @returns {Date}
 */
export function toWallClock(instant, timeZone) {
    if (!timeZone) {
        return new Date(instant.getTime());
    }

    try {
        const parts = new Intl.DateTimeFormat('en-US', {
            timeZone,
            hourCycle: 'h23',
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
        }).formatToParts(instant);

        /** @param {string} type */
        const part = (type) => Number((parts.find((item) => item.type === type) || { value: '0' }).value);

        return new Date(part('year'), part('month') - 1, part('day'), part('hour'), part('minute'), part('second'));
    } catch {
        return new Date(instant.getTime());
    }
}

/**
 * Returns a wall-clock time as a calendar date: a Date whose UTC components are that time.
 * @param {Date} local A local Date whose components are the wall-clock time.
 * @returns {Date}
 */
export function toCalendarDate(local) {
    return new Date(Date.UTC(local.getFullYear(), local.getMonth(), local.getDate(), local.getHours(), local.getMinutes(), local.getSeconds()));
}

/**
 * Returns the wall-clock time a calendar date stands for, as a local Date with those components.
 * @param {Date} value
 * @returns {Date}
 */
export function fromCalendarDate(value) {
    return new Date(value.getUTCFullYear(), value.getUTCMonth(), value.getUTCDate(), value.getUTCHours(), value.getUTCMinutes(), value.getUTCSeconds());
}

/**
 * Returns the instant of a wall-clock time in a time zone; the reverse of toWallClock.
 * @param {Date} local A local Date whose components are the wall-clock time.
 * @param {string} timeZone
 * @returns {Date}
 */
export function fromWallClock(local, timeZone) {
    if (!timeZone) {
        return new Date(local.getTime());
    }

    const wanted = toCalendarDate(local).getTime();
    let instant = wanted;

    // The zone's offset can differ on either side of a daylight saving change, so correct the guess twice.
    for (let pass = 0; pass < 2; pass++) {
        instant += wanted - toCalendarDate(toWallClock(new Date(instant), timeZone)).getTime();
    }

    return new Date(instant);
}

/**
 * Returns the calendar date of an instant in a time zone.
 * @param {Date} instant
 * @param {string} timeZone
 * @returns {Date}
 */
export function instantToCalendarDate(instant, timeZone) {
    return toCalendarDate(toWallClock(instant, timeZone));
}

/**
 * Returns the instant a calendar date stands for in a time zone.
 * @param {Date} value
 * @param {string} timeZone
 * @returns {Date}
 */
export function calendarDateToInstant(value, timeZone) {
    return fromWallClock(fromCalendarDate(value), timeZone);
}

/**
 * Returns the yyyy-MM-dd day of a calendar date.
 * @param {Date} value
 * @returns {string}
 */
export function toDayString(value) {
    return value.toISOString().slice(0, 10);
}
