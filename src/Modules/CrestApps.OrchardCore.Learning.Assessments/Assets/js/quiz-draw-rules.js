// The "Draw at random" rules of the quiz editor: a list of rules, each a bank, a tag of that bank, a difficulty and how
// many questions to draw, with the count the bank can supply shown beside it. Add rule copies the row template, and
// the remove button takes a row out; the fields are renumbered each time so the form binds every row.
//
// Markup (QuizPart.Edit.cshtml): an element with data-draw-rules holding the rows ([data-draw-rule-rows] of
// [data-draw-rule]), a <template data-draw-rule-template>, an Add rule button ([data-draw-add]) and an empty note
// ([data-draw-empty]). Its data-prefix is the name prefix of the rows, data-summary-url the bank summary endpoint with
// __bank__ for the bank, data-available-text the "of {0} available" text and data-too-many-text the validation message.
// The pickers are the CrestApps bootstrap-select fork, driven through its native window.Selectpicker API (no jQuery).

import { availableCount, bankTags, renumber } from './quiz-draw-summary.js';

const summaries = new Map();

function picker(select) {
    if (!select || !window.Selectpicker) {
        return null;
    }

    return window.Selectpicker.getOrCreateInstance(select);
}

function refresh(select) {
    picker(select)?.refresh();
}

function format(text, value) {
    return (text ?? '').replace('{0}', String(value));
}

// One request per bank for the page, shared by every row that draws from it.
function loadSummary(root, bankId) {
    if (!bankId) {
        return Promise.resolve([]);
    }

    if (!summaries.has(bankId)) {
        const url = root.dataset.summaryUrl.replace('__bank__', encodeURIComponent(bankId));

        summaries.set(bankId, fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } })
            .then(response => response.ok ? response.json() : { questions: [] })
            .then(summary => Array.isArray(summary?.questions) ? summary.questions : [])
            .catch(() => []));
    }

    return summaries.get(bankId);
}

// The tag picker lists the bank's tags after "Any tag"; a saved tag the bank no longer has stays, so it isn't lost.
function fillTags(row, questions) {
    const select = row.querySelector('[data-draw-tag]');
    const current = select.value;
    const tags = bankTags(questions);

    if (current && !tags.some(tag => tag.toLocaleLowerCase() === current.toLocaleLowerCase())) {
        tags.unshift(current);
    }

    while (select.options.length > 1) {
        select.remove(1);
    }

    for (const tag of tags) {
        const option = document.createElement('option');

        option.value = tag;
        option.textContent = tag;
        option.selected = tag === current;
        select.appendChild(option);
    }

    select.disabled = !row.querySelector('[data-draw-bank]').value;
    refresh(select);
}

function showAvailable(root, row, questions) {
    const bankId = row.querySelector('[data-draw-bank]').value;
    const count = row.querySelector('[data-draw-count]');
    const note = row.querySelector('[data-draw-available]');

    if (!bankId) {
        note.textContent = '';
        count.removeAttribute('max');
        count.setCustomValidity('');

        return;
    }

    const available = availableCount(questions, row.querySelector('[data-draw-tag]').value, row.querySelector('[data-draw-difficulty]').value);
    const tooMany = Number(count.value) > available;

    note.textContent = format(root.dataset.availableText, available);
    note.classList.toggle('text-danger', tooMany);
    count.max = String(available);
    count.setCustomValidity(tooMany ? format(root.dataset.tooManyText, available) : '');
}

async function update(root, row, refill) {
    const questions = await loadSummary(root, row.querySelector('[data-draw-bank]').value);

    if (refill) {
        fillTags(row, questions);
    }

    showAvailable(root, row, questions);
}

function rows(root) {
    return [...root.querySelectorAll('[data-draw-rule-rows] > [data-draw-rule]')];
}

function sync(root) {
    const all = rows(root);

    renumber(all, root.dataset.prefix);
    root.querySelector('[data-draw-empty]').hidden = all.length > 0;
}

function init(root) {
    const container = root.querySelector('[data-draw-rule-rows]');
    const template = root.querySelector('[data-draw-rule-template]');

    container.addEventListener('change', event => {
        const row = event.target.closest('[data-draw-rule]');

        if (!row) {
            return;
        }

        if (event.target.matches('[data-draw-bank]')) {
            // Another bank has other tags: the chosen tag is dropped unless the new bank has it too.
            update(root, row, true);
        } else if (event.target.matches('[data-draw-tag], [data-draw-difficulty], [data-draw-count]')) {
            update(root, row, false);
        }
    });

    container.addEventListener('input', event => {
        const row = event.target.closest('[data-draw-rule]');

        if (row && event.target.matches('[data-draw-count]')) {
            update(root, row, false);
        }
    });

    container.addEventListener('click', event => {
        const remove = event.target.closest('[data-draw-remove]');

        if (remove) {
            remove.closest('[data-draw-rule]').remove();
            sync(root);
        }
    });

    root.querySelector('[data-draw-add]').addEventListener('click', () => {
        container.appendChild(template.content.cloneNode(true));

        const row = rows(root).at(-1);

        row.querySelectorAll('select.selectpicker').forEach(select => picker(select));
        sync(root);
        update(root, row, true);
        row.querySelector('[data-draw-bank]')?.focus();
    });

    sync(root);

    for (const row of rows(root)) {
        update(root, row, true);
    }
}

function initAll() {
    document.querySelectorAll('[data-draw-rules]').forEach(init);
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initAll);
} else {
    initAll();
}
