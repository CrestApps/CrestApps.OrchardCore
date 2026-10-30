// @ts-check
// Draws the calendar view with FullCalendar: loads the occurrences of the calendars ticked in the side list, opens an
// occurrence's details when it is clicked, moves it when it is dragged or resized, and starts a new event from a selection.

/**
 * Reads the localized labels the page renders in its template.
 * @returns {Record<string, string>}
 */
function readLabels() {
    /** @type {Record<string, string>} */
    const labels = {};
    const template = /** @type {HTMLTemplateElement | null} */ (document.getElementById('calendar-labels'));

    if (template) {
        template.content.querySelectorAll('[data-label]').forEach((element) => {
            labels[element.getAttribute('data-label') || ''] = element.textContent || '';
        });
    }

    return labels;
}

/**
 * Builds a safe same-origin navigation URL with query parameters.
 * @param {string} candidate
 * @param {string} queryString
 * @returns {string}
 */
function toSafeNavigationUrl(candidate, queryString) {
    try {
        const url = new URL(candidate || '', window.location.origin);
        const isHttp = url.protocol === 'http:' || url.protocol === 'https:';

        if (!isHttp || url.origin !== window.location.origin) {
            return '#';
        }

        url.search = queryString;
        return url.toString();
    } catch {
        return '#';
    }
}

/**
 * Formats a date as yyyy-MM-dd in local time.
 * @param {Date} date
 * @returns {string}
 */
function toDateString(date) {
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');

    return `${date.getFullYear()}-${month}-${day}`;
}

/**
 * Adds a term and its value to a definition list.
 * @param {HTMLElement} list
 * @param {string} term
 * @param {string} value
 */
function addDetail(list, term, value) {
    if (!value) {
        return;
    }

    const dt = document.createElement('dt');
    const dd = document.createElement('dd');

    dt.className = 'col-sm-4';
    dd.className = 'col-sm-8';
    dt.textContent = term;
    dd.textContent = value;
    list.append(dt, dd);
}

/**
 * Shows a Bootstrap modal.
 * @param {HTMLElement} element
 * @returns {any}
 */
function showModal(element) {
    // @ts-ignore Bootstrap is loaded by the admin theme.
    const modal = window.bootstrap.Modal.getOrCreateInstance(element);

    modal.show();

    return modal;
}

/**
 * Asks which occurrences of a repeating event to move.
 * @returns {Promise<string>} The scope, or an empty string when the move is cancelled.
 */
function askScope() {
    const element = /** @type {HTMLElement} */ (document.getElementById('calendar-scope-modal'));

    return new Promise((resolve) => {
        const modal = showModal(element);
        let answered = false;

        /** @param {Event} event */
        const onClick = (event) => {
            const target = /** @type {HTMLElement} */ (event.target);
            const scope = target.closest('[data-scope]');

            if (!scope) {
                return;
            }

            answered = true;
            element.removeEventListener('click', onClick);
            modal.hide();
            resolve(scope.getAttribute('data-scope') || '');
        };

        element.addEventListener('click', onClick);
        element.addEventListener('hidden.bs.modal', () => {
            element.removeEventListener('click', onClick);

            if (!answered) {
                resolve('');
            }
        }, { once: true });
    });
}

