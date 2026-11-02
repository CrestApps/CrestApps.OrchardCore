using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// Checks the code in an appointment's manage link, the only thing that lets someone without an account see, move or
/// cancel it.
/// </summary>
internal static class AppointmentManageToken
{
    /// <summary>
    /// Gets whether a code is the appointment's own manage code. The comparison takes the same time however much of the
    /// code matches, so the code can't be guessed a character at a time.
    /// </summary>
    /// <param name="appointment">The appointment.</param>
    /// <param name="token">The code given.</param>
    /// <returns><see langword="true"/> when it's the appointment's code.</returns>
    public static bool Matches(Appointment appointment, string token)
    {
        if (appointment is null || string.IsNullOrEmpty(appointment.ManageToken) || string.IsNullOrEmpty(token))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(appointment.ManageToken), Encoding.UTF8.GetBytes(token));
    }
}
