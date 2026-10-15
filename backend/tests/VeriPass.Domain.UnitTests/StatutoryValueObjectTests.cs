using VeriPass.Domain.Entities;
using VeriPass.Domain.Enums;
using VeriPass.Domain.ValueObjects;
using Xunit;

namespace VeriPass.Domain.UnitTests;

public class StatutoryValueObjectTests
{
    [Theory]
    [InlineData("27AAACW1234F1Z5", true, "27", "AAACW1234F")]
    [InlineData("07AAAAA0000A1Z5", true, "07", "AAAAA0000A")]
    [InlineData("invalid-gst", false, "", "")]
    [InlineData("27AAACW1234F1", false, "", "")]
    public void Gstin_Validation_WorksCorrectly(string input, bool shouldBeValid, string expectedState, string expectedPan)
    {
        var isValid = Gstin.TryParse(input, out var gstin);
        Assert.Equal(shouldBeValid, isValid);

        if (shouldBeValid)
        {
            Assert.NotNull(gstin);
            Assert.Equal(expectedState, gstin!.StateCode);
            Assert.Equal(expectedPan, gstin.Pan);
        }
    }

    [Theory]
    [InlineData("AAACW1234F", true, 'C')]
    [InlineData("ABCDE1234F", true, 'D')]
    [InlineData("INVALIDPAN", false, ' ')]
    [InlineData("12345ABCDE", false, ' ')]
    public void PanNumber_Validation_WorksCorrectly(string input, bool shouldBeValid, char expectedEntity)
    {
        var isValid = PanNumber.TryParse(input, out var pan);
        Assert.Equal(shouldBeValid, isValid);

        if (shouldBeValid)
        {
            Assert.NotNull(pan);
            Assert.Equal(expectedEntity, pan!.EntityType);
        }
    }

    [Theory]
    [InlineData("U28195MH2018PTC123456", true, false, "MH")]
    [InlineData("L28195DL2010PLC123456", true, true, "DL")]
    [InlineData("INVALIDCIN", false, false, "")]
    public void CinNumber_Validation_WorksCorrectly(string input, bool shouldBeValid, bool expectedListed, string expectedState)
    {
        var isValid = CinNumber.TryParse(input, out var cin);
        Assert.Equal(shouldBeValid, isValid);

        if (shouldBeValid)
        {
            Assert.NotNull(cin);
            Assert.Equal(expectedListed, cin!.IsListed);
            Assert.Equal(expectedState, cin.StateCode);
        }
    }

    [Theory]
    [InlineData("UDYAM-MH-01-0012345", true)]
    [InlineData("UDYAM-DL-02-1234567", true)]
    [InlineData("INVALID-UDYAM", false)]
    public void UdyamNumber_Validation_WorksCorrectly(string input, bool shouldBeValid)
    {
        var isValid = UdyamNumber.TryParse(input, out var udyam);
        Assert.Equal(shouldBeValid, isValid);
    }
}

public class BusinessEntityTests
{
    [Fact]
    public void Organization_IndependentFromSupplierOrBuyer()
    {
        var org = new Organization
        {
            Name = "Tata Steel Ltd",
            Type = OrganizationType.BOTH
        };

        Assert.Equal(OrganizationType.BOTH, org.Type);
        Assert.True(org.IsActive);
    }

    [Fact]
    public void Business_DefaultVerificationStatus_IsUnverified()
    {
        var business = new Business
        {
            LegalName = "Acme Corp Pvt Ltd",
            BusinessIdNumber = "VP-IN-2026-00001"
        };

        Assert.Equal(VerificationStatus.UNVERIFIED, business.VerificationStatus);
    }
}
