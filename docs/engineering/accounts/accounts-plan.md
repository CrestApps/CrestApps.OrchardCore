---
sidebar_label: "Accounts Plan"
sidebar_position: 1
title: Accounts — Project Plan
description: Make the Account content item the one organization record, in its own Accounts module that CRM, Commerce and Field Service extend, and rebuild Commerce B2B selling on it in place of the Companies module.
---

# Accounts — Project Plan

> **Status: approved, in progress.** Written against `cbaf74596` (main). Each step below is one commit on
> `claude/accounts-module`, all in one PR. Revised on 2026-11-03: the Companies module is deleted rather than migrated
> (see [Commerce: Accounts B2B](#commerce-accounts-b2b)).

## Goal

An account is the business that contacts, leads, opportunities, orders, assets and work orders belong to. Today there
are two of them:

- **CRM account.** A content item of type `Account` with the empty marker part `AccountPart`, defined in
  Omnichannel.
- **Commerce company.** A `Company` catalog document in the `Companies` YesSql collection.

After this plan there is one: the `Account` content item. It lives in a new **Accounts** module that any module
extends with parts, settings and display-driver cards. Commerce's B2B selling becomes a part on the account, and the Companies module is deleted.

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

### Commerce: Accounts B2B

**Revised 2026-11-03.** The Companies module was added to main only recently and has no users, so it is deleted
outright. There is no company-to-account data migration, no compatibility layer, no obsolete aliases and no
redirects. B2B selling is rebuilt as an extension of the account.

- **Module.** A new module, `CrestApps.OrchardCore.Accounts.B2B`, with feature `CrestApps.OrchardCore.Accounts.B2B`
  (category Commerce).
  - It depends on Accounts, the Commerce area, Orders, Addresses and Transactions: the same dependencies Companies had,
    plus Accounts.
  - The Accounts module itself never references Commerce.
- **Part.** `B2BAccountPart` (attachable, added to the `Account` type by the module's migration) holds:
  - `Enabled`, which means the account can order on account;
  - `TaxId` and `BillingEmail`;
  - `Buyers`: `UserId`, `UserName`, `Email`, `Role`, `OrderLimit`, and `ContactContentItemId`, which links the buyer to
    one of the account's contacts when the email matches;
  - `Locations`, `Terms` (net days, deposit, credit limit, PO required), `PriceListId`, `MonthlyBudget` and `Punchout`.
- **Editor.** The part's editor is ocat rows in cards on the account editor.
- **Account page cards.** Buyers, terms and recent orders, as `DetailAdmin` cards.
- **Index.** `AccountBuyerIndex` (`AccountContentItemId`, `UserId`, `Published`, `Latest`), a map index on content
  items. `IAccountBuyerService.GetForUserAsync(userId)` answers "which account is this signed-in user ordering for". It
  returns only a published account whose part is enabled, cached per request.
- **Contact link.** When a buyer is saved, a buyer whose email matches one contact in the account's list gets that
  contact's id. Matching is by email, case-insensitive, and only one contact may match.
- **Price lists** stay catalog records, in a `PriceLists` collection. They gain:
  - an `IRecipeStep` and recipe step `PriceLists`;
  - a deployment step;
  - recipe-schema entries.
- **Services.** All of them are ported from Companies to the account:
  - terms tender, checkout step, approvals and budget, payment gate;
  - price provider, punchout, order handler;
  - the storefront page and the API;
  - the Report Builder data source (`B2BAccounts`, `B2BBuyers`, `PriceLists`, `PriceListEntries`).
- **Admin pages.**
  - Commerce › B2B accounts is the content list of account types, filtered to accounts with the part enabled.
  - Price lists keep their own admin pages, on AdminList.
- **Permissions.** `ManageB2BAccounts` and `ManagePriceLists`, given to the Administrator stereotype.
- **Names.** Every `Company` name goes:
  - `Order.CompanyId` becomes `Order.AccountId`, and the same for `OrderIndex` and `OrderQuery`;
  - `ApiOrder.companyId` becomes `accountId`;
  - the API routes become `account/b2b` and `checkout/{id}/b2b`;
  - the terms tender key becomes `account-terms`;
  - the reports source becomes `B2BAccountSource`.
- **Order index column.** An Orders migration step adds `AccountId` and its index, and drops `CompanyId` and its
  index. Existing dev databases lose nothing that matters, since no company data is kept.
- **Removed.**
  - The `CrestApps.OrchardCore.Companies` project.
  - Its Targets and test references.
  - Its tests, which are rewritten against accounts.
  - Its docs pages, which are replaced by `modules/accounts-b2b.md` and `user-manual/commerce/b2b-accounts.md`.
  - The `Companies` YesSql collection.

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
- **Accounts B2B.** Not compiled against by another project, so it is not governed and gets no baseline.

### Tests and checks (every step)

- **Unit tests.**
  - Accounts: child-type rules, list synchronizer, search endpoint (`{ value, text }`, permission check), part handler,
    picker.
  - B2B: buyer lookup (enabled, published, disabled, several accounts), contact matching, price provider, payment gate
    and approvals, terms tender reference, punchout with an account id, price list recipe and deployment round trip.
- **Upgrade tests.** These run against a real SQLite YesSql store:
  - A database where CRM created the `Account` type, with accounts and contacts, upgrades with the type, the contents
    and the list settings unchanged.
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
    - a copy built on main with CRM accounts and contacts, then upgraded on this branch.
  - What it covers:
    - accounts;
    - contacts on an account;
    - the account page cards;
    - a B2B order as a buyer, with terms, approval and the price list;
    - the upgraded CRM accounts.

## Steps and commits

1. **Plan.** This page.
2. **Accounts module.** Abstractions, Core, module, migration, page, search, picker, permissions, recipe, deployment
   step, docs and tests. Omnichannel still defines its own part in this commit. The Accounts migration only creates
   what is missing, so both can be on together.
3. **Omnichannel on Accounts.** References, feature dependency, removals, CRM `DetailAdmin` cards, baselines, the
   enable-on-upgrade step, docs and tests.
4. **Accounts B2B.**
   - The new module: `B2BAccountPart`, `AccountBuyerIndex` and the services ported from Companies.
   - `Order.AccountId`, the API and the reports.
   - Price lists with their recipe and deployment steps.
   - The Companies module deleted.
   - Docs and tests.
5. **Checks.** The suite, the baselines, the docs checks and the browser pass, with fixes for anything they find.

## Decisions

1. **Delete Companies** (revised 2026-11-03, by Mike): no data migration or compatibility layer, since the module was
   new on main and has no users. B2B is a new `Accounts.B2B` module on the account.
2. **Keep the Omnichannel account data in place**: the type and part keep their names. The Accounts migration only
   creates what is missing, so existing CRM accounts upgrade with no step.
3. **No type forwarding** for `AccountPart`; code that uses it changes one `using`.
