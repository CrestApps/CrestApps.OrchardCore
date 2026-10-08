import { readdirSync, readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

// The soft phone runs inside the desktop app's narrow WebView2 window as well as the browser. There the browser's own
// prompt, alert and confirm dialogs are headed with the site's address, block every call event while open, and are cut
// off. The phone asks its questions in its own panels instead (see soft-phone/transfer-panel.js and in-app-confirm.js).
const sources = [
    'src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone.js',
    ...readdirSync('src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone')
        .filter(file => file.endsWith('.js'))
        .map(file => `src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/${file}`),
    'src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/contact-center-soft-phone.js'
];

describe('the soft phone', () => {
    it.each(sources)('%s opens no native browser dialog', source => {
        const code = readFileSync(source, 'utf8');

        expect(code).not.toMatch(/\bwindow\.(?:prompt|alert|confirm)\b|(?<![\w.$])(?:prompt|alert|confirm)\(/);
    });
});
