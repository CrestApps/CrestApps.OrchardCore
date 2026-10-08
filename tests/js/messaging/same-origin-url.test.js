import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/shared/messaging-state.js';

const { sameOriginUrl } = globalThis.CrestAppsMessaging;

// A notification's "View" link is built from page data, so it may only ever lead back into this site: a crafted value
// such as a javascript: URL must never become a clickable link.
describe('sameOriginUrl', () => {
    const base = 'https://tenant.example.com/Admin/messaging';

    it('keeps a link into this site as a path', () => {
        expect(sameOriginUrl('/Admin/messaging/conversation/abc?x=1#m', base)).toBe('/Admin/messaging/conversation/abc?x=1#m');
        expect(sameOriginUrl('https://tenant.example.com/Admin/messaging/conversation/abc', base)).toBe('/Admin/messaging/conversation/abc');
    });

    it('refuses script and data URLs', () => {
        expect(sameOriginUrl('javascript:alert(1)', base)).toBeNull();
        expect(sameOriginUrl(' JavaScript:alert(1)', base)).toBeNull();
        expect(sameOriginUrl('data:text/html,<script>alert(1)</script>', base)).toBeNull();
    });

    it('refuses another site', () => {
        expect(sameOriginUrl('https://evil.example.net/Admin/messaging', base)).toBeNull();
        expect(sameOriginUrl('//evil.example.net/x', base)).toBeNull();
    });

    it('refuses nothing at all', () => {
        expect(sameOriginUrl('', base)).toBeNull();
        expect(sameOriginUrl(null, base)).toBeNull();
    });
});
