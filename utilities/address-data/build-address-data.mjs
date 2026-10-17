// Turns Google's address data into the address formats the Addresses module ships. The data comes from the Address
// Data Service that libaddressinput uses (https://github.com/google/libaddressinput: "Data licensed under the CC-BY
// 4.0"). Run it on purpose, when the snapshot should be refreshed, and review the result like code:
//
//   node utilities/address-data/build-address-data.mjs
//
// The repository's testdata/countryinfo.txt is not used: it is test data and lags the service (it still has Korea's
// six-digit postal codes, replaced in 2015).

import { writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const SOURCE_SERVICE = 'https://chromium-i18n.appspot.com/ssl-address/data';
const AGGREGATE_SERVICE = 'https://chromium-i18n.appspot.com/ssl-aggregate-address/data';
const SOURCE_PROJECT = 'https://github.com/google/libaddressinput';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const output = join(root, 'src', 'Core', 'CrestApps.OrchardCore.Addresses.Core', 'Data', 'address-formats.json');

// The country keys kept, renamed to the names the C# model reads.
const countryKeys = {
    fmt: 'format',
    lfmt: 'latinFormat',
    require: 'required',
    upper: 'upper',
    zip: 'postalCodePattern',
    zipex: 'postalCodeExamples',
    state_name_type: 'regionLabel',
    locality_name_type: 'cityLabel',
    sublocality_name_type: 'dependentLocalityLabel',
    zip_name_type: 'postalCodeLabel',
};

async function fetchJson(url) {
    for (let attempt = 1; ; attempt++) {
        const response = await fetch(url);

        if (response.ok) {
            return response.json();
        }

        if (attempt === 3) {
            throw new Error(`Could not download ${url}: ${response.status}`);
        }

        await new Promise(resolve => setTimeout(resolve, 1000 * attempt));
    }
}

// The aggregate endpoint returns a country with all its regions, keyed by id ("data/US", "data/US/CA").
async function readCountry(code, records) {
    const aggregate = await fetchJson(`${AGGREGATE_SERVICE}/${code}`);

    for (const [id, record] of Object.entries(aggregate)) {
        // Language variants ("data/CA--fr") repeat their parent in another language.
        if (!id.includes('--')) {
            records.set(id, record);
        }
    }
}

function split(value) {
    return typeof value === 'string' && value.length > 0 ? value.split('~') : [];
}

function pick(record) {
    const picked = {};

    for (const [from, to] of Object.entries(countryKeys)) {
        if (record[from] !== undefined && record[from] !== '') {
            picked[to] = record[from];
        }
    }

    return picked;
}

function regionsOf(code, record, records) {
    const keys = split(record.sub_keys);
    const names = split(record.sub_names);
    const latinNames = split(record.sub_lnames);
    const isoCodes = split(record.sub_isoids);
    const postalCodePrefixes = split(record.sub_zips);

    return keys.map((key, index) => {
        const own = records.get(`data/${code}/${key}`) ?? {};
        const region = { key };
        const name = own.name ?? names[index];
        const latinName = own.lname ?? latinNames[index];
        const isoCode = own.isoid ?? isoCodes[index];
        const postalCodePrefix = own.zip ?? postalCodePrefixes[index];

        if (name && name !== key) {
            region.name = name;
        }

        if (latinName && latinName !== key) {
            region.latinName = latinName;
        }

        if (isoCode) {
            region.isoCode = isoCode;
        }

        if (postalCodePrefix) {
            region.postalCodePrefix = postalCodePrefix;
        }

        return region;
    });
}

const records = new Map();
const countryList = await fetchJson(SOURCE_SERVICE);
const countryCodes = split(countryList.countries);

if (countryCodes.length === 0) {
    throw new Error('The service lists no countries.');
}

records.set('data/ZZ', await fetchJson(`${SOURCE_SERVICE}/ZZ`));

// A few at a time, to be gentle with the service.
for (let i = 0; i < countryCodes.length; i += 8) {
    await Promise.all(countryCodes.slice(i, i + 8).map(code => readCountry(code, records)));
}

const countries = {};

for (const code of countryCodes) {
    const record = records.get(`data/${code}`);

    if (!record) {
        continue;
    }

    const country = pick(record);
    const regions = regionsOf(code, record, records);

    if (regions.length > 0) {
        country.regions = regions;
    }

    countries[code] = country;
}

// One country a line keeps a data update reviewable as a diff.
const lines = Object.entries(countries).map(([code, country]) => `    ${JSON.stringify(code)}: ${JSON.stringify(country)}`);
const json = [
    '{',
    `  "source": ${JSON.stringify({ service: SOURCE_SERVICE, project: SOURCE_PROJECT, license: 'CC-BY-4.0', retrieved: new Date().toISOString().slice(0, 10) })},`,
    `  "default": ${JSON.stringify(pick(records.get('data/ZZ') ?? {}))},`,
    '  "countries": {',
    lines.join(',\n'),
    '  }',
    '}',
    '',
].join('\n');

await writeFile(output, json, 'utf8');

console.log(`Wrote ${Object.keys(countries).length} countries to ${output} (${json.length} characters).`);
