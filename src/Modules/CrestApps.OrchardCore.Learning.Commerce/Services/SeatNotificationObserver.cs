using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Commerce.Services;

/// <summary>
/// Tells a person their account gave them a seat in a course.
/// </summary>
internal sealed class SeatNotificationObserver : ILicensePoolObserver
{
    private readonly ILearningNotificationSender _sender;

    internal readonly IStringLocalizer S;

    public SeatNotificationObserver(ILearningNotificationSender sender, IStringLocalizer<SeatNotificationObserver> stringLocalizer)
    {
        _sender = sender;
        S = stringLocalizer;
    }

    public Task SeatGivenAsync(LicensePool pool, LicenseSeat seat, CancellationToken cancellationToken = default)
    {
        _sender.Send(seat.UserId, new LearningNotificationMessage
        {
            Subject = S["You have a seat in {0}", pool.Title ?? string.Empty],
            Summary = S["Your account gave you a seat in {0}. It is in My courses.", pool.Title ?? string.Empty],
            TextBody = S["Your account gave you a seat in {0}. It is in My courses.", pool.Title ?? string.Empty],
            Type = LearningCommerceConstants.SeatGivenNotificationType,
            ReferenceId = seat.ItemId,
            Url = "~/learning",
        });

        return Task.CompletedTask;
    }
}
