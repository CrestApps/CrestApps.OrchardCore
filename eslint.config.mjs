// Lints the browser scripts every module builds from its Assets folder, and the build scripts. Run with `npm run lint`.
import js from "@eslint/js";
import globals from "globals";

// Globals a page defines before the module scripts run, which the scripts read as bare names.
const pageGlobals = {
    signalR: "readonly",          // @microsoft/signalr, loaded ahead of the hub connections.
    flatpickr: "readonly",        // The flatpickr date picker the range picker and query builder decorate.
    flatpickrCulture: "readonly", // Set on window by Resources' flatpickr-culture.js.
    confirmDialog: "readonly",    // Orchard Core's admin theme confirmation dialog, used by the list bulk actions.
    Stripe: "readonly",           // Stripe.js, loaded from js.stripe.com ahead of the Stripe payment scripts.
    checkoutPayment: "readonly",  // The checkout's payment driver (checkout-payment.js), which payment providers register with.
};

// `catch (error) { /* best effort */ }` is how the scripts say a failure is deliberately ignored; the name is not a bug.
const sharedRules = {
    "no-unused-vars": ["error", { caughtErrors: "none" }],
};

export default [
    {
        ignores: ["**/node_modules/**", "**/wwwroot/**", "**/bin/**", "**/obj/**", "src/CrestApps.Docs/**"],
    },
    {
        files: ["src/*/*/Assets/**/*.js"],
        languageOptions: {
            ecmaVersion: "latest",
            // The build bundles each source as a module, so it is linted as one: strict, with its own scope.
            sourceType: "module",
            globals: {
                ...globals.browser,
                ...pageGlobals,
            },
        },
        rules: { ...js.configs.recommended.rules, ...sharedRules },
    },
    {
        files: [".scripts/**/*.mjs", "eslint.config.mjs"],
        languageOptions: {
            ecmaVersion: "latest",
            sourceType: "module",
            globals: globals.node,
        },
        rules: { ...js.configs.recommended.rules, ...sharedRules },
    },
];
