// The player of a learning package. Launch asks the server for a launch (with the learner's session and the page's
// antiforgery token) and opens the package in a sandboxed frame: on an opaque origin in strict mode, or on the
// package content host when the site has one (the launch names its origin and the frame's sandbox). The frame talks to
// this page only through postMessage: a message is taken only from this page's own frame, from the origin "null" in
// strict mode or the content host's origin, with the protocol name of the package's standard (SCORM 1.2 or SCORM 2004,
// which the launch names) and the id of the current launch.
// What the content set is sent to the server, which checks every value again, when the content commits or finishes
// (Terminate in SCORM 2004), every half minute, and when the page is left.

import { isFrameMessage, isWindowFinishedMessage, windowFinishedMessage } from './package-frame-messages.js';
import { createRuntimeQueue } from './package-runtime-queue.js';

// The message protocol of the SCORM 1.2 frame script; a SCORM 2004 launch names its own (crestapps-scorm2004).
const defaultProtocol = 'crestapps-scorm12';
const frameName = 'crestapps-package-frame';
const strictSandbox = 'allow-scripts allow-forms allow-popups';
const flushSeconds = 30;

function antiforgery(root) {
    return root.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
}

function form(root, values) {
    const body = new URLSearchParams(values);

    body.append('__RequestVerificationToken', antiforgery(root));

    return body;
}

function setStatus(root, text) {
    const status = root.querySelector('[data-package-message]');

    if (status) {
        status.textContent = text;
        status.classList.toggle('d-none', !text);
    }
}

// A package that opens in a window of its own: the button opens the window page, which runs the package as this page
// would. This page shows the progress the server has once the window says its session finished, or closes.
function startWindowOpener(root, button) {
    const itemId = root.dataset.packageItemId;
    let opened = null;
    let timer = null;

    window.addEventListener('message', event => {
        if (isWindowFinishedMessage(event, opened, window.location.origin, itemId)) {
            window.location.reload();
        }
    });

    button.addEventListener('click', () => {
        opened = window.open(root.dataset.packageWindowUrl, 'crestapps-package-' + itemId);

        if (!opened) {
            setStatus(root, root.dataset.errorText);

            return;
        }

        setStatus(root, root.dataset.windowText);
        window.clearInterval(timer);
        timer = window.setInterval(() => {
            if (opened.closed) {
                window.clearInterval(timer);
                setStatus(root, root.dataset.packageWindowClosedText);
                window.location.reload();
            }
        }, 1000);
    });
}

