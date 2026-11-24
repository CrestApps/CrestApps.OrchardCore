// The runtime calls of a package launch, sent one at a time and in order. Each call gets the next sequence number of the
// launch, and the server applies nothing at or below the last number it applied, so an older call can never overwrite a
// newer one. A call is built when its turn comes (it takes the values the content set until then), a busy answer is sent
// again with the same values and number before anything newer, and a call whose answer was lost is sent again the same
// way: the server, having applied its number, does not apply it twice.

const defaultAttempts = 5;
const defaultRetryDelay = 250;

function wait(milliseconds) {
    return milliseconds > 0 ? new Promise(resolve => setTimeout(resolve, milliseconds)) : Promise.resolve();
}

/**
 * Creates the queue of a launch.
 * @param {(payload: object, keepalive: boolean) => Promise<object|null>} post Sends a payload; resolves to the answer, or
 *   null when no answer came.
 * @param {{ attempts?: number, retryDelay?: number }} [options] How often and how long apart a call is sent again.
 * @returns {{ send: (build: () => object|null, keepalive?: boolean) => Promise<object|null>, idle: () => boolean }}
 */
export function createRuntimeQueue(post, options = {}) {
    const attempts = options.attempts ?? defaultAttempts;
    const retryDelay = options.retryDelay ?? defaultRetryDelay;
    let sequence = 0;
    let tail = Promise.resolve();
    let busy = 0;

    const run = async (build, keepalive) => {
        const payload = build();

        if (!payload) {
            return null;
        }

        payload.sequence = ++sequence;

        for (let attempt = 1; attempt <= attempts; attempt++) {
            let result;

            try {
                result = await post(payload, keepalive === true);
            } catch {
                result = null;
            }

            if (result && !result.retry) {
                return result;
            }

            if (attempt < attempts) {
                await wait(retryDelay * attempt);
            }
        }

        return null;
    };

    return {
        send(build, keepalive) {
            busy++;

            const next = tail.then(() => run(build, keepalive));

            tail = next.catch(() => null).finally(() => { busy--; });

            return next;
        },
        idle() {
            return busy === 0;
        },
    };
}