function initialize() {
    const root = document.getElementById('calendar-view');
    // @ts-ignore FullCalendar's global build sets window.FullCalendar.
    const FullCalendar = window.FullCalendar;

    if (!root || !FullCalendar) {
        return;
    }

    const config = JSON.parse(root.getAttribute('data-config') || '{}');
    const labels = readLabels();
    const tokenInput = /** @type {HTMLInputElement | null} */ (document.querySelector('#calendar-antiforgery input[name="__RequestVerificationToken"]'));
    const storageKey = 'crestapps-calendar-selection';
    const toggles = /** @type {HTMLInputElement[]} */ (Array.from(document.querySelectorAll('[data-calendar-toggle]')));
    const hasRequestedCalendars = new URLSearchParams(window.location.search).has('calendars');

    if (!hasRequestedCalendars) {
        try {
            const saved = JSON.parse(window.localStorage.getItem(storageKey) || 'null');

            if (Array.isArray(saved)) {
                toggles.forEach((toggle) => {
                    toggle.checked = saved.includes(toggle.value);
                });
            }
        } catch {
            // Storage can be unavailable; the server's selection stands.
        }
    }

    const selectedCalendars = () => toggles.filter((toggle) => toggle.checked).map((toggle) => toggle.value);
    const writable = new Set(config.writableCalendars || []);

    const calendar = new FullCalendar.Calendar(root.querySelector('[data-calendar]'), {
        initialView: config.initialView,
        initialDate: config.initialDate,
        locale: config.locale,
        headerToolbar: {
            left: 'prev,next today',
            center: 'title',
            right: 'timeGridDay,timeGridWeek,dayGridMonth,listWeek',
        },
        buttonText: {
            today: labels.today,
            day: labels.day,
            week: labels.week,
            month: labels.month,
            list: labels.agenda,
        },
        height: 'auto',
        nowIndicator: true,
        navLinks: true,
        dayMaxEvents: true,
        selectable: writable.size > 0,
        selectMirror: true,
        editable: true,
        eventStartEditable: true,
        eventDurationEditable: true,
        /**
         * @param {any} info
         * @param {(events: any[]) => void} success
         * @param {(error: any) => void} failure
         */
        events: (info, success, failure) => {
            const calendars = selectedCalendars();

            if (calendars.length === 0) {
                success([]);

                return;
            }

            const url = new URL(config.occurrencesUrl, window.location.origin);

            url.searchParams.set('start', info.start.toISOString());
            url.searchParams.set('end', info.end.toISOString());
            url.searchParams.set('calendars', calendars.join(','));

            fetch(url.toString(), { credentials: 'same-origin', headers: { Accept: 'application/json' } })
                .then((response) => {
                    if (!response.ok) {
                        throw new Error(String(response.status));
                    }

                    return response.json();
                })
                .then((items) => success(items.map((/** @type {any} */ item) => ({
                    id: item.id,
                    title: item.title,
                    start: item.start,
                    end: item.end,
                    allDay: item.allDay,
                    color: item.color,
                    editable: item.editable,
                    extendedProps: {
                        eventId: item.eventId,
                        calendarId: item.calendarId,
                        details: item.details || {},
                    },
                }))))
                .catch(failure);
        },
        /** @param {any} info */
        eventClick: (info) => {
            info.jsEvent.preventDefault();

            const details = info.event.extendedProps.details || {};
            const modal = /** @type {HTMLElement} */ (document.getElementById('calendar-event-modal'));
            const list = /** @type {HTMLElement} */ (modal.querySelector('[data-event-details]'));
            const edit = /** @type {HTMLAnchorElement} */ (modal.querySelector('[data-event-edit]'));
            const title = /** @type {HTMLElement} */ (modal.querySelector('[data-event-title]'));

            title.textContent = info.event.title;
            list.replaceChildren();

            if (details.busyOnly) {
                addDetail(list, '', labels.busy);
            }

            addDetail(list, labels.calendar, details.calendar);
            addDetail(list, labels.type, details.eventType);
            addDetail(list, labels.location, details.location);
            addDetail(list, labels.status, details.status);
            addDetail(list, labels.attendees, (details.attendees || []).join(', '));
            addDetail(list, labels.description, details.description);
            addDetail(list, labels.source, details.source);

            if (details.editUrl) {
                edit.href = details.editUrl;
                edit.classList.remove('d-none');
            } else {
                edit.classList.add('d-none');
            }

            showModal(modal);
        },
        /** @param {any} info */
        select: (info) => {
            const params = new URLSearchParams();
            const shown = selectedCalendars().filter((id) => writable.has(id));

            params.set('allDay', info.allDay ? 'true' : 'false');

            if (info.allDay) {
                params.set('start', toDateString(info.start));
                params.set('end', toDateString(info.end));
            } else {
                params.set('start', info.start.toISOString());
                params.set('end', info.end.toISOString());
            }

            if (shown.length === 1) {
                params.set('calendarId', shown[0]);
                window.location.href = toSafeNavigationUrl(config.createUrl, params.toString());
            } else {
                window.location.href = toSafeNavigationUrl(config.chooseUrl, params.toString());
            }
        },
        eventDrop: (/** @type {any} */ info) => move(info),
        eventResize: (/** @type {any} */ info) => move(info),
    });

    /**
     * Saves a dragged or resized occurrence, asking which occurrences to move for a repeating event.
     * @param {any} info
     */
    async function move(info) {
        const details = info.event.extendedProps.details || {};
        let scope = 'ThisOccurrence';

        if (details.recurring && details.originalStart) {
            scope = await askScope();

            if (!scope) {
                info.revert();

                return;
            }
        }

        const allDay = info.event.allDay;
        const body = {
            eventId: info.event.extendedProps.eventId,
            originalStartUtc: details.recurring ? details.originalStart : null,
            start: allDay ? toDateString(info.event.start) : info.event.start.toISOString(),
            end: info.event.end ? (allDay ? toDateString(info.event.end) : info.event.end.toISOString()) : null,
            allDay,
            scope,
        };

        try {
            const response = await fetch(config.moveUrl, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    Accept: 'application/json',
                    RequestVerificationToken: tokenInput ? tokenInput.value : '',
                },
                body: JSON.stringify(body),
            });

            if (!response.ok) {
                const result = await response.json().catch(() => ({}));

                info.revert();
                window.alert((result.errors || [labels.moveFailed]).join('\n'));

                return;
            }
        } catch {
            info.revert();
            window.alert(labels.moveFailed);

            return;
        }

        calendar.refetchEvents();
    }

    toggles.forEach((toggle) => toggle.addEventListener('change', () => {
        try {
            window.localStorage.setItem(storageKey, JSON.stringify(selectedCalendars()));
        } catch {
            // Storage can be unavailable; the selection then lasts only for this page.
        }

        calendar.refetchEvents();
    }));

    calendar.render();
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initialize);
} else {
    initialize();
}
