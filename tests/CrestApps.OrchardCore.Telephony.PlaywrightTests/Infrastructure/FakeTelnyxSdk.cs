namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// A stand-in for the Telnyx WebRTC SDK that the soft phone's real Telnyx adapter can drive, with real media.
/// </summary>
/// <remarks>
/// <para>
/// Each call is a pair of real peer connections in the page: the agent's end, which sends whatever stream the adapter
/// hands the call, and a far end, which sends Chromium's fake capture back. So what a test reads off the agent's sender
/// and its statistics is what a real call would put on the wire.
/// </para>
/// <para>
/// It copies the one SDK behaviour that matters here from @telnyx/webrtc 2.27.9: a call being torn down first tells its
/// listeners it is being destroyed, and then stops every live track of the stream it holds as
/// <c>options.localStream</c>.
/// </para>
/// </remarks>
public static class FakeTelnyxSdk
{
    /// <summary>
    /// The page script: installs <c>window.TelnyxWebRTC</c>, answers the adapter's registration-config request, and
    /// exposes <c>window.fakeTelnyx</c> for the test to ring the browser and hang calls up.
    /// </summary>
    public const string Script =
        """
        (function () {
            var fake = window.fakeTelnyx = { client: null, ready: false, calls: [] };
            var sequence = 0;

            var realFetch = window.fetch.bind(window);
            window.fetch = function (url, init) {
                if (String(url).indexOf('registration-config') >= 0) {
                    return Promise.resolve({
                        ok: true,
                        json: function () {
                            return Promise.resolve({
                                signaling: { authorizationUser: 'agent' },
                                credential: { value: 'secret' },
                                // The credential the phone is registered on, which it names when it asks the platform
                                // to connect a number it dialed.
                                session: { interactionId: 'fake-credential-1' },
                                ice: {},
                                media: {}
                            });
                        }
                    });
                }

                return realFetch(url, init);
            };

            function isOver(state) {
                return state === 'hangup' || state === 'destroy' || state === 'purge';
            }

            function Call(client, options, direction) {
                this.client = client;
                this.options = options || {};
                this.direction = direction;
                this.id = 'fake-call-' + (++sequence);
                this.state = 'new';
                this.peer = null;
                this.remoteStream = null;
                this.answers = 0;
                fake.calls.push(this);
            }

            Call.prototype.setState = function (state) {
                this.state = state;
                this.client.emit('telnyx.notification', { type: 'callUpdate', call: this });

                if (state === 'destroy') {
                    this.finalize();
                }
            };

            // What the SDK's _finalize does: close the media and stop every live track of options.localStream.
            Call.prototype.finalize = function () {
                var localStream = this.options.localStream;

                if (localStream && localStream instanceof MediaStream) {
                    localStream.getTracks().forEach(function (track) {
                        if (track.readyState === 'live') {
                            track.stop();
                        }
                    });
                }

                if (this.far) {
                    this.far.close();
                }

                if (this.peer && this.peer.instance) {
                    this.peer.instance.close();
                }
            };

            Call.prototype.connectMedia = function () {
                var call = this;
                var local = call.options.localStream
                    ? Promise.resolve(call.options.localStream)
                    : navigator.mediaDevices.getUserMedia({ audio: true });

                return Promise.all([local, navigator.mediaDevices.getUserMedia({ audio: true })]).then(function (streams) {
                    var near = new RTCPeerConnection();
                    var far = new RTCPeerConnection();

                    call.options.localStream = streams[0];
                    call.peer = { instance: near };
                    call.far = far;
                    near.onicecandidate = function (event) { if (event.candidate) { far.addIceCandidate(event.candidate); } };
                    far.onicecandidate = function (event) { if (event.candidate) { near.addIceCandidate(event.candidate); } };
                    near.ontrack = function (event) {
                        call.remoteStream = event.streams[0] || new MediaStream([event.track]);

                        if (call.options.remoteElement) {
                            call.options.remoteElement.srcObject = call.remoteStream;
                        }
                    };

                    streams[0].getAudioTracks().forEach(function (track) { near.addTrack(track, streams[0]); });
                    streams[1].getAudioTracks().forEach(function (track) { far.addTrack(track, streams[1]); });

                    return near.createOffer()
                        .then(function (offer) { return near.setLocalDescription(offer); })
                        .then(function () { return far.setRemoteDescription(near.localDescription); })
                        .then(function () { return far.createAnswer(); })
                        .then(function (answer) { return far.setLocalDescription(answer); })
                        .then(function () { return near.setRemoteDescription(far.localDescription); });
                }).then(function () {
                    if (!isOver(call.state)) {
                        call.setState('active');
                    }
                });
            };

            Call.prototype.answer = function (options) {
                this.answers++;

                if (options && options.remoteElement) {
                    this.options.remoteElement = options.remoteElement;
                }

                this.setState('answering');

                return this.connectMedia();
            };

            Call.prototype.hangup = function () {
                if (!isOver(this.state)) {
                    this.setState('hangup');
                    this.setState('destroy');
                }

                return Promise.resolve();
            };

            Call.prototype.hold = function () { this.setState('held'); return Promise.resolve(); };
            Call.prototype.unhold = function () { this.setState('active'); return Promise.resolve(); };

            Call.prototype.muteAudio = function () {
                (this.options.localStream ? this.options.localStream.getAudioTracks() : []).forEach(function (track) { track.enabled = false; });
            };

            Call.prototype.unmuteAudio = function () {
                (this.options.localStream ? this.options.localStream.getAudioTracks() : []).forEach(function (track) { track.enabled = true; });
            };

            function Client(options) {
                this.options = options;
                this.handlers = {};
                fake.client = this;
                fake.clientCount = (fake.clientCount || 0) + 1;
            }

            Client.prototype.on = function (name, handler) {
                (this.handlers[name] = this.handlers[name] || []).push(handler);
            };

            Client.prototype.off = function () { };

            Client.prototype.emit = function (name, payload) {
                (this.handlers[name] || []).slice().forEach(function (handler) { handler(payload); });
            };

            Client.prototype.connect = function () {
                var client = this;
                setTimeout(function () {
                    fake.ready = true;
                    client.emit('telnyx.ready', {});
                }, 0);
            };

            Client.prototype.disconnect = function () {
                fake.ready = false;
                return Promise.resolve();
            };

            Client.prototype.newCall = function (options) {
                var call = new Call(this, Object.assign({}, options), 'outbound');
                setTimeout(function () {
                    call.setState('trying');
                    call.connectMedia();
                }, 0);

                return call;
            };

            // Rings the browser with a leg, as the platform does for an offer: tagged with the offer in client state.
            fake.ringOfferLeg = function (reservationId, legId) {
                var call = new Call(fake.client, {
                    telnyxCallControlId: legId,
                    clientState: btoa(JSON.stringify({ i: 'cc-predial', r: reservationId })),
                    customHeaders: []
                }, 'inbound');

                call.setState('ringing');

                return call.id;
            };

            // Rings the browser with a leg the platform placed to it -- the agent's own leg of a number the phone asked the
            // platform to dial, or of an extension call.
            fake.ringLeg = function (legId) {
                var call = new Call(fake.client, { telnyxCallControlId: legId, customHeaders: [] }, 'inbound');

                call.setState('ringing');

                return call.id;
            };

            // Rings the browser with a colleague's extension call to this agent: the leg the platform places at the
            // destination of the colleague's call, tagged as a destination leg in client state.
            fake.ringColleagueLeg = function (legId) {
                var call = new Call(fake.client, {
                    telnyxCallControlId: legId,
                    clientState: btoa(JSON.stringify({ i: 'ob-dest', p: 'colleague-agent-leg', v: 'user-2' })),
                    customHeaders: [],
                    remoteCallerNumber: '+17785550000',
                    remoteCallerName: 'Agent One'
                }, 'inbound');

                call.setState('ringing');

                return call.id;
            };

            // Rings the browser with a leg the platform placed to hand this agent a colleague's call: tagged as a transfer
            // leg in client state, naming the party and who is handing it over.
            fake.ringTransferLeg = function (legId, partyNumber, transferredBy) {
                var call = new Call(fake.client, {
                    telnyxCallControlId: legId,
                    clientState: btoa(JSON.stringify({ i: 'ob-xfer', p: 'party-leg', h: 'colleague-leg', m: partyNumber, n: transferredBy })),
                    customHeaders: [{ name: 'X-Transfer-Leg', value: partyNumber }],
                    remoteCallerNumber: '+17785550000'
                }, 'inbound');

                call.setState('ringing');

                return call.id;
            };

            // Rings the browser with the leg the platform places at a supervisor's own phone to play them a call: tagged as a
            // monitor leg with its one-off token in client state and in the X-Monitor-Leg header, or in the header alone,
            // as the SDK that hands over no client state delivers it.
            fake.ringMonitorLeg = function (legId, token, headerOnly) {
                var options = {
                    telnyxCallControlId: legId,
                    customHeaders: [{ name: 'X-Monitor-Leg', value: token }],
                    remoteCallerNumber: '+15550000000'
                };

                if (!headerOnly) {
                    options.clientState = btoa(JSON.stringify({ i: 'cc-sv', p: 'customer-leg', y: 'agent-leg', l: token }));
                }

                var call = new Call(fake.client, options, 'inbound');

                call.setState('ringing');

                return call.id;
            };

            // Whether the browser dialed anything itself through the SDK.
            fake.placedCount = function () {
                return fake.calls.filter(function (call) { return call.direction === 'outbound'; }).length;
            };

            fake.byLeg = function (legId) {
                return fake.calls.filter(function (call) { return call.options.telnyxCallControlId === legId; })[0] || null;
            };

            // What the far end hears from the agent: the track the agent's sender carries and the bytes sent so far.
            fake.readSending = function (legId) {
                var call = fake.byLeg(legId);
                var peer = call && call.peer && call.peer.instance;

                if (!peer) {
                    return Promise.resolve({ state: call ? call.state : 'none', trackState: 'none', trackId: '', enabled: false, bytesSent: 0 });
                }

                var sender = peer.getSenders().filter(function (candidate) { return candidate.track && candidate.track.kind === 'audio'; })[0];
                var track = sender ? sender.track : null;

                return peer.getStats().then(function (report) {
                    var bytesSent = 0;
                    report.forEach(function (stat) {
                        if (stat.type === 'outbound-rtp' && stat.kind === 'audio') {
                            bytesSent += stat.bytesSent || 0;
                        }
                    });

                    return {
                        state: call.state,
                        trackState: track ? track.readyState : 'none',
                        trackId: track ? track.id : '',
                        enabled: !!(track && track.enabled),
                        bytesSent: bytesSent
                    };
                });
            };

            window.TelnyxWebRTC = { TelnyxRTC: Client };
        }());
        """;
}
