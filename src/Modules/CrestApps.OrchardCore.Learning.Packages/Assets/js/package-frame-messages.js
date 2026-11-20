// Which messages the player takes from the package frame. In strict mode the frame runs on an opaque origin, so its
// messages come from the origin "null"; with a package content host they come from the content host's origin, which
// the launch names. A message counts only when it also comes from the player's own frame and carries the protocol of
// the package's standard and the id of the current launch.

/**
 * Gets the origin the frame of a launch posts from.
 * @param {{ contentOrigin?: string|null }} session The launch.
 * @returns {string} The content host's origin, or "null" in strict mode.
 */
export function frameOrigin(session) {
    return session?.contentOrigin || 'null';
}

/**
 * Tells whether a message event comes from the frame of the current launch.
 * @param {MessageEvent} event The message event.
 * @param {Window|null} frameWindow The window of the player's frame.
 * @param {{ contentOrigin?: string|null, protocol: string, launchId: string }|null} session The current launch.
 * @returns {boolean} true when the player takes the message.
 */
export function isFrameMessage(event, frameWindow, session) {
    if (!session || !frameWindow || event.source !== frameWindow || event.origin !== frameOrigin(session)) {
        return false;
    }

    const data = event.data;

    return !!data && data.protocol === session.protocol && data.launchId === session.launchId;
}

// The protocol of the message a package window posts to the page that opened it when its session finished.
const windowProtocol = 'crestapps-package-window';

/**
 * Builds the message a package window posts to the page that opened it when the session finished.
 * @param {string} itemId The package item.
 * @returns {{ protocol: string, type: string, itemId: string }} The message.
 */
export function windowFinishedMessage(itemId) {
    return { protocol: windowProtocol, type: 'finished', itemId };
}

/**
 * Tells whether a message event says that the package window this page opened finished its session.
 * @param {MessageEvent} event The message event.
 * @param {Window|null} openedWindow The window this page opened.
 * @param {string} origin The page's own origin, which the window shares.
 * @param {string} itemId The package item.
 * @returns {boolean} true when the page takes the message.
 */
export function isWindowFinishedMessage(event, openedWindow, origin, itemId) {
    if (!openedWindow || event.source !== openedWindow || event.origin !== origin) {
        return false;
    }

    const data = event.data;

    return !!data && data.protocol === windowProtocol && data.type === 'finished' && data.itemId === itemId;
}
