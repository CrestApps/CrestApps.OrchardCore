import { describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/send-audio.js';

const {
    OUTBOUND_SILENCE_MS,
    hasLiveAudioTrack,
    releaseSharedCapture,
    createOutboundAudioWatch,
    startOutboundAudioMonitor,
} = globalThis.CrestAppsSoftPhone;

function track(readyState = 'live', kind = 'audio') {
    return {
        kind,
        readyState,
        stop() {
            this.readyState = 'ended';
        },
    };
}

function stream(...tracks) {
    return {
        getTracks: () => tracks,
        getAudioTracks: () => tracks.filter(candidate => candidate.kind === 'audio'),
    };
}

// What the provider SDK (2.27.9) does to a call it is done with: it tells its listeners the call is being destroyed,
// and only then stops every live track of the stream it was handed as options.localStream.
function destroyLikeTheSdk(call, notify) {
    call.state = 'destroy';
    notify(call);

    const localStream = call.options.localStream;

    if (localStream) {
        localStream.getTracks().forEach(candidate => {
            if (candidate.readyState === 'live') {
                candidate.stop();
            }
        });
    }
}

describe('hasLiveAudioTrack', () => {
    it('is true for a stream carrying a live audio track', () => {
        expect(hasLiveAudioTrack(stream(track('live')))).toBe(true);
    });

    // The stream is the soft phone's long-lived container; the track inside it is what can die. A stopped track
    // raises no event, so readyState is the only way to know.
    it('is false once the track in it has been stopped', () => {
        const microphone = track('live');
        const capture = stream(microphone);

        microphone.stop();

        expect(hasLiveAudioTrack(capture)).toBe(false);
    });

    it('is false for a missing stream, an empty one, or one carrying only video', () => {
        expect(hasLiveAudioTrack(null)).toBe(false);
        expect(hasLiveAudioTrack(stream())).toBe(false);
        expect(hasLiveAudioTrack(stream(track('live', 'video')))).toBe(false);
    });
});

// Bug: every call after the first on a registration was one-way. The agent heard the caller, the caller heard nothing,
// and the soft phone's own summary read BytesSent=0 with its send track "ended". The soft phone captures the
// microphone once per registration and hands that same stream to every call; the provider SDK stops every track of the
// stream it was handed when a call ends. Nothing raises an event for a stopped track, so the next call -- a held offer
// leg answered on accept -- went out on a dead track.
describe('releaseSharedCapture', () => {
    it('keeps the shared microphone alive through the SDK tearing a finished call down', () => {
        const microphone = track('live');
        const shared = stream(microphone);
        const call = { state: 'active', options: { localStream: shared } };

        destroyLikeTheSdk(call, finished => releaseSharedCapture(finished, shared));

        expect(microphone.readyState).toBe('live');
        expect(call.options.localStream).toBeNull();
    });

    it('also releases it from a call the SDK finalizes to recover it', () => {
        const shared = stream(track('live'));
        const call = { state: 'recovering', options: { localStream: shared } };

        expect(releaseSharedCapture(call, shared)).toBe(true);
        expect(call.options.localStream).toBeNull();
    });

    // A call still in progress keeps sending on the stream; taking it away early would stop the SDK muting it.
    it.each(['ringing', 'answering', 'active', 'held', 'hangup'])('leaves a call in state "%s" alone', state => {
        const shared = stream(track('live'));
        const call = { state, options: { localStream: shared } };

        expect(releaseSharedCapture(call, shared)).toBe(false);
        expect(call.options.localStream).toBe(shared);
    });

    // A stream the SDK captured for itself is the SDK's to stop.
    it('leaves a stream that is not the shared capture alone', () => {
        const shared = stream(track('live'));
        const own = stream(track('live'));
        const call = { state: 'destroy', options: { localStream: own } };

        expect(releaseSharedCapture(call, shared)).toBe(false);
        expect(call.options.localStream).toBe(own);
    });

    it('tolerates a call without options, and no shared stream', () => {
        expect(releaseSharedCapture({ state: 'destroy' }, stream())).toBe(false);
        expect(releaseSharedCapture(null, stream())).toBe(false);
        expect(releaseSharedCapture({ state: 'destroy', options: { localStream: null } }, null)).toBe(false);
    });
});

describe('createOutboundAudioWatch', () => {
    function observeEverySecond(watch, samples) {
        return samples.map((sample, index) => watch.observe({ now: index * 1000, trackLive: true, suppressed: false, ...sample }));
    }

    it('says nothing while audio keeps leaving', () => {
        const watch = createOutboundAudioWatch();
        const results = observeEverySecond(watch, [0, 1, 2, 3, 4, 5, 6, 7, 8].map(second => ({ bytesSent: second * 1000 })));

        expect(results.every(result => result === null)).toBe(true);
        expect(watch.hasStalled()).toBe(false);
    });

    // The four answered calls of the incident: the provider received no packets from the agent, and the soft phone sent
    // zero bytes for the whole call.
    it('reports a stall once nothing has been sent for the threshold', () => {
        const watch = createOutboundAudioWatch({ thresholdMs: 5000 });
        const results = observeEverySecond(watch, new Array(8).fill({ bytesSent: 0 }));

        expect(results.indexOf('stalled')).toBe(5);
        expect(results.filter(result => result === 'stalled')).toHaveLength(1);
        expect(watch.isStalled()).toBe(true);
        expect(watch.hasStalled()).toBe(true);
    });

    it('does not report a stall a moment short of the threshold', () => {
        const watch = createOutboundAudioWatch({ thresholdMs: 5000 });
        const results = observeEverySecond(watch, new Array(5).fill({ bytesSent: 0 }));

        expect(results).not.toContain('stalled');
    });

    it('defaults to a five-second threshold', () => {
        expect(OUTBOUND_SILENCE_MS).toBe(5000);
    });

    it('reports a stall when the counter stops moving mid-call, and a resume when it moves again', () => {
        const watch = createOutboundAudioWatch({ thresholdMs: 3000 });
        const results = observeEverySecond(watch, [
            { bytesSent: 100 }, { bytesSent: 200 }, { bytesSent: 200 }, { bytesSent: 200 }, { bytesSent: 200 },
            { bytesSent: 200 }, { bytesSent: 300 },
        ]);

        expect(results).toEqual([null, null, null, null, 'stalled', null, 'resumed']);
        expect(watch.isStalled()).toBe(false);
        expect(watch.hasStalled()).toBe(true);
    });

    // A counter that goes backwards is a new sender (renegotiation), not audio lost; the next rise is progress.
    it('treats a counter reset as neither progress nor a stall', () => {
        const watch = createOutboundAudioWatch({ thresholdMs: 5000 });
        const results = observeEverySecond(watch, [{ bytesSent: 5000 }, { bytesSent: 10 }, { bytesSent: 20 }, { bytesSent: 30 }]);

        expect(results.every(result => result === null)).toBe(true);
    });

    // Muted or on hold, sending nothing the agent's voice would be in is the point; the window starts again after.
    it('never reports a stall while outbound audio is suppressed, and restarts its window afterwards', () => {
        const watch = createOutboundAudioWatch({ thresholdMs: 3000 });
        const results = observeEverySecond(watch, [
            { bytesSent: 0, suppressed: true }, { bytesSent: 0, suppressed: true }, { bytesSent: 0, suppressed: true },
            { bytesSent: 0, suppressed: true }, { bytesSent: 0, suppressed: true },
            { bytesSent: 0 }, { bytesSent: 0 }, { bytesSent: 0 }, { bytesSent: 0 },
        ]);

        expect(results.indexOf('stalled')).toBe(8);
    });

    it('does not count bytes as progress while the send track is not live', () => {
        const watch = createOutboundAudioWatch({ thresholdMs: 2000 });
        const results = observeEverySecond(watch, [
            { bytesSent: 0, trackLive: false }, { bytesSent: 10, trackLive: false }, { bytesSent: 20, trackLive: false },
        ]);

        expect(results[2]).toBe('stalled');
    });
});

describe('startOutboundAudioMonitor', () => {
    function statsWithBytes(bytesSent) {
        return new Map([
            ['out', { type: 'outbound-rtp', kind: 'audio', bytesSent }],
            ['in', { type: 'inbound-rtp', kind: 'audio', bytesReceived: 900 }],
        ]);
    }

    function fakePeer(sendTrack) {
        const peer = {
            bytesSent: 0,
            sendTrack,
            getStats: () => Promise.resolve(statsWithBytes(peer.bytesSent)),
            getSenders: () => [{ track: peer.sendTrack }],
        };

        return peer;
    }

    function fakeClock() {
        let now = 0;
        let callback = null;

        return {
            now: () => now,
            setInterval: fn => {
                callback = fn;

                return 7;
            },
            clearInterval: vi.fn(),
            async advance(ms) {
                now += ms;
                callback();
                await Promise.resolve();
                await Promise.resolve();
            },
        };
    }

    it('tells the phone when nothing leaves a connected call, naming the send track state', async () => {
        const clock = fakeClock();
        const dead = track('ended');
        const peer = fakePeer(dead);
        const onStalled = vi.fn();
        const monitor = startOutboundAudioMonitor({
            readPeer: () => peer,
            isSuppressed: () => false,
            onStalled,
            now: clock.now,
            setInterval: clock.setInterval,
            clearInterval: clock.clearInterval,
        });

        for (let second = 0; second <= 6; second++) {
            await clock.advance(1000);
        }

        expect(onStalled).toHaveBeenCalledTimes(1);
        expect(onStalled.mock.calls[0][0]).toMatchObject({ trackState: 'ended', bytesSent: 0 });
        expect(monitor.hasStalled()).toBe(true);
    });

    it('says nothing on a call whose audio flows, and stops sampling when stopped', async () => {
        const clock = fakeClock();
        const peer = fakePeer(track('live'));
        const onStalled = vi.fn();
        const monitor = startOutboundAudioMonitor({
            readPeer: () => peer,
            isSuppressed: () => false,
            onStalled,
            now: clock.now,
            setInterval: clock.setInterval,
            clearInterval: clock.clearInterval,
        });

        for (let second = 0; second <= 8; second++) {
            peer.bytesSent += 1600;
            await clock.advance(1000);
        }

        monitor.stop();

        expect(onStalled).not.toHaveBeenCalled();
        expect(clock.clearInterval).toHaveBeenCalledWith(7);
        expect(monitor.hasStalled()).toBe(false);
    });

    it('reports the resume once audio leaves again', async () => {
        const clock = fakeClock();
        const peer = fakePeer(track('live'));
        const onStalled = vi.fn();
        const onResumed = vi.fn();

        startOutboundAudioMonitor({
            readPeer: () => peer,
            isSuppressed: () => false,
            onStalled,
            onResumed,
            now: clock.now,
            setInterval: clock.setInterval,
            clearInterval: clock.clearInterval,
        });

        for (let second = 0; second <= 6; second++) {
            await clock.advance(1000);
        }

        peer.bytesSent = 3200;
        await clock.advance(1000);

        expect(onStalled).toHaveBeenCalledTimes(1);
        expect(onResumed).toHaveBeenCalledTimes(1);
    });

    it('holds its tongue while the agent is muted or holding', async () => {
        const clock = fakeClock();
        const peer = fakePeer(track('live'));
        const onStalled = vi.fn();

        startOutboundAudioMonitor({
            readPeer: () => peer,
            isSuppressed: () => true,
            onStalled,
            now: clock.now,
            setInterval: clock.setInterval,
            clearInterval: clock.clearInterval,
        });

        for (let second = 0; second <= 10; second++) {
            await clock.advance(1000);
        }

        expect(onStalled).not.toHaveBeenCalled();
    });

    it('waits for a peer connection before it samples anything', async () => {
        const clock = fakeClock();
        const onStalled = vi.fn();

        startOutboundAudioMonitor({
            readPeer: () => null,
            onStalled,
            now: clock.now,
            setInterval: clock.setInterval,
            clearInterval: clock.clearInterval,
        });

        for (let second = 0; second <= 10; second++) {
            await clock.advance(1000);
        }

        expect(onStalled).not.toHaveBeenCalled();
    });
});
