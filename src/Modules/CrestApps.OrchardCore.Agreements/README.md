# Agreements

Agreements with accounts, such as support or maintenance contracts: their period, their status and the hours they
cover (a block of hours or an allowance each month, each entry of time rounded up to none, 6, 15 or 30 minutes).

- Other modules keep their own terms on an agreement (in its `Properties`) and add their editors through display
  drivers on `Agreement`.
- Modules that work under an agreement register an `IAgreementUsageProvider`; `IAgreementBalanceService` adds up the
  time of every provider in the period (the whole agreement for a block, the calendar month for an allowance).
- The **Agreements** background task expires agreements past their end and tells every `IAgreementObserver` when an
  agreement is about to end or passes 80% and 100% of its hours, once a period.
- The **Agreements** recipe step and deployment step import and export agreements; an account's page lists its
  agreements with their hours.

See the [documentation](https://orchardcore.crestapps.com/docs/modules/agreements).