function start(root) {
    const button = root.querySelector('[data-package-launch]');
    const host = root.querySelector('[data-package-frame-host]');

    if (!button || !host) {
        return;
    }

    if (root.dataset.packageWindowUrl) {
        startWindowOpener(root, button);

        return;
    }

    const inWindow = root.dataset.packageInWindow === 'true';

    let session = null;

    // Posts one runtime call of the current launch. A busy answer comes back with retry, and the queue sends the same
    // call again; an answer that never came is null, and the queue sends it again too.
    const post = async (current, payload, keepalive) => {
        const body = form(root, {
            token: current.token,
            payload: JSON.stringify(payload),
        });

        try {
            const response = await fetch(current.runtimeUrl, {
                method: 'POST',
                body,
                credentials: 'same-origin',
                keepalive,
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
            });

            if (!response.ok) {
                setStatus(root, root.dataset.errorText);

                return { accepted: false };
            }

            const result = await response.json();

            if (!result.retry && !result.accepted) {
                setStatus(root, result.message || root.dataset.errorText);
            } else if (result.refused?.length) {
                console.warn('The server refused some values the package set.', result.refused);
            }

            // The course player refreshes its outline and progress when the server completed the item.
            if (result.itemCompleted) {
                document.dispatchEvent(new CustomEvent('learning:item-completed', { detail: result }));
            }

            return result;
        } catch {
            return null;
        }
    };

    // Every call of a launch goes through its queue, one at a time and in order (review round 9, M3): a finish waits for
    // the commit before it, and takes the values the content set until its turn.
    const send = (commit, finish, keepalive) => {
        const current = session;

        if (!current) {
            return Promise.resolve(null);
        }

        return current.queue.send(() => {
            const changes = current.pending.splice(0);

            return changes.length === 0 && !commit && !finish ? null : { changes, commit, finish };
        }, keepalive);
    };

    const onMessage = async event => {
        const frame = host.querySelector('iframe');

        // Only this page's own frame, from the origin of the launch's frame, and only for the current launch.
        if (!isFrameMessage(event, frame?.contentWindow ?? null, session)) {
            return;
        }

        const data = event.data;

        switch (data.type) {
            case 'set':
                if (typeof data.element === 'string' && typeof data.value === 'string') {
                    session.pending.push([data.element, data.value]);
                }

                break;
            case 'commit':
                await send(true, false);
                break;
            case 'finish': {
                const result = await send(false, true);

                session.finished = true;
                window.clearInterval(session.timer);

                if (result?.accepted) {
                    setStatus(root, root.dataset.finishedText);

                    if (inWindow && window.opener && !window.opener.closed) {
                        // The page that opened this window shows the progress; the window closes.
                        window.opener.postMessage(windowFinishedMessage(root.dataset.packageItemId), window.location.origin);
                        window.setTimeout(() => window.close(), 800);
                    } else {
                        // The page shows the status, score and progress the server now has.
                        window.setTimeout(() => window.location.reload(), 800);
                    }
                }

                break;
            }
            default:
                break;
        }
    };

    window.addEventListener('message', onMessage);

    window.addEventListener('pagehide', () => {
        if (session && !session.finished && session.pending.length > 0) {
            send(true, false, true);
        }
    });

    button.addEventListener('click', async () => {
        button.disabled = true;
        setStatus(root, '');

        try {
            const response = await fetch(root.dataset.launchUrl, {
                method: 'POST',
                body: form(root, {}),
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
            });
            const result = response.ok ? await response.json() : null;

            if (!result?.launched) {
                setStatus(root, result?.message || root.dataset.errorText);
                button.disabled = false;

                return;
            }

            if (session) {
                window.clearInterval(session.timer);
            }

            session = {
                token: result.token,
                launchId: result.launchId,
                protocol: result.protocol || defaultProtocol,
                contentOrigin: result.contentOrigin || null,
                runtimeUrl: result.runtimeUrl,
                pending: [],
                finished: false,
            };
            const launched = session;
            session.queue = createRuntimeQueue((payload, keepalive) => post(launched, payload, keepalive));
            session.timer = window.setInterval(() => send(false, false), flushSeconds * 1000);

            const frame = document.createElement('iframe');

            frame.name = frameName;
            frame.title = root.dataset.frameTitle;
            frame.className = 'learning-package-frame w-100 border rounded bg-white';
            frame.style.minHeight = inWindow ? '85vh' : '70vh';
            frame.setAttribute('sandbox', result.frameSandbox || strictSandbox);
            frame.setAttribute('allow', 'fullscreen; autoplay');
            frame.setAttribute('allowfullscreen', '');
            frame.setAttribute('referrerpolicy', 'no-referrer');
            frame.src = result.launchUrl;

            host.replaceChildren(frame);
            host.classList.remove('d-none');
            button.classList.add('d-none');
            frame.focus();
        } catch {
            setStatus(root, root.dataset.errorText);
            button.disabled = false;
        }
    });

    // The window a package opens in launches it at once.
    if (inWindow) {
        button.click();
    }
}

// A cmi5 course: each AU has its own Launch button. A file of the package opens in the sandboxed frame; an https AU,
// or an AU that asks for a window of its own, opens in a new window. The AU talks to the Learning Record Store itself;
// the page only learns when the AU returns (the return page in the frame posts a message), and then reloads to show
// the progress the server has.
function startCmi5(root) {
    const host = root.querySelector('[data-package-frame-host]');
    const buttons = root.querySelectorAll('[data-cmi5-au]');

    if (!host || buttons.length === 0) {
        return;
    }

    window.addEventListener('message', event => {
        const frame = host.querySelector('iframe');

        if (!frame || event.source !== frame.contentWindow || event.origin !== 'null') {
            return;
        }

        if (event.data?.protocol === 'crestapps-cmi5' && event.data.type === 'return') {
            window.location.reload();
        }
    });

    buttons.forEach(button => button.addEventListener('click', async () => {
        buttons.forEach(other => { other.disabled = true; });
        setStatus(root, '');

        try {
            const body = form(root, { au: button.dataset.cmi5Au });
            const response = await fetch(root.dataset.cmi5LaunchUrl, {
                method: 'POST',
                body,
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
            });
            const result = response.ok ? await response.json() : null;

            if (!result?.launched) {
                setStatus(root, result?.message || root.dataset.errorText);

                return;
            }

            if (result.ownWindow) {
                window.open(result.launchUrl, '_blank', 'noopener');
                setStatus(root, root.dataset.windowText);

                return;
            }

            const frame = document.createElement('iframe');

            frame.name = frameName;
            frame.title = root.dataset.frameTitle;
            frame.className = 'learning-package-frame w-100 border rounded bg-white';
            frame.style.minHeight = '70vh';
            frame.setAttribute('sandbox', 'allow-scripts allow-forms allow-popups');
            frame.setAttribute('allow', 'fullscreen');
            frame.setAttribute('referrerpolicy', 'no-referrer');
            frame.src = result.launchUrl;

            host.replaceChildren(frame);
            host.classList.remove('d-none');
            frame.focus();
        } catch {
            setStatus(root, root.dataset.errorText);
        } finally {
            buttons.forEach(other => { other.disabled = false; });
        }
    }));
}

document.querySelectorAll('[data-learning-package]').forEach(root => (root.dataset.cmi5LaunchUrl ? startCmi5(root) : start(root)));
