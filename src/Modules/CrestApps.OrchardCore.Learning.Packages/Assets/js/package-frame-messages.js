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
