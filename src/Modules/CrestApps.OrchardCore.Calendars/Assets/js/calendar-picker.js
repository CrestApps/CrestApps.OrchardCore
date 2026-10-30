// @ts-check
// Lets a page ask the user to choose a time on their own calendar. The page renders the CalendarPicker shape and calls
// CrestAppsCalendarPicker.open(): a modal shows the user's schedule (their calendars and the events they attend) so they
// can see when they're free, and resolves with the time they click or drag across, or null when they close it.

(function () {
    /**
     * Returns the wall-clock time of an instant in a time zone, as a local Date with those components, which is what date
     * inputs that work in that zone expect.
     * @param {Date} instant
     * @param {string} timeZone
     * @returns {Date}
     */
    const toWallClock = (instant, timeZone) => {
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
    };

    /**
     * Opens the picker.
     * @param {{ date?: Date | null, durationMinutes?: number, title?: string }} [options]
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
        const initial = options && options.date ? options.date : new Date();

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
                headerToolbar: { left: 'prev,next today', center: 'title', right: 'timeGridDay,timeGridWeek' },
                buttonText: { today: data.todayText, day: data.dayText, week: data.weekText },
                height: 'auto',
                nowIndicator: true,
                selectable: true,
                selectMirror: true,
                scrollTime: `${String(Math.max(0, initial.getHours() - 1)).padStart(2, '0')}:00:00`,
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

                    url.searchParams.set('start', info.start.toISOString());
                    url.searchParams.set('end', info.end.toISOString());
                    url.searchParams.set('schedule', data.userId);

                    fetch(url.toString(), { credentials: 'same-origin', headers: { Accept: 'application/json' } })
                        .then((response) => (response.ok ? response.json() : []))
                        .then((items) => success([
                            ...items.map((/** @type {any} */ item) => ({
                                id: item.id,
                                title: item.title,
                                start: item.start,
                                end: item.end,
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

                    chosen = { start: info.start, end, local: toWallClock(info.start, data.timeZone || '') };
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
    window.CrestAppsCalendarPicker = { open, toWallClock };
})();
