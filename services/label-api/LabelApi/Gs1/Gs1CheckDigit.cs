namespace TrackNTrash.LabelApi.Gs1;

/// <summary>
/// GS1 mod-10 (Standard Weighting) check-digit calculation.
/// Used for GTIN-14 (13 data digits) and SSCC-18 (17 data digits).
/// Algorithm (GS1 General Specifications §7.9): starting from the right-most
/// data digit, apply weights 3,1,3,1,… sum, then check = (10 − (sum mod 10)) mod 10.
/// </summary>
public static class Gs1CheckDigit
{
    /// <summary>Computes the mod-10 check digit for a string of data digits (no check digit).</summary>
    public static int Compute(ReadOnlySpan<char> dataDigits)
    {
        if (dataDigits.IsEmpty)
            throw new ArgumentException("Data digits must not be empty.", nameof(dataDigits));

        int sum = 0;
        // Weight is 3 for the right-most data digit, then alternates 1,3,1,3...
        int weight = 3;
        for (int i = dataDigits.Length - 1; i >= 0; i--)
        {
            char c = dataDigits[i];
            if (c < '0' || c > '9')
                throw new ArgumentException($"Non-digit character '{c}' in GS1 data.", nameof(dataDigits));
            sum += (c - '0') * weight;
            weight = weight == 3 ? 1 : 3;
        }
        return (10 - (sum % 10)) % 10;
    }

    /// <summary>Appends the computed check digit to the supplied data digits.</summary>
    public static string AppendCheckDigit(string dataDigits)
        => dataDigits + Compute(dataDigits).ToString();

    /// <summary>Validates a full GS1 key (data digits + trailing check digit).</summary>
    public static bool IsValid(string keyWithCheckDigit)
    {
        if (string.IsNullOrEmpty(keyWithCheckDigit) || keyWithCheckDigit.Length < 2)
            return false;
        var data = keyWithCheckDigit.AsSpan(0, keyWithCheckDigit.Length - 1);
        int expected = Compute(data);
        return keyWithCheckDigit[^1] - '0' == expected;
    }

    /// <summary>Builds a valid GTIN-14 from 13 data digits (throws if not 13 digits).</summary>
    public static string BuildGtin14(string thirteenDigits)
    {
        if (thirteenDigits.Length != 13)
            throw new ArgumentException("GTIN-14 requires exactly 13 data digits.", nameof(thirteenDigits));
        return AppendCheckDigit(thirteenDigits);
    }

    /// <summary>Builds a valid SSCC-18 from 17 data digits (throws if not 17 digits).</summary>
    public static string BuildSscc18(string seventeenDigits)
    {
        if (seventeenDigits.Length != 17)
            throw new ArgumentException("SSCC-18 requires exactly 17 data digits.", nameof(seventeenDigits));
        return AppendCheckDigit(seventeenDigits);
    }
}
