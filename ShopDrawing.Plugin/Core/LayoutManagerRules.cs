using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ShopDrawing.Plugin.Core
{
    public static class LayoutManagerRules
    {
        private static readonly char[] InvalidLayoutNameChars = ['<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`'];

        public static string BuildPageLabel(string userTitle, int pageIndex, int totalPages)
        {
            return totalPages <= 1
                ? userTitle
                : $"{userTitle} (PHẦN {pageIndex}/{totalPages})";
        }

        public static string GetViewTitlePrefix(LayoutViewKind kind)
        {
            return kind == LayoutViewKind.Plan
                ? "MẶT BẰNG - "
                : "MẶT ĐỨNG VÁCH - ";
        }

        public static string BuildViewTitle(LayoutViewKind kind, string userTitle, int pageIndex, int totalPages)
        {
            return GetViewTitlePrefix(kind) + BuildPageLabel(userTitle, pageIndex, totalPages);
        }

        public static string SanitizeLayoutTabLabel(string text)
        {
            var source = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(source))
                return "LAYOUT";

            var builder = new StringBuilder(source.Length);
            bool lastWasSeparator = false;

            foreach (char ch in source)
            {
                bool invalid = char.IsControl(ch) || InvalidLayoutNameChars.Contains(ch);
                char output = invalid ? '-' : ch;

                if ((output == '-' || char.IsWhiteSpace(output)) && lastWasSeparator)
                    continue;

                builder.Append(output);
                lastWasSeparator = output == '-' || char.IsWhiteSpace(output);
            }

            string sanitized = builder.ToString().Trim().Trim('-', '_');
            if (string.IsNullOrWhiteSpace(sanitized))
                return "LAYOUT";

            return sanitized.Length <= 200
                ? sanitized
                : sanitized[..200].TrimEnd();
        }

        public static string FormatDate(DateTime value)
        {
            return value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        public static string NormalizeScale(string scaleText)
        {
            var normalized = (scaleText ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
                return "1:100";

            normalized = normalized.Replace(" ", string.Empty);
            int scaleIndex = normalized.LastIndexOf("1:", StringComparison.Ordinal);
            return scaleIndex >= 0
                ? normalized[scaleIndex..]
                : normalized;
        }

        public static string BuildScaleLabel(string scaleText)
        {
            return $"TỶ LỆ {NormalizeScale(scaleText)}";
        }

        public static string NextRevision(string currentRevision)
        {
            var normalized = (currentRevision ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
                return "A";

            if (normalized.Any(ch => ch < 'A' || ch > 'Z'))
                return "A";

            var chars = normalized.ToCharArray();
            for (int i = chars.Length - 1; i >= 0; i--)
            {
                if (chars[i] == 'Z')
                {
                    chars[i] = 'A';
                    continue;
                }

                chars[i]++;
                return new string(chars);
            }

            return new string('A', normalized.Length + 1);
        }

        public static string MergeWallNames(string existingName, string newName)
        {
            if (string.IsNullOrWhiteSpace(existingName)) return newName;
            if (string.IsNullOrWhiteSpace(newName)) return existingName;

            // Extract the base part (remove "MẶT ĐỨNG VÁCH - " and remove "(PHẦN ...)")
            string ExtractBaseCodes(string fullTitle)
            {
                string s = fullTitle;
                int suffixIdx = s.IndexOf("(PHẦN", StringComparison.OrdinalIgnoreCase);
                if (suffixIdx > 0)
                {
                    s = s[..suffixIdx].Trim();
                }

                const string prefix = "MẶT ĐỨNG VÁCH - ";
                if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    s = s[prefix.Length..].Trim();
                }
                else if (s.StartsWith("SD-", StringComparison.OrdinalIgnoreCase))
                {
                    s = s["SD-".Length..].Trim();
                }

                return s;
            }

            string existingBase = ExtractBaseCodes(existingName);
            string newBase = ExtractBaseCodes(newName);

            // Split by comma/hyphen and merge the wall codes while preserving first-seen order.
            var parts = existingBase.Split(new[] { ',', '-' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(p => p.Trim())
                 .Concat(newBase.Split(new[] { ',', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()))
                 .Where(p => !string.IsNullOrWhiteSpace(p))
                 .Where(p => !p.All(char.IsDigit))
                 .Distinct()
                 .ToList();

            string mergedCodes = string.Join(", ", parts);
            return $"MẶT ĐỨNG VÁCH - {mergedCodes}";
        }

        public static string GetMergedLayoutTabName(string mergedTitleText)
        {
            string s = mergedTitleText;

            // Strip known prefixes
            const string longPrefix = "M\u1eb6T \u0110\u1ee8NG V\u00c1CH - ";
            if (s.StartsWith(longPrefix, StringComparison.OrdinalIgnoreCase))
                s = s[longPrefix.Length..].Trim();
            else if (s.StartsWith("SD-", StringComparison.OrdinalIgnoreCase))
                s = s["SD-".Length..].Trim();

            string codes = s.Replace(", ", "-").Replace(",", "-");
            return SanitizeLayoutTabLabel($"SD-{codes}");
        }

        public static string InferDrawingTitleFromLayoutTabName(string layoutName)
        {
            string name = (layoutName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            const string titlePrefix = "MẶT ĐỨNG VÁCH - ";
            if (name.StartsWith(titlePrefix, StringComparison.OrdinalIgnoreCase))
                return name;

            if (!name.StartsWith("SD-", StringComparison.OrdinalIgnoreCase))
                return name;

            var parts = name["SD-".Length..]
                .Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();

            if (parts.Count > 0 && parts[0].All(char.IsDigit))
                parts.RemoveAt(0);

            return parts.Count == 0
                ? name
                : $"{titlePrefix}{string.Join(", ", parts)}";
        }
    }
}
