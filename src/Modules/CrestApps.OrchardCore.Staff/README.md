# CrestApps Staff

One record for the people who work for the business: employees, contractors and crew, with or without a user account.

- `StaffMember`: employment details, the reporting line, dated team memberships, capabilities, time zone, home base and work location; other modules keep their data in its `Properties`.
- Services: `IStaffMemberManager`, `IStaffDirectory`, `IStaffHierarchy`, `IStaffScopeService`, `IStaffTimeZoneResolver`, `IStaffAccountService`, `IStaffBadgeService`, `IStaffHistoryService`.
- Extension points: `DisplayDriver<StaffMember>` cards, `AddStaffCapability`, `IStaffLifecycleObserver`, `IStaffHomeLinkProvider`, `AddStaffScopePermissions`, the `StaffPicker` view component and the `Staff` user audience.
- Features: Staff, Staff Compensation (pay and cost rates), Staff Badges (PINs), Staff Organization Chart.
- Admin: **Staff > All Staff**, **Staff > My Profile**, **Staff > Organization Chart**, **Configuration > Settings > Staff**, and a **Staff** card on the user editor.
- Recipes and deployment: the `Staff` step; reports: the **Staff** data source; workflows: Staff Member Hired, Staff Member Left, Staff Manager Changed; AI tools: `staffFind`, `staffReportingLine`.

Workforce, Contact Center, Dispatch, Work Orders, Assets, Scheduling, Point of Sale, Telephony, Time Tracker and Accounts build on it. See the [Staff documentation](https://orchardcore.crestapps.com/docs/modules/staff).
