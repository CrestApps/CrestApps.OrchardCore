/*
 * Signaling region: which of the provider's points of presence this browser connects to.
 *
 * The provider resolves its signaling host by DNS geography, and warns in its own documentation that the answer
 * can be wrong -- their example is a client in India being routed to Frankfurt instead of Chennai, "call latency
 * increases". An agent working far from where the tenant was set up inherits whatever that lookup returns, and
 * nothing in the soft phone ever said which region they landed on, let alone let them change it.
 *
 * The provider SDK accepts a region, which rewrites its signaling host. This turns that into something an
 * operator can set for the tenant and an agent can override for themselves -- necessary because the choice is
 * per-person, not per-tenant: a team with agents on two continents cannot have one right answer.
 *
 * Scope, stated plainly because it is easy to assume more: this selects the SIGNALING edge. The provider
 * documents the signaling and media planes as separate systems and does not document how the media gateway for a
 * browser leg is chosen, so whether media follows is a question for measurement, not for assumption. The round
 * trip reported on a call is what settles it.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // The regions the vendored SDK accepts, with labels for the picker. Kept in step with the SDK's own Region
    // constant (lib/src/Region.d.ts); an unknown value is treated as automatic rather than passed through, so a
    // stale stored choice cannot send a client to a host that no longer exists.
    //
    // There is no Middle East entry. An agent in that part of the world has no in-region edge: Europe is the
    // nearest, and saying so here is more use than leaving them to discover it.
    var SIGNALING_REGIONS = [
        { value: '', label: 'Automatic' },
        { value: 'us-west', label: 'US West' },
        { value: 'us-central', label: 'US Central' },
        { value: 'us-east', label: 'US East' },
        { value: 'ca-central', label: 'Canada Central' },
        { value: 'eu', label: 'Europe' },
        { value: 'apac', label: 'Asia Pacific' },
        { value: 'south-asia', label: 'South Asia' }
    ];

    // Normalizes a stored, configured or chosen region. Empty means automatic: let the provider's geo-routing
    // decide, which is what every client did before this existed.
    function clampSignalingRegion(value) {
        var region = typeof value === 'string' ? value.trim().toLowerCase() : '';

        if (!region) {
            return '';
        }

        for (var i = 0; i < SIGNALING_REGIONS.length; i++) {
            if (SIGNALING_REGIONS[i].value && SIGNALING_REGIONS[i].value === region) {
                return region;
            }
        }

        return '';
    }

    // The label for a region value, for the readout.
    function describeSignalingRegion(value) {
        var region = clampSignalingRegion(value);

        if (!region) {
            return '';
        }

        for (var i = 0; i < SIGNALING_REGIONS.length; i++) {
            if (SIGNALING_REGIONS[i].value === region) {
                return SIGNALING_REGIONS[i].label;
            }
        }

        return region;
    }

    // The region this browser should actually register on, given what the operator configured for the tenant and
    // what this agent chose for themselves.
    //
    // The agent's choice wins when they made one, because the right edge is a property of where the person is
    // sitting rather than of where the tenant was set up: a team on two continents has no single right answer,
    // and the operator's value is a starting point for everyone rather than a rule over anyone. Automatic -- the
    // choice an agent starts with -- falls through to the tenant setting, and when that is empty too, to the
    // provider's own geo-routing, which is what every client did before any of this existed.
    function resolveSignalingRegion(configuredRegion, agentChoice) {
        return clampSignalingRegion(agentChoice) || clampSignalingRegion(configuredRegion);
    }

    // Adds the region to the provider client options when one is chosen. Automatic adds nothing at all, so the
    // SDK's own default routing is left exactly as it was rather than being overridden with an empty string.
    function withSignalingRegion(clientOptions, value) {
        var region = clampSignalingRegion(value);

        if (region) {
            clientOptions.region = region;
        }

        return clientOptions;
    }

    softPhone.SIGNALING_REGIONS = SIGNALING_REGIONS;
    softPhone.clampSignalingRegion = clampSignalingRegion;
    softPhone.describeSignalingRegion = describeSignalingRegion;
    softPhone.resolveSignalingRegion = resolveSignalingRegion;
    softPhone.withSignalingRegion = withSignalingRegion;
}(typeof globalThis !== 'undefined' ? globalThis : window));
