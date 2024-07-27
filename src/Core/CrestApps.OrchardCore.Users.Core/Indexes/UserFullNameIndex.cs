using YesSql.Indexes;

namespace CrestApps.OrchardCore.Users.Core.Indexes;

public class UserFullNameIndex : MapIndex
{
    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string MiddleName { get; set; }

    public string DisplayName { get; set; }
}
