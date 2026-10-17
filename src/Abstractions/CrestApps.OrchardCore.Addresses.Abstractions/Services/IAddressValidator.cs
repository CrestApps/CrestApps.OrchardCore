using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses.Services;

/// <summary>
/// Checks an address against its country's rules: the components the country requires, its postal code pattern and
/// its list of regions. It checks the form of the address, not that the place exists.
/// </summary>
public interface IAddressValidator
{
    ValueTask<AddressValidationResult> ValidateAsync(Address address);
}

/// <summary>
/// What the check found, and the address tidied up: the country and postal code in capitals, and a region given by
/// its name stored as its code.
/// </summary>
public sealed class AddressValidationResult
{
    public AddressValidationResult(Address address, IReadOnlyList<AddressProblem> problems)
    {
        Address = address;
        Problems = problems ?? [];
    }

    public Address Address { get; }

    public IReadOnlyList<AddressProblem> Problems { get; }

    public bool IsValid
        => Problems.Count == 0;
}

/// <summary>
/// One thing wrong with an address. A caller turns it into a message on its own field.
/// </summary>
public readonly record struct AddressProblem(AddressComponent Component, AddressProblemKind Kind);

public enum AddressProblemKind
{
    /// <summary>
    /// The country requires the component and it is empty.
    /// </summary>
    Missing,

    /// <summary>
    /// The value does not have the country's form, such as a postal code that does not match its pattern.
    /// </summary>
    Invalid,

    /// <summary>
    /// The value is not one the country has: an unknown country, or a region not on its list.
    /// </summary>
    Unknown,

    /// <summary>
    /// The postal code does not belong to the region.
    /// </summary>
    NotInRegion,
}
