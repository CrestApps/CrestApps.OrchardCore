import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Tickets/Assets/js/ticket-board-editor.js';

const editor = globalThis.CrestAppsTicketBoardEditor;

// Each column row posts its fields as Columns[n]; after a row is added, removed or moved, the rows are numbered again
// in the order they stand.
describe('renumber', () => {
    const prefixName = 'TicketBoard.Columns';
    const prefixId = 'TicketBoard_Columns';

    it('numbers a field name', () => {
        expect(editor.renumber('TicketBoard.Columns[3].StatusIds', prefixName, prefixId, 1)).toBe('TicketBoard.Columns[1].StatusIds');
    });

    it('numbers a new row from the template', () => {
        expect(editor.renumber('TicketBoard.Columns[__index__].Name', prefixName, prefixId, 4)).toBe('TicketBoard.Columns[4].Name');
        expect(editor.renumber('TicketBoard_Columns___index___Name', prefixName, prefixId, 4)).toBe('TicketBoard_Columns_4_Name');
    });

    it('numbers an id and a label target', () => {
        expect(editor.renumber('TicketBoard_Columns_12_Status_open', prefixName, prefixId, 0)).toBe('TicketBoard_Columns_0_Status_open');
    });

    it('leaves other names alone', () => {
        expect(editor.renumber('TicketBoard.Name', prefixName, prefixId, 2)).toBe('TicketBoard.Name');
        expect(editor.renumber('', prefixName, prefixId, 2)).toBe('');
    });
});

// A status is in one column only: the first column that has it ticked keeps it.
describe('takenStatuses', () => {
    it('gives each ticked status to the first column that has it', () => {
        expect(editor.takenStatuses([
            { statusIds: ['new'] },
            { statusIds: ['open', 'new'] },
            { statusIds: [] },
        ])).toEqual({ new: 0, open: 1 });
    });
});

describe('keptDefault', () => {
    it('keeps a default that is still ticked', () => {
        expect(editor.keptDefault('requester', ['requester', 'supplier'])).toBe('requester');
    });

    it('goes back to asking when the default is no longer ticked', () => {
        expect(editor.keptDefault('requester', ['supplier'])).toBe('');
        expect(editor.keptDefault('', ['supplier'])).toBe('');
    });
});

describe('move', () => {
    it('moves a column', () => {
        expect(editor.move(['a', 'b', 'c'], 0, 2)).toEqual(['b', 'c', 'a']);
    });

    it('leaves the columns as they are for a move past either end', () => {
        expect(editor.move(['a', 'b'], 1, 2)).toEqual(['a', 'b']);
    });
});
