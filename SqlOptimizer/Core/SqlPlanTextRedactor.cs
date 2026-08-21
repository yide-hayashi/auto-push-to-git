namespace Project_auto_push_to_gitlab.SqlOptimizer.Core
{
    /// <summary>
    /// Removes values that Oracle can embed in DBMS_XPLAN text. Plan text is
    /// persisted and exposed to MCP, so runtime bind values must be removed at
    /// the domain boundary even when an evaluator forgets to do so.
    /// </summary>
    public static class SqlPlanTextRedactor
    {
        private const string PeekedBindsHeader = "Peeked Binds";
        private const string RedactedMessage =
            "[REDACTED: peeked bind values are intentionally omitted]";

        public static string RedactPeekedBindValues(string? planText)
        {
            if (string.IsNullOrEmpty(planText) ||
                planText.IndexOf(PeekedBindsHeader, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return planText ?? string.Empty;
            }

            string newline = planText.Contains("\r\n", StringComparison.Ordinal)
                ? "\r\n"
                : "\n";
            string[] lines = planText.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
            List<string> output = new(lines.Length);
            bool redacting = false;

            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (!redacting)
                {
                    if (line.TrimStart().StartsWith(
                            PeekedBindsHeader,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        output.Add("Peeked Binds:");
                        output.Add("-------------");
                        output.Add(RedactedMessage);
                        redacting = true;
                    }
                    else
                    {
                        output.Add(line);
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    output.Add(string.Empty);
                    redacting = false;
                    continue;
                }

                if (IsSectionHeader(lines, index))
                {
                    output.Add(line);
                    redacting = false;
                }
            }

            return string.Join(newline, output);
        }

        private static bool IsSectionHeader(IReadOnlyList<string> lines, int index)
        {
            if (index + 1 >= lines.Count || string.IsNullOrWhiteSpace(lines[index]))
            {
                return false;
            }

            string underline = lines[index + 1].Trim();
            return underline.Length >= 3 &&
                   underline.All(character => character == '-');
        }
    }
}
