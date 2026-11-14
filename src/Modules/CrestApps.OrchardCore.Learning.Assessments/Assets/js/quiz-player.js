// The quiz player saves each answer as the learner gives it and shows the time left. The server keeps the deadline:
// when the time is up it submits the attempt itself, and refuses answers that arrive late.

const saveDelay = 600;

function token(form) {
    return form.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
}

function answerUrl(form, index) {
    return form.dataset.answerUrl.replace(/\/answers\/0$/, `/answers/${index}`);
}

async function save(form, fieldset) {
    const index = fieldset.dataset.question;
    const body = new URLSearchParams();

    body.append('__RequestVerificationToken', token(form));

    for (const element of fieldset.querySelectorAll('input, select, textarea')) {
        if (!element.name || ((element.type === 'radio' || element.type === 'checkbox') && !element.checked)) {
            continue;
        }

        body.append(element.name, element.value);
    }

    const response = await fetch(answerUrl(form, index), {
        method: 'POST',
        body,
        credentials: 'same-origin',
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
    });

    if (!response.ok) {
        return;
    }

    const result = await response.json();

    if (result.error === 'DeadlinePassed' || result.open === false) {
        window.location.reload();

        return;
    }

    fieldset.querySelector('[data-saved]')?.classList.toggle('d-none', !result.saved);
}

function watch(form) {
    const timers = new Map();

    form.addEventListener('input', event => {
        const fieldset = event.target.closest('[data-question]');

        if (!fieldset) {
            return;
        }

        fieldset.querySelector('[data-saved]')?.classList.add('d-none');
        clearTimeout(timers.get(fieldset));
        timers.set(fieldset, setTimeout(() => save(form, fieldset), saveDelay));
    });

    form.querySelector('[data-learning-submit]')?.addEventListener('click', event => {
        const text = event.currentTarget.dataset.confirmText;

        if (text && !window.confirm(text)) {
            event.preventDefault();
        }
    });
}

function format(seconds) {
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const rest = seconds % 60;
    const pad = value => String(value).padStart(2, '0');

    return hours > 0 ? `${hours}:${pad(minutes)}:${pad(rest)}` : `${minutes}:${pad(rest)}`;
}

function countdown(timer, form) {
    const deadline = Date.parse(timer.dataset.deadline);
    const offset = Date.parse(timer.dataset.serverNow) - Date.now();
    const text = timer.querySelector('[data-learning-timer-text]');

    const tick = () => {
        const left = Math.max(0, Math.round((deadline - (Date.now() + offset)) / 1000));

        text.textContent = format(left);

        if (left <= 60) {
            timer.classList.replace('alert-warning', 'alert-danger');
        }

        if (left === 0) {
            clearInterval(handle);
            form?.submit();
        }
    };

    const handle = setInterval(tick, 1000);
    tick();
}

document.addEventListener('DOMContentLoaded', () => {
    const form = document.querySelector('[data-learning-attempt]');

    if (form) {
        watch(form);
    }

    const timer = document.querySelector('[data-learning-timer]');

    if (timer) {
        countdown(timer, form);
    }
});
