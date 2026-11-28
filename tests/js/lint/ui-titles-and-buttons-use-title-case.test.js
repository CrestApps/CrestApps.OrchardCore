import { readdirSync, readFileSync } from 'node:fs';
import { join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// Page titles, breadcrumb items, admin menu labels and button labels are Title Case, as in Orchard Core
// ("Edit Content Type", "Add Role", "Save & Continue"). Field labels, hints, messages and tooltips stay sentence
// case. The rules below are the ones AGENTS.md ("Text Casing") describes:
//  - capitalize every word, except a, an, the, and, but, or, nor, for, as, at, by, in, of, on, to, up, via, per, vs;
//  - always capitalize the first word, the last word and the word after a colon;
//  - capitalize the particle of a phrasal verb in a short label ("Sign In", "Sign In to Accept", "Move Up", "Log Out"),
//    but not a preposition ("Pay in Full", "Cash on Delivery", "Open in Calendar");
//  - leave words that already have inner capitals (AI, MCP, GitHub), placeholders and markup alone.
const sourceDirectory = 'src';
const small = new Set(['a', 'an', 'the', 'and', 'but', 'or', 'nor', 'for', 'as', 'at', 'by', 'in', 'of', 'on', 'to', 'up', 'via', 'per', 'vs', 'vs.']);
const particles = new Set(['in', 'on', 'up', 'out', 'off', 'down', 'over', 'back']);
const phrasalFollowers = new Set(['to', 'with', 'from', 'again', 'and', 'or', 'as', 'for', 'now']);

function capitalizePart(part, force, isSubPart, partCount) {
    if (!part || /[A-Z]/.test(part.slice(1)) || /^[A-Z0-9]/.test(part)) {
        return part;
    }

    const lower = part.toLowerCase();

    if ((!isSubPart && !force && small.has(lower)) || (isSubPart && partCount > 1 && small.has(lower))) {
        return lower;
    }

    return part[0].toUpperCase() + part.slice(1);
}

function capitalizeWord(word, force) {
    if (/[{}@<>/\\=_$%]/.test(word)) {
        return word;
    }

    const [, prefix, core, suffix] = /^([^A-Za-z0-9]*)(.*?)([^A-Za-z0-9]*)$/.exec(word);

    if (!core) {
        return word;
    }

    const parts = core.split('-');

    return prefix + parts.map((part, index) => capitalizePart(part, force && index === 0, index > 0, parts.length)).join('-') + suffix;
}

function titleCase(text) {
    const tokens = text.split(/(\s+)/);
    const words = tokens.map((token, index) => ({ token, index })).filter(({ token }) => token.trim() !== '');
    let afterColon = false;

    words.forEach(({ token, index }, position) => {
        const lower = token.toLowerCase();
        const isLast = position === words.length - 1;

        // "Sign In", "Sign In to Accept", "Log In With GitHub" are phrasal verbs, while "Pay in Full",
        // "Cash on Delivery" and "Open in Calendar" are prepositions.
        const phrasal = position === 1
            && words.length <= 4
            && particles.has(lower)
            && (isLast || phrasalFollowers.has(words[position + 1].token.toLowerCase()));
        const force = position === 0 || isLast || afterColon || phrasal;

        if (position === 1 && !phrasal && (lower === 'in' || lower === 'on')) {
            tokens[index] = lower;
        } else {
            tokens[index] = /^e-mails?$/i.test(token) ? `E-${token.slice(2)}` : capitalizeWord(token, force);
        }

        afterColon = /[:?!]$/.test(token) || ['-', '—', '–'].includes(token);
    });

    return tokens.join('');
}

// Sentences, long tooltips and screen-reader hints are not labels.
function isLabel(text, wordLimit) {
    const words = text.trim().split(/\s+/).filter(Boolean);

    return /[A-Za-z]/.test(text)
        && !/^\s*\(/.test(text)
        && !/[.!?]\s*$/.test(text.trim())
        && words.length <= wordLimit
        && !/[.,;:?!]/.test(text.replace(/:\s*\{\d+\}$/, ''));
}

// The localized literals of the breadcrumb title attribute, the breadcrumb items and the visible text of buttons.
const localized = /\b[TS]\[\s*"((?:[^"\\]|\\.)*)"/g;

