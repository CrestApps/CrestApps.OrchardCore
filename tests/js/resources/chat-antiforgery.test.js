// @vitest-environment node
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { JSDOM } from 'jsdom';
import { describe, expect, it } from 'vitest';

// MD6: the package's chat session document endpoints require an antiforgery token. The vendored @crestapps/ai-chat-ui
// build sends the chat configuration's antiforgeryToken (which AIChatUIConfiguration adds) as the RequestVerificationToken
// header on the upload and the removal, to the site's own origin only.
const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const read = relative => fs.readFileSync(path.join(repo, relative), 'utf8');

const markers = read('src/Modules/CrestApps.OrchardCore.Resources/wwwroot/vendors/crestapps/chat-markers.js');
const aiChat = read('src/Modules/CrestApps.OrchardCore.AI.Chat/wwwroot/scripts/ai-chat.js');

function page() {
    const dom = new JSDOM('<!doctype html><html><head></head><body></body></html>', { runScripts: 'outside-only', url: 'https://site.example/admin' });

    dom.window.eval(markers);

    return dom.window;
}

describe('the vendored chat scripts and the antiforgery token', () => {
    it('send the configuration token as the RequestVerificationToken header to the site itself', () => {
        const window = page();
        const headers = window.CoreAIAntiforgery.headers({ antiforgeryToken: 'token-1' }, { 'Content-Type': 'application/json' }, '/ai/chat-sessions/remove-document');

        expect(headers.RequestVerificationToken).toBe('token-1');
        expect(headers['Content-Type']).toBe('application/json');
        window.close();
    });

    it('never send it to another origin', () => {
        const window = page();
        const headers = window.CoreAIAntiforgery.headers({ antiforgeryToken: 'token-1' }, undefined, 'https://elsewhere.example/upload');

        expect(headers.RequestVerificationToken).toBeUndefined();
        window.close();
    });

    it('post the session document upload and removal with those headers', () => {
        expect(aiChat).toContain('headers: window.CoreAIAntiforgery.headers(config, void 0, config.uploadDocumentUrl)');
        expect(aiChat).toContain('headers: window.CoreAIAntiforgery.headers(config, { "Content-Type": "application/json" }, config.removeDocumentUrl)');
    });
});
