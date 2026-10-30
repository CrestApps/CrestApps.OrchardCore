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
 * Returns a same-origin URL of an event page asking for its modal form, or an empty string for any other URL.
 * @param {string} candidate
 * @returns {string}
 */
function toModalUrl(candidate) {
    try {
        const url = new URL(candidate || '', window.location.origin);

        if (url.origin !== window.location.origin) {
            return '';
        }

        url.searchParams.set('modal', 'true');

        return url.toString();
    } catch {
        return '';
    }
}

/**
 * Puts server-rendered editor HTML into an element. Scripts the page has already loaded are skipped; every other script is
 * recreated so the browser runs it, in order.
 * @param {HTMLElement} target
 * @param {string} html
 */
function renderEditor(target, html) {
    const parsed = new DOMParser().parseFromString(html, 'text/html');
    const loaded = new Set(Array.from(document.querySelectorAll('script[src]')).map((script) => script.getAttribute('src')));
    const scripts = Array.from(parsed.body.querySelectorAll('script'));

    scripts.forEach((script) => script.remove());
    parsed.head.querySelectorAll('link[rel="stylesheet"]').forEach((link) => {
        if (!document.querySelector(`link[rel="stylesheet"][href="${CSS.escape(link.getAttribute('href') || '')}"]`)) {
            document.head.append(document.importNode(link, true));
        }
    });
    target.replaceChildren(...Array.from(parsed.body.childNodes).map((node) => document.importNode(node, true)));

    scripts.forEach((script) => {
        const source = script.getAttribute('src');

        if (source && loaded.has(source)) {
            return;
        }

        const runnable = document.createElement('script');

        if (source) {
            runnable.src = source;
            runnable.async = false;
        } else {
            runnable.textContent = script.textContent;
        }

        target.append(runnable);
    });
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

    const element = /** @type {HTMLElement} */ (root.querySelector('[data-calendar]'));

    // Below this width the calendar shows one day at a time, with a compact toolbar whose view buttons move to the bottom.
    const narrowWidth = 576;
    const isNarrow = () => element.clientWidth < narrowWidth;

    /**
     * Returns the toolbars for a wide or a narrow calendar.
     * @param {boolean} narrow
     */
    const toolbars = (narrow) => narrow
        ? {
            headerToolbar: { left: 'prev,next', center: 'title', right: 'today' },
            footerToolbar: { center: 'timeGridDay,dayGridMonth,listWeek' },
        }
        : {
            headerToolbar: { left: 'prev,next today', center: 'title', right: 'timeGridDay,timeGridWeek,dayGridMonth,listWeek' },
            footerToolbar: false,
        };

    let narrow = isNarrow();

    root.classList.toggle('calendar-narrow', narrow);

    const calendar = new FullCalendar.Calendar(element, {
        // A week of seven columns can't be read on a phone, so a narrow calendar opens on the day instead.
        initialView: narrow && config.initialView === 'timeGridWeek' ? 'timeGridDay' : config.initialView,
        initialDate: config.initialDate,
        locale: config.locale,
        ...toolbars(narrow),
        views: {
            timeGridWeek: { dayHeaderFormat: { weekday: 'short', day: 'numeric' } },
            dayGridMonth: { dayHeaderFormat: { weekday: narrow ? 'narrow' : 'short' } },
        },
        loading: (/** @type {boolean} */ isLoading) => root.classList.toggle('calendar-loading', isLoading),
        windowResize: () => {
            const nowNarrow = isNarrow();

            if (nowNarrow === narrow) {
                return;
            }

            narrow = nowNarrow;
            root.classList.toggle('calendar-narrow', narrow);

            const layout = toolbars(narrow);

            calendar.setOption('headerToolbar', layout.headerToolbar);
            calendar.setOption('footerToolbar', layout.footerToolbar);

            if (narrow && calendar.view.type === 'timeGridWeek') {
                calendar.changeView('timeGridDay');
            }
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

            // A person's or everyone's schedule comes from their calendars and the events they attend, not the ticked list.
            if (!config.schedule && calendars.length === 0) {
                success([]);

                return;
            }

            const url = new URL(config.occurrencesUrl, window.location.origin);

            url.searchParams.set('start', info.start.toISOString());
            url.searchParams.set('end', info.end.toISOString());

            if (config.schedule) {
                url.searchParams.set('schedule', config.schedule);
            } else {
                url.searchParams.set('calendars', calendars.join(','));
            }

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

            edit.onclick = (clickEvent) => {
                clickEvent.preventDefault();
                // @ts-ignore Bootstrap is loaded by the admin theme.
                window.bootstrap.Modal.getOrCreateInstance(modal).hide();
                openEditor(edit.href);
            };

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
                openEditor(toSafeNavigationUrl(config.createUrl, params.toString()));
            } else {
                openEditor(toSafeNavigationUrl(config.chooseUrl, params.toString()));
            }

            calendar.unselect();
        },
        eventDrop: (/** @type {any} */ info) => move(info),
        eventResize: (/** @type {any} */ info) => move(info),
    });

    const editorModal = /** @type {HTMLElement} */ (document.getElementById('calendar-editor-modal'));
    const editorBody = /** @type {HTMLElement} */ (editorModal.querySelector('[data-editor-body]'));
    const editorTitle = /** @type {HTMLElement} */ (editorModal.querySelector('[data-editor-title]'));

    /**
     * Shows a message at the top of the editor modal.
     * @param {string} message
     */
    const showEditorMessage = (message) => {
        const alert = document.createElement('div');

        alert.className = 'alert alert-danger';
        alert.setAttribute('role', 'alert');
        alert.textContent = message;
        editorBody.querySelector('.alert[role="alert"]')?.remove();
        editorBody.prepend(alert);
        editorBody.querySelectorAll('button').forEach((button) => {
            /** @type {HTMLButtonElement} */ (button).disabled = false;
        });
    };

    /**
     * Shows what an event page answered: its form or list in the modal, or, once a change is done, the calendar again.
     * @param {Response} response
     */
    const showEditorResponse = async (response) => {
        if (!response.ok) {
            showEditorMessage(labels.editorFailed);

            return;
        }

        if ((response.headers.get('Content-Type') || '').includes('application/json')) {
            const outcome = await response.json();

            if (outcome.succeeded) {
                // @ts-ignore Bootstrap is loaded by the admin theme.
                window.bootstrap.Modal.getOrCreateInstance(editorModal).hide();
                calendar.refetchEvents();
            } else {
                showEditorMessage(outcome.message || labels.editorFailed);
            }

            return;
        }

        renderEditor(editorBody, await response.text());

        const heading = editorBody.querySelector('[data-calendar-modal-title]');

        editorTitle.textContent = heading ? heading.textContent : '';
        heading?.remove();

        // The editor's pickers start on page load, so the ones in the modal are started here.
        // @ts-ignore The item selector and the select picker are loaded by the page.
        window.CrestAppsItemSelector?.initAll(editorBody);
        editorBody.querySelectorAll('select.selectpicker').forEach((select) => {
            // @ts-ignore
            window.Selectpicker?.getOrCreateInstance(select);
        });
    };

    /**
     * Opens an event page (choose a calendar, add or edit an event) in the editor modal.
     * @param {string} href
     */
    const openEditor = async (href) => {
        const url = toModalUrl(href);

        if (!url) {
            return;
        }

        const spinner = document.createElement('div');

        spinner.className = 'text-center text-muted py-5';
        spinner.textContent = labels.loading;
        editorTitle.textContent = '';
        editorBody.replaceChildren(spinner);
        showModal(editorModal);

        try {
            await showEditorResponse(await fetch(url, { credentials: 'same-origin', headers: { Accept: 'text/html, application/json' } }));
        } catch {
            showEditorMessage(labels.editorFailed);
        }
    };

    // Every form in the modal posts in place, with the button that was pressed.
    editorBody.addEventListener('submit', async (event) => {
        const form = /** @type {HTMLFormElement} */ (event.target);

        if (!form.matches('[data-calendar-editor-form]')) {
            return;
        }

        event.preventDefault();

        const submitter = /** @type {HTMLButtonElement | null} */ (/** @type {SubmitEvent} */ (event).submitter);
        const url = toModalUrl(submitter && submitter.hasAttribute('formaction') ? submitter.formAction : form.action);

        if (!url) {
            return;
        }

        try {
            await showEditorResponse(await fetch(url, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { Accept: 'text/html, application/json' },
                body: new FormData(form, submitter),
            }));
        } catch {
            showEditorMessage(labels.editorFailed);
        }
    });

    // Links between event pages (such as choosing a calendar, then the kind of event) stay in the modal.
    editorBody.addEventListener('click', (event) => {
        const link = /** @type {HTMLElement} */ (event.target).closest('a[data-calendar-modal-link]');

        if (link) {
            event.preventDefault();
            openEditor(/** @type {HTMLAnchorElement} */ (link).href);
        }
    });

    document.querySelectorAll('[data-calendar-new-event]').forEach((button) => button.addEventListener('click', (event) => {
        event.preventDefault();
        openEditor(/** @type {HTMLAnchorElement} */ (button).href);
    }));

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
