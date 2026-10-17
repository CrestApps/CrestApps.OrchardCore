# Third-party notices

## Google address data

`Data/address-formats.json` is derived from Google's address data, as served by the Address Data Service
(<https://chromium-i18n.appspot.com/ssl-address/data>) that [libaddressinput](https://github.com/google/libaddressinput)
uses, retrieved 2026-10-16.

Copyright Google. The data is licensed under the Creative Commons Attribution 4.0 International License
(CC BY 4.0): <https://creativecommons.org/licenses/by/4.0/>.

Changes made: the data was converted to JSON; language variants and regions below the first level were left out;
only the address format, required and capitalized parts, postal code patterns and examples, part names, and
first-level regions (key, name, Latin name, ISO 3166-2 code, postal code prefix) were kept, under different key names.
`utilities/address-data/build-address-data.mjs` in the CrestApps.OrchardCore repository makes the conversion.

The data is provided as is, without warranties. Google does not endorse CrestApps or this project.
