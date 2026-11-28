// @ts-check
/*
 * The survey designer: outline commands (add, move, duplicate and delete pages and questions, also by dragging), the
 * question and page editors loaded into the side panel and saved without leaving the page, and the lists and rule
 * builders of every editor on the page. Everything posts to the server, which applies the change to the draft and
 * returns the new markup.
 *
 * Unsaved edits are never lost silently: Save draft (and Save and publish) first saves the open question or page,
 * opening another item asks before it replaces unsaved edits, and leaving the page asks too.
 */
import { withDraftStamp } from './designer/draft-stamp.js';
import { initRepeaters } from './designer/repeater.js';
import { initRuleBuilders } from './designer/rule-builder.js';

/**
 * @param {string} id The id of a JSON script element.
 * @param {any} fallback The value when it is missing.
 * @returns {any} The parsed value.
 */
function readJson(id, fallback) {
    const node = document.getElementById(id);

    if (!node) {
        return fallback;
    }

    try {
        return JSON.parse(node.textContent || '');
    } catch {
        return fallback;
    }
}

const designer = document.querySelector('[data-survey-designer]');

if (designer) {
    const context = { operators: readJson('survey-rule-operators', []), texts: readJson('survey-designer-texts', {}) };
    const outlineHost = /** @type {HTMLElement} */ (document.getElementById('survey-outline'));
    const panel = /** @type {HTMLElement} */ (document.getElementById('survey-editor-panel'));
    const settingsForm = /** @type {HTMLFormElement | null} */ (document.getElementById('survey-settings-form'));
    const pickerModal = document.getElementById('survey-picker-modal');
    let targetPageId = '';
    /** @type {string | null} */
    let draggedQuestion = null;
    // True once the page is about to unload on purpose (a save, or a link the user confirmed).
    let leaving = false;
    // True while the confirm dialog of Discard draft is open: discarding drops the edits on purpose.
    let discarding = false;

    const token = () => /** @type {HTMLInputElement | null} */ (document.querySelector('input[name="__RequestVerificationToken"]'))?.value ?? '';
    const outline = () => /** @type {HTMLElement | null} */ (outlineHost.querySelector('[data-survey-outline]'));

    /**
     * Wires the lists and rule builders under a root.
     * @param {ParentNode} root The root.
     */
    const wire = root => {
        initRepeaters(root, item => wire(item));
        initRuleBuilders(root, context);
    };

    const versionInput = /** @type {HTMLInputElement | null} */ (settingsForm?.querySelector('[data-survey-draft-version]') ?? null);

    // The version of the draft this page last saw. Every save sends it and the server refuses one made over another
    // author's change (409), so a save here never moves the page past a change it didn't see.
    let knownVersion = versionInput?.value ?? '';

    /**
     * Adopts the draft version a save returned. The server accepted the save only because the page knew the draft it
     * replaced, so the settings form may post over the new version too.
     * @param {Response} response The response of a save.
     */
    const trackVersion = response => {
        const version = response.headers.get('X-Survey-Draft-Version');

        if (version) {
            knownVersion = version;

            if (versionInput) {
                versionInput.value = version;
            }

            // Discard draft carries the version too, so it doesn't read the page's own saves as another author's.
            document.querySelectorAll('a[data-survey-discard]').forEach(link => {
                const anchor = /** @type {HTMLAnchorElement} */ (link);
                anchor.href = withDraftStamp(anchor.href, version, window.location.href);
            });
        }
    };

    /**
     * Gets the headers every save sends.
     * @param {Record<string, string>} headers Other headers.
     * @returns {Record<string, string>} The headers.
     */
    const saveHeaders = headers => (knownVersion ? { ...headers, 'X-Survey-Draft-Version': knownVersion } : headers);

    /**
     * Shows a short error in the panel.
     * @param {string} message The message.
     */
    const showError = message => {
        const alert = document.createElement('div');

        alert.className = 'alert alert-danger mt-2';
        alert.setAttribute('role', 'alert');
        alert.textContent = message || context.texts.failed;
        panel.prepend(alert);
        setTimeout(() => alert.remove(), 6000);
    };

    /**
     * Reads a form as text to tell whether it changed. The draft version and the antiforgery token are left out, since
     * saves made on this page change them.
     * @param {HTMLFormElement} form The form.
     * @returns {string} The form's values.
     */
    const snapshot = form => {
        /** @type {string[]} */
        const values = [];

        new FormData(form).forEach((value, key) => {
            if (key !== 'draftVersion' && key !== '__RequestVerificationToken') {
                values.push(`${key}=${typeof value === 'string' ? value : value.name}`);
            }
        });

        return values.join('&');
    };

    const panelForm = () => /** @type {HTMLFormElement | null} */ (panel.querySelector('form[data-survey-panel-form]'));
    let panelBaseline = '';
    let settingsBaseline = '';

    const resetPanelBaseline = () => {
        const form = panelForm();

        panelBaseline = form ? snapshot(form) : '';
    };

    const isPanelDirty = () => {
        const form = panelForm();

        return !!form && snapshot(form) !== panelBaseline;
    };

    const isSettingsDirty = () => !!settingsForm && snapshot(settingsForm) !== settingsBaseline;

    /**
     * Saves the question or page open in the panel and shows the editor the server returns.
     * @param {HTMLFormElement} form The panel's form.
     * @returns {Promise<boolean>} Whether it was saved.
     */
    const savePanel = async form => {
        const response = await fetch(form.action, {
            method: 'POST',
            credentials: 'same-origin',
            headers: saveHeaders({ 'X-Requested-With': 'XMLHttpRequest' }),
            body: new FormData(form),
        });

        if (response.status === 409) {
            // Another author changed the draft: the edits stay in the panel, unsaved, and the message says to reload.
            showError(await response.text());

            return false;
        }

        if (!response.ok && response.status !== 422) {
            showError(context.texts.failed);

            return false;
        }

        panel.innerHTML = await response.text();
        wire(panel);

        if (!response.ok) {
            // The editor now shows the problems, and its edits still count as unsaved.
            return false;
        }

        resetPanelBaseline();
        trackVersion(response);
        await refreshOutline();

        return true;
    };

    /**
     * Loads an editor into the panel.
     * @param {string} url The editor's URL.
     */
    const loadPanel = async url => {
        if (isPanelDirty() && !window.confirm(context.texts.discardPanel)) {
            return;
        }

        const response = await fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' });

        if (!response.ok) {
            showError(context.texts.failed);

            return;
        }

        panel.innerHTML = await response.text();
        wire(panel);
        resetPanelBaseline();
        /** @type {HTMLElement | null} */ (panel.querySelector('input:not([type=hidden]), textarea, select'))?.focus();
    };

    /**
     * Opens the editor of a question.
     * @param {string} id The question id.
     */
    const editQuestion = id => {
        const url = outline()?.dataset.questionUrl;

        if (url && id) {
            loadPanel(url.replace('__question__', encodeURIComponent(id)));
        }
    };

    /**
     * Opens the editor of a page.
     * @param {string} id The page id.
     */
    const editPage = id => {
        const url = outline()?.dataset.pageUrl;

        if (url && id) {
            loadPanel(url.replace('__page__', encodeURIComponent(id)));
        }
    };

    /**
     * Sends an outline command and shows the new outline.
     * @param {Record<string, any>} command The command.
     * @returns {Promise<string | null>} The id of the item added, or null.
     */
    const send = async command => {
        const url = outline()?.dataset.commandsUrl;

        if (!url) {
            return null;
        }

        const response = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: saveHeaders({ 'Content-Type': 'application/json', 'RequestVerificationToken': token(), 'X-Requested-With': 'XMLHttpRequest' }),
            body: JSON.stringify(command),
        });

        if (!response.ok) {
            showError(await response.text());

            return null;
        }

        outlineHost.innerHTML = await response.text();
        trackVersion(response);

        return response.headers.get('X-Survey-Item');
    };

    /**
     * Reloads the outline after an editor saved.
     */
    const refreshOutline = async () => {
        const url = outline()?.dataset.commandsUrl?.replace(/\/commands$/, '/outline');

        if (!url) {
            return;
        }

        const response = await fetch(url, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } });

        if (response.ok) {
            outlineHost.innerHTML = await response.text();
        }
    };

    /**
     * @param {string | undefined} value A number attribute.
     * @returns {number | undefined} The number, or undefined.
     */
    const toIndex = value => (value === undefined || value === '' ? undefined : Number(value));

    document.addEventListener('click', async event => {
        const target = /** @type {HTMLElement} */ (event.target);
        const commandButton = /** @type {HTMLElement | null} */ (target.closest('[data-survey-command]'));

        if (commandButton) {
            event.preventDefault();

            const data = commandButton.dataset;
            const confirmText = data.confirm === 'page' ? context.texts.confirmDeletePage : data.confirm ? context.texts.confirmDelete : null;

            if (confirmText && !window.confirm(confirmText)) {
                return;
            }

            const command = data.surveyCommand;
            const id = await send({
                command,
                pageId: data.pageId,
                questionId: data.questionId,
                toPageId: data.toPageId,
                index: toIndex(data.index),
            });

            if (id && command === 'duplicate') {
                editQuestion(id);
            } else if (id && (command === 'addPage' || command === 'duplicatePage')) {
                editPage(id);
            } else if (command === 'delete' || command === 'deletePage') {
                panel.innerHTML = '';
                resetPanelBaseline();
            }

            return;
        }

        const picker = /** @type {HTMLElement | null} */ (target.closest('[data-survey-open-picker]'));

        if (picker) {
            event.preventDefault();
            targetPageId = picker.dataset.surveyOpenPicker ?? '';

            const bootstrap = /** @type {any} */ (window).bootstrap;

            if (pickerModal && bootstrap?.Modal) {
                bootstrap.Modal.getOrCreateInstance(pickerModal).show();
            }

            return;
        }

        const add = /** @type {HTMLElement | null} */ (target.closest('[data-survey-add-question]'));

        if (add) {
            const id = await send({ command: 'addQuestion', pageId: targetPageId, controlType: add.dataset.controlType, preset: add.dataset.preset || undefined });

            if (id) {
                editQuestion(id);
            }

            return;
        }

        const question = /** @type {HTMLElement | null} */ (target.closest('[data-survey-edit-question]'));

        if (question) {
            event.preventDefault();
            editQuestion(question.dataset.surveyEditQuestion ?? '');

            return;
        }

        const page = /** @type {HTMLElement | null} */ (target.closest('[data-survey-edit-page]'));

        if (page) {
            event.preventDefault();
            editPage(page.dataset.surveyEditPage ?? '');

            return;
        }

        if (target.closest('[data-survey-discard]')) {
            discarding = true;

            return;
        }

        // A link that leaves the designer asks first when there are unsaved edits.
        const link = /** @type {HTMLAnchorElement | null} */ (target.closest('a[href]'));

        if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
            return;
        }

        const href = link.getAttribute('href') ?? '';

        if (href === '' || href.startsWith('#') || link.target === '_blank' || link.hasAttribute('download') || link.hasAttribute('data-bs-toggle')) {
            return;
        }

        // Only a link that navigates to a page leaves the designer: script, data and mail links do not.
        const protocol = new URL(link.href, window.location.href).protocol;

        if (protocol !== 'http:' && protocol !== 'https:') {
            return;
        }

        if (isPanelDirty() || isSettingsDirty()) {
            if (window.confirm(context.texts.unsaved)) {
                leaving = true;
            } else {
                event.preventDefault();
            }
        }
    });

    // The confirm dialog of Discard draft closed; when it was cancelled, the edits count again.
    document.addEventListener('hidden.bs.modal', () => {
        discarding = false;
    });

    window.addEventListener('beforeunload', event => {
        if (leaving || discarding || !(isPanelDirty() || isSettingsDirty())) {
            return;
        }

        event.preventDefault();
        event.returnValue = '';
    });

    // Save draft, and Save and publish, first save the open question or page, so one click saves everything.
    settingsForm?.addEventListener('submit', async event => {
        const submitter = /** @type {HTMLButtonElement | null} */ (/** @type {SubmitEvent} */ (event).submitter ?? null);

        if (leaving || submitter?.hasAttribute('data-survey-discard')) {
            leaving = true;

            return;
        }

        const form = panelForm();

        if (form && isPanelDirty()) {
            event.preventDefault();

            const saved = await savePanel(form);

            // The admin theme marked the form as submitting to block a double post; this post is retried or continued.
            settingsForm.classList.remove('submitting');

            if (!saved) {
                showError(context.texts.panelInvalid);

                return;
            }

            leaving = true;
            settingsForm.requestSubmit(submitter ?? undefined);

            return;
        }

        // Publishing with nothing to save goes straight to the publish page, without saving an unchanged draft.
        const publishUrl = submitter?.dataset.surveyPublish;

        if (publishUrl && !isSettingsDirty()) {
            event.preventDefault();
            leaving = true;
            window.location.href = publishUrl;

            return;
        }

        leaving = true;
    });

    panel.addEventListener('submit', async event => {
        const form = /** @type {HTMLFormElement} */ (event.target);

        if (!form.matches('[data-survey-panel-form]')) {
            return;
        }

        event.preventDefault();
        await savePanel(form);
    });

    outlineHost.addEventListener('dragstart', event => {
        const item = /** @type {HTMLElement | null} */ (/** @type {HTMLElement} */ (event.target).closest('[data-question-id]'));

        if (item) {
            draggedQuestion = item.dataset.questionId ?? null;
            item.classList.add('opacity-50');
            event.dataTransfer?.setData('text/plain', draggedQuestion ?? '');
        }
    });

    outlineHost.addEventListener('dragover', event => {
        if (draggedQuestion && /** @type {HTMLElement} */ (event.target).closest('[data-survey-question-list]')) {
            event.preventDefault();
        }
    });

    outlineHost.addEventListener('drop', async event => {
        const target = /** @type {HTMLElement} */ (event.target);
        const list = /** @type {HTMLElement | null} */ (target.closest('[data-survey-question-list]'));

        if (!draggedQuestion || !list) {
            return;
        }

        event.preventDefault();

        const over = /** @type {HTMLElement | null} */ (target.closest('[data-question-id]'));
        const items = Array.from(list.querySelectorAll('[data-question-id]')).filter(item => /** @type {HTMLElement} */ (item).dataset.questionId !== draggedQuestion);
        let index = items.length;

        if (over && over.dataset.questionId !== draggedQuestion) {
            const box = over.getBoundingClientRect();
            index = items.indexOf(over) + (event.clientY > box.top + box.height / 2 ? 1 : 0);
        }

        const id = draggedQuestion;
        draggedQuestion = null;
        await send({ command: 'move', questionId: id, toPageId: list.dataset.pageId, index });
    });

    outlineHost.addEventListener('dragend', () => {
        draggedQuestion = null;
        outlineHost.querySelectorAll('.opacity-50').forEach(item => item.classList.remove('opacity-50'));
    });

    wire(document);
    settingsBaseline = settingsForm ? snapshot(settingsForm) : '';
}
