// @ts-check
// Draws the Calendar dashboard widgets with FullCalendar. A dashboard can hold several, so every element marked
// data-calendar-widget gets its own calendar, showing the calendars its widget names. The widget only shows events:
// clicking one opens its page when the viewer may change it, and the widget links to the full calendar page for the rest.
// The calendar is drawn in the user's time zone (see shared/calendar-time.js), so its times match the rest of the site.

import { browserTimeZone, instantToCalendarDate } from './shared/calendar-time.js';

/**
 * Returns a same-origin URL, or an empty string for any other URL.
 * @param {string} candidate
 * @returns {string}
 */
function toSameOriginUrl(candidate) {
    try {
        const url = new URL(candidate || '', window.location.origin);

        return url.origin === window.location.origin && (url.protocol === 'http:' || url.protocol === 'https:') ? url.toString() : '';
    } catch {
        return '';
    }
}

/**
 * Draws one widget.
 * @param {HTMLElement} root
 */
function drawWidget(root) {
    // @ts-ignore FullCalendar's global build sets window.FullCalendar.
    const FullCalendar = window.FullCalendar;
    const element = /** @type {HTMLElement | null} */ (root.querySelector('[data-calendar]'));

    if (!FullCalendar || !element || root.hasAttribute('data-calendar-drawn')) {
        return;
    }

    root.setAttribute('data-calendar-drawn', '');

    const config = JSON.parse(root.getAttribute('data-config') || '{}');
    const labels = root.dataset;
    const timeZone = config.timeZone || browserTimeZone();
    const calendars = Array.isArray(config.calendars) ? config.calendars : [];

    // A widget is usually narrow; hour-by-hour views get a fixed height so they scroll instead of filling the dashboard.
    /** @param {string} view */
    const heightFor = (view) => (view.startsWith('timeGrid') ? 480 : 'auto');

    const calendar = new FullCalendar.Calendar(element, {
        initialView: config.initialView || 'listWeek',
        initialDate: config.initialDate,
        locale: config.locale,
        timeZone: 'UTC',
        now: () => instantToCalendarDate(new Date(), timeZone),
        headerToolbar: { left: 'prev,next today', center: 'title', right: 'timeGridDay,timeGridWeek,dayGridMonth,listWeek' },
        headerToolbarClass: 'calendar-fc-toolbar',
        viewClass: 'calendar-fc-view',
        todayText: labels.todayText,
        dayText: labels.dayText,
        weekTextLong: labels.weekText,
        monthText: labels.monthText,
        listText: labels.agendaText,
        noEventsText: labels.emptyText,
        height: heightFor(config.initialView || 'listWeek'),
        scrollTime: '08:00:00',
        nowIndicator: true,
        navLinks: true,
        dayMaxEvents: true,
        editable: false,
        selectable: false,
        loading: (/** @type {boolean} */ isLoading) => root.classList.toggle('calendar-loading', isLoading),
        datesSet: (/** @type {any} */ info) => {
            const height = heightFor(info.view.type);

            if (calendar.getOption('height') !== height) {
                calendar.setOption('height', height);
            }
        },
        /**
         * @param {any} info
         * @param {(events: any[]) => void} success
         * @param {(error: any) => void} failure
         */
        events: (info, success, failure) => {
            const url = new URL(config.occurrencesUrl || '', window.location.origin);

            if (url.origin !== window.location.origin || calendars.length === 0) {
                success([]);

                return;
            }

            // The range is in the user's time zone; a day either side covers any offset from UTC.
            url.searchParams.set('start', new Date(info.start.getTime() - 86400000).toISOString());
            url.searchParams.set('end', new Date(info.end.getTime() + 86400000).toISOString());
            url.searchParams.set('calendars', calendars.join(','));

            fetch(url.toString(), { credentials: 'same-origin', headers: { Accept: 'application/json' } })
                .then((response) => {
                    if (!response.ok) {
                        throw new Error(String(response.status));
                    }

                    return response.json();
                })
                .then((items) => success(items.map((/** @type {any} */ item) => {
                    const details = item.details || {};

                    return {
                        id: item.id,
                        title: details.busyOnly && !item.title ? labels.busyText : item.title,
                        start: item.allDay ? item.start : instantToCalendarDate(new Date(item.start), timeZone),
                        end: item.allDay || !item.end ? item.end : instantToCalendarDate(new Date(item.end), timeZone),
                        allDay: item.allDay,
                        color: item.color,
                        url: details.editUrl ? toSameOriginUrl(details.editUrl) || undefined : undefined,
                    };
                })))
                .catch(failure);
        },
    });

    calendar.render();
}

function initialize() {
    document.querySelectorAll('[data-calendar-widget]').forEach((root) => drawWidget(/** @type {HTMLElement} */ (root)));
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialize);
} else {
    initialize();
}
