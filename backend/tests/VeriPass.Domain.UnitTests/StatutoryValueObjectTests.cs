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

    [Fact]
    public void Gstin_Mod36Checksum_VerifiesValidAndDetectsCorrupted()
    {
        // 1. Calculate the exact expected checksum character for a 14-char base
        string base14 = "27AAACW1234F1Z";
        Gstin.VerifyChecksum(base14 + "0", out var expectedChar);

        string validGstin = base14 + expectedChar;
        Assert.True(Gstin.VerifyChecksum(validGstin, out var calculated));
        Assert.Equal(expectedChar, calculated);

        // 2. Corrupting the last character must fail checksum
        char corruptedChar = expectedChar == 'A' ? 'B' : 'A';
        string corruptedGstin = base14 + corruptedChar;
        Assert.False(Gstin.VerifyChecksum(corruptedGstin, out _));
    }

    [Theory]
    [InlineData("01", true)]
    [InlineData("27", true)]
    [InlineData("38", true)]
    [InlineData("97", true)]
    [InlineData("00", false)]
    [InlineData("39", false)]
    [InlineData("98", false)]
    public void Gstin_StateCodeValidation_AdheresToIndianStatesAndUTs(string stateCode, bool expectedValid)
    {
        var gstin = new Gstin($"{stateCode}AAACW1234F1Z5");
        Assert.Equal(expectedValid, gstin.IsValidStateCode);
    }

    [Theory]
    [InlineData("AAACW1234F", "Wipro Limited", true)]
    [InlineData("AAACW1234F", "Infosys Limited", false)]
    [InlineData("BBBPK5678D", "Kiran Kumar", true)]
    public void PanNumber_MatchesLegalNameInitial_EvaluatesCorrectly(string pan, string legalName, bool expectedMatch)
    {
        var panObj = new PanNumber(pan);
        Assert.Equal(expectedMatch, panObj.MatchesLegalNameInitial(legalName));
    }

    [Theory]
    [InlineData("SBIN0001234", true)]
    [InlineData("HDFC0000456", true)]
    [InlineData("ICIC0001122", true)]
    [InlineData("SBIN1001234", false)] // 5th character must be '0'
    [InlineData("SBIN001234", false)]  // 10 chars instead of 11
    [InlineData("INVALID_IFSC", false)]
    public void BankIdentifier_IfscValidation_AdheresToRbiSpecification(string ifsc, bool expectedValid)
    {
        Assert.Equal(expectedValid, BankIdentifier.IsValidIfsc(ifsc));
    }

    [Theory]
    [InlineData("1234567890", true)]
    [InlineData("123456789012345678", true)]
    [InlineData("123", false)] // Too short (< 9)
    [InlineData("12345678901234567890", false)] // Too long (> 18)
    public void BankIdentifier_AccountNumberValidation_EnforcesStandardLength(string accountNumber, bool expectedValid)
    {
        Assert.Equal(expectedValid, BankIdentifier.IsValidAccountNumber(accountNumber));
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
