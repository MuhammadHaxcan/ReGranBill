using System.Text;

namespace ReGranBill.Server.Helpers;

public static class PdfFileNameHelper
{
    public static string BuildVoucherFileName(string? partyName, string voucherNumber, DateOnly date)
    {
        var partySegment = SanitizeSegment(partyName, "voucher");
        var voucherSegment = SanitizeSegment(voucherNumber, "document");
        var dateSegment = date.ToString("yyyy-MM-dd");
        return $"{partySegment}_{voucherSegment}_{dateSegment}.pdf";
    }

    private static string SanitizeSegment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var builder = new StringBuilder(value.Length);
        var lastWasSeparator = false;

        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasSeparator = false;
                continue;
            }

            if (ch is '-' or '_')
            {
                builder.Append(ch);
                lastWasSeparator = false;
                continue;
            }

            if (!lastWasSeparator)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }

        var sanitized = builder.ToString().Trim('_', '-', '.');
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}
