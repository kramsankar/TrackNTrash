using TrackNTrash.LabelApi.Gs1;
using Xunit;

namespace TrackNTrash.LabelApi.Tests;

public class CheckDigitTests
{
    // ---------- GTIN-14 ----------

    [Theory]
    // 13 data digits -> expected check digit
    [InlineData("0001234567890", 5)]   // GTIN-14 00012345678905
    [InlineData("0959123456789", 2)]   // computed vector -> 09591234567892
    [InlineData("0000000000000", 0)]
    public void Gtin14_check_digit_is_correct(string data13, int expected)
    {
        Assert.Equal(expected, Gs1CheckDigit.Compute(data13));
        Assert.True(Gs1CheckDigit.IsValid(data13 + expected));
    }

    [Fact]
    public void Gtin14_full_key_validates()
    {
        string gtin = Gs1CheckDigit.BuildGtin14("0001234567890");
        Assert.Equal("00012345678905", gtin);
        Assert.True(Gs1CheckDigit.IsValid(gtin));
    }

    [Fact]
    public void Gtin14_detects_bad_check_digit()
    {
        Assert.False(Gs1CheckDigit.IsValid("00012345678904"));
    }

    [Fact]
    public void BuildGtin14_rejects_wrong_length()
    {
        Assert.Throws<ArgumentException>(() => Gs1CheckDigit.BuildGtin14("123"));
    }

    // ---------- SSCC-18 ----------

    [Theory]
    // 17 data digits -> expected check digit
    [InlineData("30614141123456789", 1)]   // known GS1-style vector -> 306141411234567891
    [InlineData("00000000000000000", 0)]
    public void Sscc18_check_digit_is_correct(string data17, int expected)
    {
        Assert.Equal(expected, Gs1CheckDigit.Compute(data17));
        Assert.True(Gs1CheckDigit.IsValid(data17 + expected));
    }

    [Fact]
    public void Sscc18_full_key_validates()
    {
        string sscc = Gs1CheckDigit.BuildSscc18("30614141123456789");
        Assert.Equal("306141411234567891", sscc);
        Assert.True(Gs1CheckDigit.IsValid(sscc));
        Assert.Equal(18, sscc.Length);
    }

    [Fact]
    public void Sscc18_detects_bad_check_digit()
    {
        Assert.False(Gs1CheckDigit.IsValid("306141411234567890"));
    }

    [Fact]
    public void Compute_rejects_non_digits()
    {
        Assert.Throws<ArgumentException>(() => Gs1CheckDigit.Compute("12A45"));
    }

    // ---------- round-trip property ----------

    [Theory]
    [InlineData("1234567890123")]
    [InlineData("9501101530003")]
    public void Gtin_roundtrip_is_self_consistent(string data13)
    {
        string full = Gs1CheckDigit.AppendCheckDigit(data13);
        Assert.True(Gs1CheckDigit.IsValid(full));
    }
}