function candidates(text) {
    const found = [];
    const add = (kind, source, wordLimit) => {
        for (const match of source.matchAll(localized)) {
            if (isLabel(match[1], wordLimit)) {
                found.push({ kind, text: match[1] });
            }
        }
    };

    for (const [, attributes] of text.matchAll(/<breadcrumb\s([^>]*?)>/g)) {
        add('breadcrumb title', /\btitle="([^"]*(?:"[^"]*")*[^"]*)"/.exec(attributes)?.[0] ?? '', 6);
    }

    for (const [, content] of text.matchAll(/<breadcrumb-item\b[^>]*>([^<]*)<\/breadcrumb-item>/g)) {
        add('breadcrumb item', content, 6);
    }

    // The confirmation dialog of a button or link: its title and its two buttons (the message is a sentence).
    for (const [, label] of text.matchAll(/\bdata-(?:title|ok-text|cancel-text)="(@T\[[^"]*"[^"]*")/g)) {
        add('confirmation dialog', label, 6);
    }

    for (const [, column] of text.matchAll(/<admin-list-column\b[^>]*?\btitle="(@T\[[^"]*"[^"]*")/g)) {
        add('list column', column, 6);
    }

    for (const [, name] of text.matchAll(/\.(?:Card|Tab|Column)\(\s*(S\["(?:[^"\\]|\\.)*")/g)) {
        add('card, tab or column name', name, 6);
    }

    for (const [, tag, attributes, content] of text.matchAll(/<(button|a)\b([^>]*)>([\s\S]*?)<\/\1>/g)) {
        if (tag === 'button' || /class="[^"]*\b(btn|dropdown-item)\b/.test(attributes)) {
            add('button', content.replace(/<[^>]*>/g, ' '), 6);
        }
    }

    return found;
}

function views() {
    return readdirSync(sourceDirectory, { recursive: true })
        .map(file => String(file))
        .filter(file => (file.endsWith('.cshtml') || file.endsWith('.cs')) && !file.split(sep).some(part => part === 'bin' || part === 'obj' || part === 'node_modules'))
        .map(file => join(sourceDirectory, file))
        .map(path => ({ path, text: readFileSync(path, 'utf8') }));
}

describe('title case rules', () => {
    it.each([
        ['Add price list', 'Add Price List'],
        ['Back to queue', 'Back to Queue'],
        ['Sign in with GitHub', 'Sign In With GitHub'],
        ['Chat: {0}', 'Chat: {0}'],
        ['Edit \'{0}\' deployment', 'Edit \'{0}\' Deployment'],
        ['New AI agent platform', 'New AI Agent Platform'],
        ['Move up', 'Move Up'],
        ['No-show', 'No-Show'],
        ['Send e-mail', 'Send E-mail'],
        ['Pay On Account', 'Pay on Account'],
        ['Cash On Delivery', 'Cash on Delivery'],
        ['Sign in to accept', 'Sign In to Accept'],
        ['Hand in again', 'Hand In Again'],
        ['Open In Calendar', 'Open in Calendar'],
    ])('%s is written %s', (text, expected) => {
        expect(titleCase(text)).toBe(expected);
    });
});

describe('page titles, breadcrumb items and buttons', () => {
    const all = views();
    const used = all.flatMap(({ path, text }) => candidates(text).map(candidate => ({ path, ...candidate })));

    it('are found, so the check below is not passing on nothing', () => {
        expect(used.filter(({ kind }) => kind === 'breadcrumb title').length).toBeGreaterThan(500);
        expect(used.filter(({ kind }) => kind === 'breadcrumb item').length).toBeGreaterThan(300);
        expect(used.filter(({ kind }) => kind === 'button').length).toBeGreaterThan(1500);
        expect(used.filter(({ kind }) => kind === 'list column').length).toBeGreaterThan(100);
        expect(used.filter(({ kind }) => kind === 'card, tab or column name').length).toBeGreaterThan(500);
    });

    it('are written in Title Case', () => {
        const wrong = used
            .filter(({ text }) => titleCase(text) !== text)
            .map(({ path, kind, text }) => `${path}: ${kind} "${text}" should be "${titleCase(text)}"`);

        expect(wrong).toEqual([]);
    });
});
