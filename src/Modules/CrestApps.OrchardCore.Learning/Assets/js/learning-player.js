// The course player reports what the learner does; the server decides what is completed.
// It records the time on the open item (a call when the item opens, one every half minute while the page is visible,
// and one when the learner leaves it) and, for a video lesson, how much of the video has been watched.

import { createActiveTimer, progressText } from './learning-player-helpers.js';

const heartbeatSeconds = 30;
const viewedReportSeconds = 10;

function token(root) {
    return root.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
}

async function post(root, url, values) {
    const body = new URLSearchParams(values);

    body.append('__RequestVerificationToken', token(root));

    const response = await fetch(url, {
        method: 'POST',
        body,
        credentials: 'same-origin',
        headers: { 'X-Requested-With': 'XMLHttpRequest' },
    });

    if (!response.ok) {
        return null;
    }

    return response.json();
}

function showCompleted(root, result) {
    if (!result || !result.itemCompleted) {
        return;
    }

    document.querySelector('[data-learning-completed]')?.classList.remove('d-none');

    const state = document.querySelector(`[data-learning-item="${root.dataset.itemId}"] [data-learning-state]`);

    if (state) {
        state.innerHTML = '<i class="fa-solid fa-circle-check text-success" aria-hidden="true"></i>';
        state.append(Object.assign(document.createElement('span'), { className: 'visually-hidden', textContent: root.dataset.completedText }));
    }

    // The item's condition turns from "To do: ..." into "Done: ...".
    document.querySelectorAll('.learning-item-header [data-learning-condition-state="todo"]').forEach(badge => {
        badge.classList.remove('text-bg-light', 'border');
        badge.classList.add('text-bg-success');
        badge.dataset.learningConditionState = 'done';
        badge.textContent = badge.dataset.doneText ?? badge.textContent;
        badge.prepend(Object.assign(document.createElement('i'), { className: 'fa-solid fa-check me-1', ariaHidden: 'true' }));
    });

    showProgress(result);

    if (result.courseCompleted) {
        document.querySelector('[data-learning-course-done]')?.classList.remove('d-none');
    }
}

// The bar and "3 of 7 complete · 43%" beside it.
function showProgress(result) {
    const progress = document.querySelector('[data-learning-progress]');

    if (!progress || typeof result.progressPercent !== 'number') {
        return;
    }

    const bar = progress.querySelector('[role="progressbar"]');
    const fill = progress.querySelector('.progress-bar');
    const label = progress.querySelector('[data-learning-progress-text]');
    const required = typeof result.requiredItems === 'number' ? result.requiredItems : Number(progress.dataset.required);
    const completed = typeof result.completedItems === 'number' ? result.completedItems : Number(progress.dataset.completed);
    const text = progressText(progress.dataset.textTemplate ?? '{2}%', completed, required, result.progressPercent);

    bar?.setAttribute('aria-valuenow', String(result.progressPercent));
    bar?.setAttribute('aria-valuetext', text);

    if (fill) {
        fill.style.width = `${result.progressPercent}%`;
        fill.classList.toggle('bg-success', result.progressPercent >= 100);
    }

    if (label) {
        label.textContent = text;
    }
}

// "Collapse all" / "Expand all" for the sections of the player's outline, as Moodle's course index.
function toggleSections(button) {
    const sections = () => [...document.querySelectorAll('#learning-player-outline [data-learning-section]')];
    const isCurrent = section => section.querySelector('[aria-current="step"]') !== null;

    // The section of the open item stays open when collapsing, so it does not count.
    const others = () => {
        const all = sections();
        const rest = all.filter(section => !isCurrent(section));

        return rest.length > 0 ? rest : all;
    };

    const update = () => {
        const anyOpen = others().some(section => section.open);

        button.textContent = anyOpen ? button.dataset.collapseText : button.dataset.expandText;
        button.setAttribute('aria-expanded', String(anyOpen));
    };

    button.addEventListener('click', () => {
        const open = !others().some(section => section.open);

        sections().forEach(section => {
            section.open = open || isCurrent(section);
        });
        update();
    });

    sections().forEach(section => section.addEventListener('toggle', update));
    update();
}

// The share of the video's length covered by the ranges the learner played.
function watchedPercent(video) {
    if (!video.duration || !Number.isFinite(video.duration)) {
        return 0;
    }

    let seconds = 0;

    for (let index = 0; index < video.played.length; index++) {
        seconds += video.played.end(index) - video.played.start(index);
    }

    return Math.min(100, Math.floor((seconds / video.duration) * 100));
}

function trackVideo(root) {
    const video = document.querySelector('[data-learning-video]');

    if (!video) {
        return;
    }

    let reported = 0;
    let lastReport = 0;

    const report = async force => {
        const percent = watchedPercent(video);
        const now = Date.now();

        if (percent <= reported || (!force && now - lastReport < viewedReportSeconds * 1000)) {
            return;
        }

        const previous = reported;

        reported = percent;
        lastReport = now;

        const result = await post(root, root.dataset.viewedUrl, { percent: String(percent) });

        // The enrollment was busy with another change: report the same share again on the next update.
        if (result?.retry) {
            reported = previous;
        }

        showCompleted(root, result);
    };

    video.addEventListener('timeupdate', () => report(false));
    video.addEventListener('pause', () => report(true));
    video.addEventListener('ended', () => report(true));
}

function trackTime(root) {
    const timer = createActiveTimer(Date.now());

    const beat = async () => {
        const seconds = timer.take(Date.now());

        showCompleted(root, await post(root, root.dataset.heartbeatUrl, { seconds: String(seconds) }));
    };

    // When the learner leaves the item (another item, another tab, closing the page), the seconds since the last call
    // are sent at once: most items take less than the half minute between calls, and their time was lost.
    const leave = () => {
        const seconds = timer.hide(Date.now());

        if (seconds <= 0) {
            return;
        }

        const body = new FormData();

        body.append('seconds', String(seconds));
        body.append('__RequestVerificationToken', token(root));

        if (!navigator.sendBeacon?.(root.dataset.heartbeatUrl, body)) {
            fetch(root.dataset.heartbeatUrl, { method: 'POST', body, credentials: 'same-origin', keepalive: true });
        }
    };

    // The first call records that the item was opened, which completes an item completed by viewing it. When the
    // enrollment is busy with another change, it is sent again shortly; the half-minute calls need no retry, because the
    // server counts the time since the last access it recorded.
    const open = attempt => post(root, root.dataset.heartbeatUrl, { seconds: '0' }).then(result => {
        if (result?.retry && attempt < 3) {
            setTimeout(() => open(attempt + 1), 2000);

            return;
        }

        showCompleted(root, result);
    });

    open(1);

    setInterval(() => {
        if (document.visibilityState === 'visible') {
            beat();
        }
    }, heartbeatSeconds * 1000);

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden') {
            leave();
        } else {
            timer.show(Date.now());
        }
    });
    window.addEventListener('pagehide', leave);
}

document.querySelectorAll('[data-learning-toggle-sections]').forEach(toggleSections);

document.querySelectorAll('[data-learning-player]').forEach(root => {
    trackTime(root);
    trackVideo(root);
});
