---
sidebar_label: "Accounts Plan"
sidebar_position: 1
title: Accounts — Project Plan
description: Make the Account content item the one organization record, in its own Accounts module that CRM, Commerce and Field Service extend, and fold the Commerce B2B company into it.
---

# Accounts — Project Plan

> **Status: proposed.** Written against `cbaf74596` (main). Nothing is built yet. Each step below is one commit on
> `claude/accounts-module`, all in one PR.

## Goal

An account is the business that contacts, leads, opportunities, orders, assets and work orders belong to. Today there
are two of them:

- **CRM account.** A content item of type `Account` with the empty marker part `AccountPart`, defined in
  Omnichannel.
- **Commerce company.** A `Company` catalog document in the `Companies` YesSql collection.

After this plan there is one: the `Account` content item. It lives in a new **Accounts** module that any module
extends with parts, settings and display-driver cards. Commerce's B2B data becomes a part on the account.

## What exists today

### CRM accounts (Omnichannel)

| Where | What |
|---|---|
| `Omnichannel.Core/Models/AccountPart.cs`, `AccountPartSettings.cs` | The part (an empty marker) and its settings (`OfferedContentTypes`). Namespace `CrestApps.OrchardCore.Omnichannel.Core.Models`. |
| `Omnichannel.Core/OmnichannelConstants.cs:67, 99` | `ContentParts.Account = "AccountPart"`, `ContentTypes.Account = "Account"`. |
| `Omnichannel.Core/Services/OmnichannelRecordKinds.cs` | `IsAccount` (has `AccountPart`) and `IsAccountChild` (a contact type that can sit in an account). |
| `Omnichannel.Core/Indexes/OpportunityIndex.cs:68` | `AccountContentItemId`, filled from `ContainedPart.ListContentItemId`. This is the only index with an account column. |
| `Omnichannel.Core` lead conversion models | `LeadConversionRequest.AccountMode/AccountName/ExistingAccountItemId`, `LeadConversionResult.Account`, `LeadPart.ConvertedAccountItemId`, `ILeadConversionHandler` context. These stay: they use an account, they don't define one. |
| `Omnichannel.Managements/Migrations/CrmMigrations.cs:68-107` | `CreateAsync` (returns 6) defines the part and, only if missing, the `Account` type (TitlePart, AccountPart, ListPart of contact types). No later step touches it. |
| `Omnichannel.Managements/CrmStartup.cs:71-138, 205` | Registers the part, `AccountPartHandler`, `AccountPickerDisplayDriver`, `CrmAccountListSynchronizer`, `AccountListPartShapeTableProvider`, the search endpoints and `AccountContentImportHandler` (only with Content Transfer). |
| `Endpoints/CrmSearchEndpoints.cs` | `GET Admin/api/crestapps/omnichannel/crm/accounts/search`. The URL is hard-coded in `Views/AccountPicker.Edit.cshtml:6` and `Views/Leads/Convert.cshtml:9`. |
| `Handlers/AccountPartHandler.cs`, `Handlers/AccountContentImportHandler.cs`, `Services/CrmAccountListSynchronizer.cs`, `Services/AccountListPartShapeTableProvider.cs`, `Drivers/AccountPickerDisplayDriver.cs` | The account handlers, the ListPart sync of contained contact types, the picker. |
| `Handlers/CrmContentDefinitionEventHandler.cs`, `Handlers/CrmRecipeEventHandler.cs` | Call the list synchronizer when types change or a recipe runs. |
| `Services/OmnichannelContentTypeProvider.cs` | `Markers.Account`, `IsAccountContentType`, `GetAccountContentTypes(Async)`. |
| `Services/CrmAdminMenu.cs:43-73` | Interaction Center › Accounts. |
| `Services/CrmContentsAdminListFilterProvider.cs:59` | The `account:` filter on opportunities. |
| `Controllers/ActivitiesController.cs:351-467` | `omnichannel/accounts/{id}/activities`. |
| Lead conversion: `LeadConversionService`, `LeadMatchFinder.FindAccountsAsync`, `LeadsController`, `ConvertLeadTask`, `LeadSubjectActionDisplayDriver` and their views | Create, find and pick accounts. These stay in Omnichannel. |
| Views | `AccountPicker.Edit/SummaryAdmin`, `ListPartNavigationAdmin-Account`, `Activities/Account`, the "View Account" buttons on `ContactNavigationAdmin` and `ListPartNavigationAdmin-OmnichannelContact`. |
| Tests | `OmnichannelRecordKindsTests`, `OmnichannelContentTypeProviderCrmTests`. There are no tests for the search endpoint, synchronizer, handler or picker. |
| Docs | `omnichannel/crm.md` (`### AccountPart`), `user-manual/leads-accounts-opportunities.md` (`## Accounts`), `user-manual/use-cases/leads-to-deals.md`, `omnichannel/management.md`, `omnichannel/index.md`, the glossary, and a few one-line mentions. |
| Field Service (PR #1252, open) | `Assets/Owners/ContactAssetsDisplayDriver.cs`, `WorkOrders/Agreements/AccountAgreementsDisplayDriver.cs`, `WorkOrders/Integrations/ContactWorkOrders.cs` use `AccountPart` from the Omnichannel namespace. Whichever PR lands second updates the `using`. |

### Commerce companies

| Where | What |
|---|---|
| `Companies/Models/Company.cs` | `Company : CatalogItem` with `Name`, `TaxId`, `BillingEmail`, `Buyers` (`UserId`, `UserName`, `Email`, `Role` Buyer/Admin, `OrderLimit`), `Locations`, `Terms` (`NetDays`, `DepositPercent`, `CreditLimit`, `RequirePurchaseOrder`), `PriceListId`, `MonthlyBudget`, `IsEnabled`, `Punchout`, timestamps. `ItemId` is an `IdGenerator` id: 26 characters, the same shape as a `ContentItemId`. |
| `Companies/Indexes/CompanyIndexes.cs`, `Migrations/CompanyMigrations.cs` (v1) | `CompanyIndex` (`ItemId`, `Name`, `IsEnabled`), `CompanyBuyerIndex` (`CompanyId`, `UserId`), `PriceListIndex`, all in the `Companies` collection. |
| `Companies/Models/PriceList.cs`, `Services/PriceListStore.cs` | Price lists, also in the `Companies` collection. The company points at the list, not the other way. |
| `Companies/Services/*` | `ICompanyStore`/`CompanyStore` (`FindByBuyerAsync`), `CompanyService` (buyer lookup, cached per user), `CompanyTermsService` (the `company-terms` tender, reference `"{companyId}:{sessionId}"`), `CompanyCheckoutStepService`, `CompanyApprovalService` (monthly budget through `OrderQuery.CompanyId`), `CompanyApprovalPaymentGate`, `CompanyPriceProvider : IBuyerPriceProvider`, `PunchoutService` (cookie carries the company id), `PunchoutCartActionProvider`. |
| `Companies/Handlers/*` | `CompanyCheckoutHandler`, `CompanyOrderHandler` (sets `Order.CompanyId`). |
| `Companies/Controllers`, `Views` | `CompaniesAdminController` (`companies/...`), `PriceListsAdminController`, `CompanyAccountController` (storefront `account/company`), `PunchoutController` (`punchout/oci/{companyId}`), the checkout shapes. |
| `Companies/Api/CompanyApiEndpoints.cs` | `GET account/company`, `PUT checkout/{id}/company`, approvals, `POST/DELETE checkout/{id}/account`. DTOs `ApiCompany*`. |
| `Companies/Reports/DataSources/*` | `CompanyReportDataSource` with the Companies, CompanyBuyers, PriceLists and PriceListEntries data sets. |
| `Companies/CompaniesPermissions.cs`, `Navigation/*` | `ManageCompanies`; Commerce › Companies and Price lists; the storefront "Company" menu. |
| `Orders.Core/Models/Order.cs:90`, `Indexes/OrderIndex.cs:36`, `Services/OrderStore.cs:90, 218` | `Order.CompanyId`, `OrderIndex.CompanyId` (`IDX_OrderIndex_CompanyId`, created in `OrderMigrations.cs:30, 46`), `OrderQuery.CompanyId`. |
| `Orders/Api/OrderApiModels.cs:38, 52, 259`, `Orders/Reports/DataSources/OrdersReportDataSet.cs:100, 148` | `ApiOrder.CompanyId`; the orders data set's `CompanyId` identifier field, linked to `CompanySource.Companies`. |
| `Reports.Abstractions/DataSources/CommerceReportDataSets.cs:372-390` | `CompanySource` names. |
| Transactions | No company column. Terms invoices are ledger transactions owned by the buyer (`OwnerId`), `Source = "company-terms"`. What a company owes is summed across its buyers. |
| Recipes, deployment steps, skills, JS | None. Companies and price lists have no recipe or deployment step today. |
| Other manifests | Nothing depends on the Companies feature. Only `Cms.Core.Targets` and the test project reference the project. |
| Tests | `Commerce/CompaniesTests`, `CompanyStoreTests`, `PriceListTests`, `PunchoutTests`, `Companies/CompanyReportDataSourceTests`, `CommerceTransactionsDependencyTests`. |
| Docs | `modules/companies.md`, `user-manual/commerce/companies.md`, `feature-reference.md`, `modules/commerce.md`, `commerce-extensions.md`, `report-builder/data-sources.md`, `user-manual/commerce/index.md`, `headless-api.md`. |

Not part of this plan: `Address.Company` and `LeadPart.Company` (free text), and the Customers module's business
customers (`Customer.CompanyName`/`TaxId`). Linking a business customer to an account is a later plan.

## Design

### Projects and features

| Project | Holds |
|---|---|
| `src/Abstractions/CrestApps.OrchardCore.Accounts.Abstractions` | `AccountPart`, `AccountPartSettings`, `AccountsConstants` (feature id, `AccountPart`, `Account`, permission names, `DetailAdmin`), `IAccountChildTypeRule` (decides which types sit in an account's list), `IAccountService` (find, search, get display text). Namespace `CrestApps.OrchardCore.Accounts`. |
| `src/Core/CrestApps.OrchardCore.Accounts.Core` | `AccountRecordKinds.IsAccount`, the list synchronizer, the search service behind the endpoint, display helpers. |
| `src/Modules/CrestApps.OrchardCore.Accounts` | Feature `CrestApps.OrchardCore.Accounts` (Content Management category). Depends on `OrchardCore.Contents`, `OrchardCore.ContentTypes`, `OrchardCore.Title`, `OrchardCore.Lists`. |

The Accounts module owns:

- **Migration.** `AccountMigrations.CreateAsync` defines the part and creates the `Account` type only if it is missing.
  This is the same check `CrmMigrations` makes today. On an existing site the type is already there and the step is a
  no-op, so no content definition is rewritten.
- **Handlers.** `AccountPartHandler` (detach children on removal), and `AccountContentImportHandler` registered only
  when Content Transfer is on.
- **Contact list.** The ListPart sync of contained types, and the `ListPartNavigationAdmin-Account` alternate.
  - Which types are children comes from the registered `IAccountChildTypeRule`s.
  - Omnichannel registers the rule "a contact type that isn't a lead". This is what `IsAccountChild` does today, so the
    Accounts module never knows what a contact is.
- **Search endpoint.** `GET Admin/api/crestapps/accounts/search?query=`, returning `{ value, text }` for the shared
  ItemSelector.
  - The old CRM route is kept as an alias for one release, so a cached page or a saved script still works.
  - The views stop hard-coding the URL and use the route name.
- **Picker.** `AccountPicker` driver and views, used by Omnichannel (contacts, lead conversion) and by B2B.
- **Account page.** `Admin/accounts/{contentItemId}` renders the account with display type `DetailAdmin`.
  - The Accounts module adds the header card and the contacts card.
  - Other modules add cards with `ContentPartDisplayDriver<AccountPart>` for `DetailAdmin`: CRM adds activities and
    opportunities, B2B adds buyers, terms and orders, and Field Service adds assets and work orders.
  - The account list's row links go to this page.
- **Menu.** The "Accounts" admin item keeps its id and position, so CRM users see it where it is today. It shows on a
  Commerce-only site too.
- **Permissions.** `ManageAccounts` and `ViewAccounts`. They are implied by the content permissions on the `Account`
  type, and given to the Administrator stereotype. The CRM roles that see accounts today keep that access through
  `ImpliedBy`.
- **Recipes and deployment.** Accounts are content items, so OC's `ContentDefinition` and `Content` steps already carry
  them and every part on them, B2B included. The Accounts module adds:
  - a recipe `accounts.recipe.json` that enables the feature;
  - an "All accounts" deployment step (a filtered `Content` step);
  - recipe-schema entries.
- **Docs.** `modules/accounts.md`, a user-manual page `user-manual/accounts.md`, and the extension guide (how to add a
  card or a part).

### Omnichannel

- Omnichannel.Core and Omnichannel.Managements reference Accounts.Abstractions. The CRM feature
  (`CrestApps.OrchardCore.Omnichannel.Crm`) adds `AccountsConstants.Feature` to its dependencies.
- **Removed:** `AccountPart`, `AccountPartSettings`, the part definition and type creation in `CrmMigrations`, the
  handler, synchronizer, picker, search endpoint and list alternate.
- **Constants and helpers.** `OmnichannelConstants.ContentParts.Account` and `ContentTypes.Account` become `[Obsolete]`
  aliases of the Accounts constants for one release. `OmnichannelRecordKinds.IsAccount` calls the Accounts helper.
- **Unchanged:** lead conversion, `OpportunityIndex.AccountContentItemId`, the `account:` filter, account activities and
  the Interaction Center. `CrmMigrations` keeps its step numbers. The `CreateAsync` body loses the account lines, which
  is safe because Accounts runs first as a dependency.
- **Moved:** CRM's activities and opportunities cards on the new account page, as `DetailAdmin` drivers.

### Commerce: the B2B part

The Companies module stays. Its project name and its feature id `CrestApps.OrchardCore.Companies` are unchanged, so
tenants that have it on keep it on. It becomes **B2B Accounts**: the Commerce extension of an account.

- **Feature.** It gains a dependency on Accounts.
- **Part.** `B2BAccountPart` (attachable, added to the `Account` type by migration) holds:
  - `Enabled`, which means the account can order on account;
  - `TaxId` and `BillingEmail`;
  - `Buyers`: `UserId`, `UserName`, `Email`, `Role`, `OrderLimit`, and `ContactContentItemId`, which links the buyer to
    one of the account's contacts when the email matches;
  - `Locations`, `Terms` (net days, deposit, credit limit, PO required), `PriceListId`, `MonthlyBudget` and `Punchout`.
- **Editor.** The part's editor is ocat rows in cards on the account editor.
- **Account page cards.** Buyers, terms and recent orders, as `DetailAdmin` cards.
- **Index.** `AccountBuyerIndex` (`AccountContentItemId`, `UserId`, `Published`, `Latest`), a map index on content
  items. `IAccountBuyerService.GetForUserAsync(userId)` answers "which account is this signed-in user ordering for". It
  returns only a published account whose part is enabled, cached per request as today.
- **Contact link.** When a buyer is saved, or a contact is added to the account, a buyer whose email matches a contact
  in the account's list gets that contact's id. Matching is by email, case-insensitive, and only one contact may match.
- **Price lists** stay catalog records. They move from `Companies` into a `PriceLists` collection, and they gain:
  - an `IRecipeStep` `PriceLists`;
  - a deployment step;
  - recipe-schema entries.
- **Services.** All of them switch from `ICompanyStore` to the account. This covers terms, checkout step, approvals,
  payment gate, price provider, punchout, order handler, the storefront `account/company` page and the reports.
- **Admin pages.**
  - Commerce › Companies is retired. Its menu item becomes "B2B accounts", the account list filtered to accounts with
    the part enabled.
  - `Admin/companies` redirects there, and `Admin/companies/edit/{id}` redirects to the account editor.
  - Price lists keep their admin pages.
- **Permission.** `ManageCompanies` keeps its name, so role assignments survive, and is relabelled "Manage B2B
  accounts".

### Ids and references

A migrated account keeps the company's id as its `ContentItemId`. Both are 26-character `IdGenerator` ids, so every
stored reference is already correct:

- `Order.CompanyId` and the `OrderIndex` rows;
- the `"{companyId}:{sessionId}"` tender references;
- punchout cookies and links (`punchout/oci/{companyId}`);
- saved reports filtered by company.

Nothing has to be rewritten. The renames are then about names, not data:

| Today | After |
|---|---|
| `Order.CompanyId` | `Order.AccountId`. `CompanyId` stays an `[Obsolete]` alias that reads and writes `AccountId`, so stored orders deserialize unchanged and no order document is rewritten. |
| `OrderIndex.CompanyId` | `OrderIndex.AccountId`. The migration adds the column, copies it with one `UPDATE`, adds `IDX_OrderIndex_AccountId`, and leaves the old column in place until a later release. |
| `OrderQuery.CompanyId` | `OrderQuery.AccountId`. |
| `ApiOrder.companyId`, `ApiCompany.id` | Add `accountId`. `companyId` stays in the JSON, with the same value, for one release. The routes `account/company` and `checkout/{id}/company` stay; they are headless client contracts. |
| Report data sets `Companies`, `CompanyBuyers` | Same data-set names, so saved reports keep working, read from accounts. Display names become "B2B accounts" and "B2B buyers". The orders data set's field becomes `AccountId`, linked to the Accounts source, with `CompanyId` kept as a hidden alias. |
| Transactions | No change: no company column exists. |

### Data migration

This is `CompanyMigrations.UpdateFrom1Async` in the B2B feature, which now depends on Accounts.

1. **Make sure Accounts is on.** The new dependency loads the Accounts feature for a tenant that had CRM or Companies
   on, but the shell descriptor doesn't list it. The CRM and Companies migrations therefore enable it through
   `IShellFeaturesManager` when it is missing. The upgrade test checks that the type, the menu and the account page are
   there afterwards.
2. **Add the part.** Add `B2BAccountPart` to the `Account` type if it is missing.
3. **Convert each company.** Read every `Company` document from the `Companies` collection. The `Company` class stays,
   internal and marked legacy, only to read them. For each one:
   - If no content item has `ContentItemId == company.ItemId`, create an `Account` with that id:
     - Title = `Name`;
     - `B2BAccountPart` from the company, with `Enabled = IsEnabled`;
     - `Published`;
     - owner = the admin who runs the upgrade, or empty.
   - If that item exists and is an account, set its `B2BAccountPart` from the company only when the part is still
     empty. A second run therefore changes nothing.
   - If that id is taken by something that isn't an account, log it, skip it, and report it on the Accounts admin page
     as "N companies could not be moved". This should never happen with generated ids, but it must not throw.
   - Link buyers to contacts by email, as above.
   - Work in batches of 50 with `SaveChangesAsync` between batches. The work is queued with `ShellScope.AddDeferredTask`
     so it runs after the migration's own transaction, with the content handlers available.
4. **Move price lists.** Copy each `PriceList` document from `Companies` to `PriceLists` under the same `ItemId`,
   skipping ids that are already there.
5. **Rename the order column.** Add `OrderIndex.AccountId` and backfill it, in the Orders migration.
6. **Keep the old documents.** Companies and price lists in the `Companies` collection are left as they are, and so are
   the `CompanyIndex` and `CompanyBuyerIndex` tables. A later release drops them. Nothing is deleted in this one, so a
   bad upgrade can be retried or rolled back.

Accounts that already exist in CRM are not merged with companies by name. Two records with the same name stay two
accounts, and the admin merges them by hand. An automatic merge would change ids and need every reference rewritten.

### Public API baselines

- **Family regex.** `PublicApiApprovalTests._familyRegex` gains `Accounts`. Accounts.Abstractions and Accounts.Core are
  compiled against by Omnichannel and B2B, so they become governed and get new `*.approved.txt` files. Each member is
  read before it is committed.
- **Omnichannel baselines.**
  - `Omnichannel.Core.approved.txt` loses `AccountPart` and `AccountPartSettings`, and shows the obsolete constants.
  - `Omnichannel.Managements.approved.txt` loses `AccountContentImportHandler` and the picker and synchronizer types.
  - Each change is reviewed in its own commit, the Omnichannel step, as a surface change.
- **No type forwarding.** A forwarded type would have to keep the old namespace. The part's data is stored by part name
  and its settings by type name only, so the namespace change is safe for content. Code that compiles against
  `CrestApps.OrchardCore.Omnichannel.Core.Models.AccountPart` changes one `using`. That includes PR #1252.
- **Companies.** Not in a governed family, so no baseline exists. Its public types change freely, and the docs record
  the API changes.

### Tests and checks (every step)

- **Unit tests.**
  - Accounts: child-type rules, list synchronizer, search endpoint (`{ value, text }`, permission check), part handler,
    picker.
  - B2B: buyer lookup (enabled, published, disabled, several accounts), contact matching, price provider, payment gate
    and approvals, terms tender reference, punchout with an account id, `Order.CompanyId` alias deserialization.
- **Migration tests.** These run against a real SQLite YesSql store, using the existing integration-test host:
  - The database before the upgrade has CRM accounts with contacts, two companies with buyers, terms, a price list and
    orders.
  - After the upgrade:
    - the accounts exist under the company ids;
    - the old CRM accounts are untouched;
    - the buyers are indexed;
    - orders resolve through `AccountId`;
    - price lists are in `PriceLists`.
  - A second run makes no change.
  - A fresh install creates the type exactly once.
- **Build and suites.**
  - Release build with `-warnaserror`.
  - The full test suite, `PublicApiApprovalTests` included.
  - `npm run check:manuals` in `src/CrestApps.Docs`.
  - `npx eslint .` and `npx vitest run`.
- **Browser pass.**
  - Two sites, both `admin` / `Password1!`:
    - a fresh local test site;
    - a copy built on main with companies, then upgraded on this branch.
  - What it covers:
    - accounts;
    - contacts on an account;
    - the account page cards;
    - a B2B order as a buyer, with terms, approval and the price list;
    - the upgraded data.

## Steps and commits

1. **Plan.** This page.
2. **Accounts module.** Abstractions, Core, module, migration, page, search, picker, permissions, recipe, deployment
   step, docs and tests. Omnichannel still defines its own part in this commit. The Accounts migration only creates
   what is missing, so both can be on together.
3. **Omnichannel on Accounts.** References, feature dependency, removals, CRM `DetailAdmin` cards, baselines, the
   enable-on-upgrade step, docs and tests.
4. **B2B accounts.**
   - `B2BAccountPart`, `AccountBuyerIndex` and the services switched over.
   - `Order.AccountId`, the API fields and the reports.
   - The `PriceLists` collection, with its recipe and deployment steps.
   - The data migration, the retired company pages and their redirects.
   - Docs and tests.
5. **Checks.** The suite, the baselines, the docs checks and the browser pass, with fixes for anything they find.

## Decisions for review

These are the calls this plan makes. Each has an alternative.

1. **Keep the Companies project and feature id as B2B Accounts** rather than retire it. A new module would leave every
   tenant with a dead enabled feature and need a feature-swap migration.
2. **Keep the company id as the account's `ContentItemId`** rather than mint new ids and rewrite orders, tender
   references and punchout links.
3. **No automatic merge** of a company into a CRM account with the same name.
4. **Leave the old `Companies` documents and tables** for one release rather than delete them after the move.
5. **Rename to `AccountId` with obsolete `CompanyId` aliases** in `Order`, the order index and the API, rather than keep
   the `CompanyId` name forever.
