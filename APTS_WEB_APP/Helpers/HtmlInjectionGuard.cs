using System;
using System.Text.RegularExpressions;
using System.Web;

namespace APTS_WEB_APP.Helpers
{
    public static class HtmlInjectionGuard
    {
        private static readonly Regex MarkupPattern = new Regex(
            @"<\s*/?\s*[a-zA-Z!/?][^>]*>|javascript\s*:|on[a-zA-Z]+\s*=",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool ContainsHtmlInjection(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string decodedValue = HttpUtility.HtmlDecode(value);
            return MarkupPattern.IsMatch(value)
                || (!string.Equals(value, decodedValue, StringComparison.Ordinal) && MarkupPattern.IsMatch(decodedValue))
                || decodedValue.IndexOf('<') >= 0
                || decodedValue.IndexOf('>') >= 0;
        }

        public static string TrimPlainText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value.Trim(), @"[\x00-\x08\x0B\x0C\x0E-\x1F]", string.Empty);
        }
    }
}