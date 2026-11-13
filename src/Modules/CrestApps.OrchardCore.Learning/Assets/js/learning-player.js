// The course player reports what the learner does; the server decides what is completed.
// It records the time on the open item (a call when the item opens, then one every half minute while the page is
// visible) and, for a video lesson, how much of the video has been watched.

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

    const progress = document.querySelector('[data-learning-progress]');

    if (progress && typeof result.progressPercent === 'number') {
        progress.setAttribute('aria-valuenow', String(result.progressPercent));
        const bar = progress.querySelector('.progress-bar');

        if (bar) {
            bar.style.width = `${result.progressPercent}%`;
            bar.textContent = `${result.progressPercent}%`;
        }
    }
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

        reported = percent;
        lastReport = now;
        showCompleted(root, await post(root, root.dataset.viewedUrl, { percent: String(percent) }));
    };

    video.addEventListener('timeupdate', () => report(false));
    video.addEventListener('pause', () => report(true));
    video.addEventListener('ended', () => report(true));
}

function trackTime(root) {
    let last = Date.now();

    const beat = async () => {
        const now = Date.now();
        const seconds = Math.round((now - last) / 1000);

        last = now;
        showCompleted(root, await post(root, root.dataset.heartbeatUrl, { seconds: String(seconds) }));
    };

    // The first call records that the item was opened, which completes an item completed by viewing it.
    post(root, root.dataset.heartbeatUrl, { seconds: '0' }).then(result => showCompleted(root, result));

    setInterval(() => {
        if (document.visibilityState === 'visible') {
            beat();
        } else {
            last = Date.now();
        }
    }, heartbeatSeconds * 1000);
}

document.querySelectorAll('[data-learning-player]').forEach(root => {
    trackTime(root);
    trackVideo(root);
});
