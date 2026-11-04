// @ts-check
// Lets a page ask the user to choose a time on their own calendar. The page renders the CalendarPicker shape and calls
// CrestAppsCalendarPicker.open(): a modal shows the user's schedule (their calendars and the events they attend) so they
// can see when they're free, and resolves with the time they click or drag across, or null when they close it. The
// calendar is drawn in the user's time zone, not the browser's, so the time they click is the time they get.

import {
    browserTimeZone,
    calendarDateToInstant,
    fromCalendarDate,
    fromWallClock,
    instantToCalendarDate,
    toCalendarDate,
    toWallClock,
} from './shared/calendar-time.js';

(function () {
    /**
     * Opens the picker.
     * @param {{ date?: Date | null, durationMinutes?: number, title?: string }} [options] The date is the wall-clock time
     * to show as chosen, in the user's time zone (what a date input in that zone holds), like the local time it returns.
     * @returns {Promise<{ start: Date, end: Date, local: Date } | null>} The chosen instant, its end, and its wall-clock
     * time in the user's time zone; or null when the user closes the picker.
     */
    const open = (options) => {
        const settings = /** @type {HTMLElement | null} */ (document.querySelector('[data-calendar-picker]'));

        // @ts-ignore FullCalendar and Bootstrap are loaded by the page.
        if (!settings || !window.FullCalendar || !window.bootstrap) {
            return Promise.resolve(null);
        }

        const data = settings.dataset;
        const durationMinutes = options && options.durationMinutes ? options.durationMinutes : 15;
        const browserZone = browserTimeZone();
        const timeZone = data.timeZone || browserZone;

        /** @param {Date} instant */
        const toZoneCalendarDate = (instant) => instantToCalendarDate(instant, timeZone);

        // Everything the calendar shows, the chosen time included, is a wall-clock time in the user's time zone.
        const initialLocal = options && options.date ? options.date : toWallClock(new Date(), timeZone);
        const initial = toCalendarDate(initialLocal);

        const modal = document.createElement('div');

        modal.className = 'modal fade';
        modal.tabIndex = -1;
        modal.setAttribute('aria-hidden', 'true');

        const dialog = document.createElement('div');
        const content = document.createElement('div');
        const header = document.createElement('div');
        const title = document.createElement('h5');
        const close = document.createElement('button');
        const body = document.createElement('div');
        const hint = document.createElement('p');
        const host = document.createElement('div');

        dialog.className = 'modal-dialog modal-xl modal-dialog-scrollable modal-fullscreen-md-down';
        content.className = 'modal-content';
        header.className = 'modal-header';
        title.className = 'modal-title';
        title.textContent = (options && options.title) || data.titleText || '';
        close.type = 'button';
        close.className = 'btn-close';
        close.setAttribute('data-bs-dismiss', 'modal');
        close.setAttribute('aria-label', data.closeText || '');
        body.className = 'modal-body';
        body.id = 'calendar-picker-modal';
        hint.className = 'text-muted small';
        hint.textContent = data.hintText || '';

        // The times are the user's, which aren't the computer's when they're away; say which they are.
        if (timeZone && browserZone && timeZone !== browserZone) {
            hint.textContent += ` ${(data.zoneText || '').replace('{0}', timeZone)}`;
        }

        header.append(title, close);
        body.append(hint, host);
        content.append(header, body);
        dialog.append(content);
        modal.append(dialog);
        document.body.append(modal);

        return new Promise((resolve) => {
            /** @type {{ start: Date, end: Date, local: Date } | null} */
            let chosen = null;

            // @ts-ignore
            const bootstrapModal = window.bootstrap.Modal.getOrCreateInstance(modal);

            // @ts-ignore
            const calendar = new window.FullCalendar.Calendar(host, {
                initialView: host.clientWidth < 576 || window.innerWidth < 576 ? 'timeGridDay' : 'timeGridWeek',
                initialDate: initial,
                locale: data.locale || undefined,
                timeZone: 'UTC',
                now: () => toZoneCalendarDate(new Date()),
                headerToolbar: { left: 'prev,next today', center: 'title', right: 'timeGridDay,timeGridWeek' },
                headerToolbarClass: 'calendar-fc-toolbar',
                todayText: data.todayText,
                dayText: data.dayText,
                weekTextLong: data.weekText,
                // A fixed height keeps the toolbar in view and lets the calendar open at the chosen time.
                height: Math.max(360, Math.round(window.innerHeight * 0.65)),
                nowIndicator: true,
                selectable: true,
                selectMirror: true,
                scrollTime: `${String(Math.max(0, initialLocal.getHours() - 1)).padStart(2, '0')}:00:00`,
                slotDuration: '00:15:00',
                snapDuration: '00:15:00',
                /**
                 * @param {any} info
                 * @param {(events: any[]) => void} success
                 * @param {(error: any) => void} failure
                 */
                events: (info, success, failure) => {
                    const url = new URL(data.occurrencesUrl || '', window.location.origin);

                    if (url.origin !== window.location.origin || !data.userId) {
                        success([]);

                        return;
                    }

                    // The range is in the user's time zone; a day either side covers any offset from UTC.
                    url.searchParams.set('start', new Date(info.start.getTime() - 86400000).toISOString());
                    url.searchParams.set('end', new Date(info.end.getTime() + 86400000).toISOString());
                    url.searchParams.set('schedule', data.userId);

                    fetch(url.toString(), { credentials: 'same-origin', headers: { Accept: 'application/json' } })
                        .then((response) => (response.ok ? response.json() : []))
                        .then((items) => success([
                            ...items.map((/** @type {any} */ item) => ({
                                id: item.id,
                                title: item.title,
                                start: item.allDay ? item.start : toZoneCalendarDate(new Date(item.start)),
                                end: item.allDay || !item.end ? item.end : toZoneCalendarDate(new Date(item.end)),
                                allDay: item.allDay,
                                color: item.color,
                                editable: false,
                            })),
                            {
                                id: 'calendar-picker-choice',
                                title: data.selectedText,
                                start: initial,
                                end: new Date(initial.getTime() + durationMinutes * 60000),
                                display: 'background',
                                color: '#0d6efd',
                            },
                        ]))
                        .catch(failure);
                },
                /** @param {any} info */
                select: (info) => {
                    if (info.allDay) {
                        calendar.unselect();

                        return;
                    }

                    const end = info.end && info.end > info.start ? info.end : new Date(info.start.getTime() + durationMinutes * 60000);

                    const local = fromCalendarDate(info.start);

                    chosen = { start: fromWallClock(local, timeZone), end: calendarDateToInstant(end, timeZone), local };
                    bootstrapModal.hide();
                },
            });

            modal.addEventListener('shown.bs.modal', () => calendar.render(), { once: true });
            modal.addEventListener('hidden.bs.modal', () => {
                calendar.destroy();
                modal.remove();
                resolve(chosen);
            }, { once: true });

            bootstrapModal.show();
        });
    };

    // @ts-ignore
    window.CrestAppsCalendarPicker = { open, toWallClock, fromWallClock };
})();
