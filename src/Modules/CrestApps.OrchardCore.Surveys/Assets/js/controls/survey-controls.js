// Wires the question types that need script: ranking lists, sliders, constant sums and signatures. Every control
// still posts plain form inputs, so a page works without this script.
import { initConstantSums } from './constant-sum.js';
import { initRanking } from './ranking.js';
import { initSignatures } from './signature.js';
import { initSliders } from './slider.js';

function init(root) {
    initRanking(root);
    initSliders(root);
    initConstantSums(root);
    initSignatures(root);
}

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => init(document));
} else {
    init(document);
}

document.addEventListener('survey:page-loaded', (event) => init(event.target instanceof Element ? event.target : document));
