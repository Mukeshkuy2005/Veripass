namespace VeriPass.Domain.ValueObjects;

public sealed class Address
{
    public string Line1 { get; }
    public string? Line2 { get; }
    public string City { get; }
    public string? District { get; }
    public string State { get; }
    public string Pincode { get; }
    public string Country { get; }

    public Address(string line1, string? line2, string city, string? district, string state, string pincode, string country = "India")
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        District = district;
        State = state;
        Pincode = pincode;
        Country = country;
    }

    public override string ToString() =>
        string.Join(", ", new[] { Line1, Line2, City, District, State, Pincode, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
