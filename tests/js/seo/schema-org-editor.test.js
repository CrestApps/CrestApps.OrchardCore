import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Seo/Assets/js/schema-org-editor.js';

const editor = globalThis.CrestAppsSchemaOrgEditor;

const thing = [
    { name: 'name', kind: 'Text', level: 'Recommended', suggestions: ['Item:DisplayText'] },
    { name: 'description', kind: 'LongText', level: 'Recommended', suggestions: ['Item:Summary'] },
    { name: 'image', kind: 'Image', level: 'Recommended', suggestions: [] },
];

const data = {
    fallbackProperties: thing,
    types: [
        {
            name: 'BlogPosting',
            group: 'Articles',
            properties: [
                { name: 'headline', kind: 'Text', level: 'Recommended', suggestions: ['Item:DisplayText'] },
                { name: 'author', kind: 'Object', objectType: 'Person', level: 'Recommended', suggestions: ['Item:Author'] },
                { name: 'keywords', kind: 'TextList', level: 'Optional', suggestions: [] },
                { name: 'wordCount', kind: 'Number', level: 'Optional', suggestions: [] },
                { name: 'thumbnailUrl', kind: 'Image', level: 'Optional', suggestions: [] },
            ].concat(thing),
        },
        {
            name: 'Event',
            group: 'Events',
            properties: [
                { name: 'startDate', kind: 'DateTime', level: 'Required', suggestions: [] },
                { name: 'location', kind: 'Object', objectType: 'Place', level: 'Required', suggestions: [] },
                { name: 'location.address', kind: 'Text', level: 'Required', suggestions: [] },
                { name: 'eventStatus', kind: 'Enumeration', level: 'Recommended', suggestions: [], options: [{ value: 'https://schema.org/EventScheduled', text: 'Scheduled' }] },
                { name: 'offers', kind: 'Object', objectType: 'Offer', level: 'Recommended', suggestions: [] },
                { name: 'offers.price', kind: 'Number', level: 'Recommended', suggestions: [] },
            ].concat(thing),
        },
    ],
    sources: [
        { key: 'Item:DisplayText', text: 'Title', group: 'Content item', kind: 'Text' },
        { key: 'Item:Summary', text: 'Summary', group: 'Content item', kind: 'LongText' },
        { key: 'Item:Author', text: 'Author', group: 'Content item', kind: 'Object' },
        { key: 'Field:BlogPost.Image', text: 'Image', group: 'Fields of Blog Post', kind: 'Image' },
        { key: 'Field:BlogPost.Tags', text: 'Tags', group: 'Fields of Blog Post', kind: 'TextList' },
        { key: 'Field:BlogPost.Subtitle', text: 'Subtitle', group: 'Fields of Blog Post', kind: 'Text' },
        { key: 'Field:Event.Starts', text: 'Starts', group: 'Fields of Event', kind: 'DateTime' },
        { key: 'Field:Event.Price', text: 'Price', group: 'Fields of Event', kind: 'Number' },
    ],
};

describe('humanize', () => {
    it('turns property names into labels', () => {
        expect(editor.humanize('startDate')).toBe('Start date');
        expect(editor.humanize('offers.priceCurrency')).toBe('Offers › Price currency');
    });
});

describe('propertiesOf', () => {
    it('gives a listed type its properties, and any other type the generic ones', () => {
        expect(editor.propertiesOf(data, 'blogposting').map((p) => p.name)).toContain('headline');
        expect(editor.propertiesOf(data, 'MusicEvent')).toBe(thing);
    });
});

describe('suggestMapping', () => {
    it('takes the value the vocabulary suggests', () => {
        const headline = editor.findProperty(data, 'BlogPosting', 'headline');

        expect(editor.suggestMapping(headline, data.sources)).toMatchObject({ source: 'Content', value: 'Item:DisplayText' });
    });

    it('takes a field named for the property, or a field of the very kind', () => {
        const keywords = editor.findProperty(data, 'BlogPosting', 'keywords');
        const image = editor.findProperty(data, 'BlogPosting', 'image');
        const startDate = editor.findProperty(data, 'Event', 'startDate');

        expect(editor.suggestMapping(keywords, data.sources)).toMatchObject({ value: 'Field:BlogPost.Tags' });
        expect(editor.suggestMapping(image, data.sources)).toMatchObject({ value: 'Field:BlogPost.Image' });
        expect(editor.suggestMapping(startDate, data.sources)).toMatchObject({ value: 'Field:Event.Starts' });
    });

    it('never suggests a field of the wrong kind', () => {
        const price = { name: 'price', kind: 'Number', level: 'Recommended', suggestions: [] };

        expect(editor.scoreSource(price, { key: 'Field:Product.Price', kind: 'Image' })).toBe(0);
        expect(editor.scoreSource(price, { key: 'Field:Product.Price', kind: 'Number' })).toBeGreaterThan(0);
    });

    it('leaves to editors what the content type cannot give', () => {
        const status = editor.findProperty(data, 'Event', 'eventStatus');

        expect(editor.suggestMapping(status, data.sources)).toMatchObject({ source: 'Editor' });
    });
});

describe('suggestMappings', () => {
    it('adds the recommended properties and the optional ones a field no other row uses is named for', () => {
        const names = editor.suggestMappings(data, 'BlogPosting', []).map((m) => m.name);

        expect(names).toEqual(expect.arrayContaining(['headline', 'author', 'image', 'keywords']));
        expect(names).not.toContain('wordCount');
        expect(names).not.toContain('thumbnailUrl');
    });

    it('leaves out what the site fills in for every item', () => {
        const names = editor.suggestMappings(data, 'BlogPosting', []).map((m) => m.name);

        expect(names).not.toContain('name');
        expect(names).not.toContain('description');
    });

    it('writes an object through its properties, and skips what is mapped', () => {
        const names = editor.suggestMappings(data, 'Event', [{ name: 'startDate', source: 'Editor' }]).map((m) => m.name);

        expect(names).toEqual(expect.arrayContaining(['location.address', 'offers.price', 'eventStatus']));
        expect(names).not.toContain('location');
        expect(names).not.toContain('offers');
        expect(names).not.toContain('startDate');
    });
});

describe('missingRequired', () => {
    it('lists the required properties no row gives', () => {
        expect(editor.missingRequired(data, 'Event', [])).toEqual(['startDate', 'location', 'location.address']);
        expect(editor.missingRequired(data, 'Event', [
            { name: 'startDate', source: 'Editor' },
            { name: 'location.address', source: 'Editor' },
        ])).toEqual([]);
    });
});

describe('previewJson', () => {
    it('shows the shape of the JSON-LD with a placeholder for each value', () => {
        const preview = editor.previewJson(data, 'Event', [
            { name: 'startDate', source: 'Content', value: 'Field:Event.Starts' },
            { name: 'offers.price', source: 'Fixed', value: '10' },
            { name: 'eventStatus', source: 'Editor' },
        ], { entered: 'entered' });

        expect(preview).toEqual({
            '@context': 'https://schema.org',
            '@type': 'Event',
            startDate: '‹Starts›',
            offers: { '@type': 'Offer', price: '10' },
            eventStatus: '‹entered›',
        });
    });
});
