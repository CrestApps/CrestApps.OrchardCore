// The player of a learning package. Launch asks the server for a launch (with the learner's session and the page's
// antiforgery token) and opens the package in a sandboxed frame on an opaque origin. The frame talks to this page only
// through postMessage: a message is taken only from this page's own frame, from the origin "null", with the protocol
// name of the package's standard (SCORM 1.2 or SCORM 2004, which the launch names) and the id of the current launch.
// What the content set is sent to the server, which checks every value again, when the content commits or finishes
// (Terminate in SCORM 2004), every half minute, and when the page is left.

// The message protocol of the SCORM 1.2 frame script; a SCORM 2004 launch names its own (crestapps-scorm2004).
const defaultProtocol = 'crestapps-scorm12';
const frameName = 'crestapps-package-frame';
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

function start(root) {
    const button = root.querySelector('[data-package-launch]');
    const host = root.querySelector('[data-package-frame-host]');

    if (!button || !host) {
        return;
    }

    let session = null;

    const send = async (commit, finish, keepalive) => {
        if (!session) {
            return null;
        }

        const changes = session.pending.splice(0);

        if (changes.length === 0 && !commit && !finish) {
            return null;
        }

        const body = form(root, {
            token: session.token,
            payload: JSON.stringify({ changes, commit, finish }),
        });

        try {
            const response = await fetch(session.runtimeUrl, {
                method: 'POST',
                body,
                credentials: 'same-origin',
                keepalive: keepalive === true,
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
            });

            if (!response.ok) {
                setStatus(root, root.dataset.errorText);

                return null;
            }

            const result = await response.json();

            if (result.retry) {
                // The runtime data was busy with another write: send the same values again with the next call.
                session.pending.unshift(...changes);
            } else if (!result.accepted) {
                setStatus(root, result.message || root.dataset.errorText);
            } else if (result.refused?.length) {
                console.warn('The server refused some values the package set.', result.refused);
            }

            return result;
        } catch {
            session.pending.unshift(...changes);

            return null;
        }
    };

    const onMessage = async event => {
        const frame = host.querySelector('iframe');

        // Only this page's own sandboxed frame, which has an opaque origin, and only for the current launch.
        if (!session || !frame || event.source !== frame.contentWindow || event.origin !== 'null') {
            return;
        }

        const data = event.data;

        if (!data || data.protocol !== session.protocol || data.launchId !== session.launchId) {
            return;
        }

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

                    // The page shows the status, score and progress the server now has.
                    window.setTimeout(() => window.location.reload(), 800);
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
                runtimeUrl: result.runtimeUrl,
                pending: [],
                finished: false,
            };
            session.timer = window.setInterval(() => send(false, false), flushSeconds * 1000);

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
            button.classList.add('d-none');
            frame.focus();
        } catch {
            setStatus(root, root.dataset.errorText);
            button.disabled = false;
        }
    });
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
