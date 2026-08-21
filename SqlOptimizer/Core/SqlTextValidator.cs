using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Core
{
    public interface ISqlTextValidator
    {
        SqlValidationResult Validate(string sql);

        void EnsureBindingsMatch(
            SqlValidationResult validation,
            IReadOnlyList<SqlBindParameter> bindParameters);
    }

    public sealed class SqlValidationResult
    {
        internal SqlValidationResult(string executableSql, IReadOnlyList<string> bindNames)
        {
            ExecutableSql = executableSql;
            BindNames = new ReadOnlyCollection<string>(bindNames.ToArray());
        }

        public string ExecutableSql { get; }

        public IReadOnlyList<string> BindNames { get; }
    }

    /// <summary>
    /// Performs a conservative lexical check. It deliberately accepts only one
    /// SELECT (or WITH ... SELECT) statement and does not try to be a full Oracle parser.
    /// </summary>
    public sealed class OracleSelectStatementValidator : ISqlTextValidator
    {
        private static readonly HashSet<string> ForbiddenKeywords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "INSERT",
                "UPDATE",
                "DELETE",
                "MERGE",
                "UPSERT",
                "CREATE",
                "ALTER",
                "DROP",
                "TRUNCATE",
                "GRANT",
                "REVOKE",
                "COMMIT",
                "ROLLBACK",
                "SAVEPOINT",
                "EXEC",
                "EXECUTE",
                "BEGIN",
                "DECLARE",
                "CALL",
                "ANALYZE",
                "AUDIT",
                "NOAUDIT",
                "FLASHBACK",
                "LOCK",
                "SET",
                "COMMENT",
                "RENAME",
                "FUNCTION",
                "PROCEDURE",
                "PACKAGE",
                "TRIGGER"
            };

        public SqlValidationResult Validate(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
            {
                throw new SqlValidationException("SQL is required.");
            }

            IReadOnlyList<SqlToken> tokens = Tokenize(sql);
            if (tokens.Count == 0)
            {
                throw new SqlValidationException("SQL does not contain a statement.");
            }

            SqlToken[] semicolons = tokens
                .Where(token => token.Kind == SqlTokenKind.Semicolon)
                .ToArray();

            if (semicolons.Length > 1 ||
                (semicolons.Length == 1 && !ReferenceEquals(semicolons[0], tokens[tokens.Count - 1])))
            {
                throw new SqlValidationException("Multiple SQL statements are not allowed.");
            }

            int statementTokenCount = semicolons.Length == 0 ? tokens.Count : tokens.Count - 1;
            if (statementTokenCount == 0)
            {
                throw new SqlValidationException("SQL does not contain a statement.");
            }

            SqlToken first = tokens[0];
            bool startsWithSelect = first.IsKeyword("SELECT");
            bool startsWithWith = first.IsKeyword("WITH");
            if (!startsWithSelect && !startsWithWith)
            {
                throw new SqlValidationException(
                    "Only a SELECT statement or a WITH ... SELECT statement is allowed.");
            }

            int depth = 0;
            bool hasTopLevelSelect = startsWithSelect;
            SqlToken? previousWord = null;

            for (int index = 0; index < statementTokenCount; index++)
            {
                SqlToken token = tokens[index];
                if (token.Kind == SqlTokenKind.OpenParenthesis)
                {
                    depth++;
                    previousWord = null;
                    continue;
                }

                if (token.Kind == SqlTokenKind.CloseParenthesis)
                {
                    depth--;
                    if (depth < 0)
                    {
                        throw new SqlValidationException("SQL contains an unmatched closing parenthesis.");
                    }

                    previousWord = null;
                    continue;
                }

                if (token.Kind != SqlTokenKind.Word)
                {
                    continue;
                }

                if (ForbiddenKeywords.Contains(token.Text))
                {
                    if (previousWord?.IsKeyword("FOR") == true && token.IsKeyword("UPDATE"))
                    {
                        throw new SqlValidationException("SELECT ... FOR UPDATE is not allowed.");
                    }

                    throw new SqlValidationException(
                        $"The keyword '{token.Text.ToUpperInvariant()}' is not allowed.");
                }

                if (depth == 0 && token.IsKeyword("SELECT"))
                {
                    hasTopLevelSelect = true;
                }

                previousWord = token;
            }

            if (depth != 0)
            {
                throw new SqlValidationException("SQL contains unmatched parentheses.");
            }

            if (startsWithWith && !hasTopLevelSelect)
            {
                throw new SqlValidationException("A WITH clause must end in a top-level SELECT.");
            }

            string executableSql = semicolons.Length == 0
                ? sql.Trim()
                : sql.Substring(0, semicolons[0].Position).Trim();

            IReadOnlyList<string> bindNames = tokens
                .Take(statementTokenCount)
                .Where(token => token.Kind == SqlTokenKind.Bind)
                .Select(token => SqlBindName.Normalize(token.Text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new SqlValidationResult(executableSql, bindNames);
        }

        public void EnsureBindingsMatch(
            SqlValidationResult validation,
            IReadOnlyList<SqlBindParameter> bindParameters)
        {
            if (validation is null)
            {
                throw new ArgumentNullException(nameof(validation));
            }

            if (bindParameters is null)
            {
                throw new ArgumentNullException(nameof(bindParameters));
            }

            string[] actualNames = bindParameters
                .Select(parameter => SqlBindName.Normalize(parameter.Name))
                .ToArray();

            string? duplicate = actualNames
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;

            if (duplicate is not null)
            {
                throw new SqlValidationException($"Bind ':{duplicate}' is defined more than once.");
            }

            string[] missing = validation.BindNames
                .Except(actualNames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string[] unexpected = actualNames
                .Except(validation.BindNames, StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (missing.Length == 0 && unexpected.Length == 0)
            {
                return;
            }

            string missingText = missing.Length == 0
                ? "none"
                : string.Join(", ", missing.Select(name => ":" + name));
            string unexpectedText = unexpected.Length == 0
                ? "none"
                : string.Join(", ", unexpected.Select(name => ":" + name));

            throw new SqlValidationException(
                $"Bind definitions do not match SQL. Missing: {missingText}; unexpected: {unexpectedText}.");
        }

        private static IReadOnlyList<SqlToken> Tokenize(string sql)
        {
            List<SqlToken> tokens = new List<SqlToken>();
            int position = 0;

            while (position < sql.Length)
            {
                char current = sql[position];
                if (char.IsWhiteSpace(current))
                {
                    position++;
                    continue;
                }

                if (current == '-' && Peek(sql, position + 1) == '-')
                {
                    position += 2;
                    while (position < sql.Length &&
                           sql[position] != '\r' &&
                           sql[position] != '\n')
                    {
                        position++;
                    }

                    continue;
                }

                if (current == '/' && Peek(sql, position + 1) == '*')
                {
                    int commentStart = position;
                    position += 2;
                    bool terminated = false;
                    while (position + 1 < sql.Length)
                    {
                        if (sql[position] == '*' && sql[position + 1] == '/')
                        {
                            position += 2;
                            terminated = true;
                            break;
                        }

                        position++;
                    }

                    if (!terminated)
                    {
                        throw new SqlValidationException(
                            $"Unterminated block comment at character {commentStart}.");
                    }

                    continue;
                }

                if ((current == 'q' || current == 'Q') && Peek(sql, position + 1) == '\'')
                {
                    position = ReadAlternativeQuotedLiteral(sql, position);
                    continue;
                }

                if (current == '\'')
                {
                    position = ReadQuoted(sql, position, '\'', "string literal");
                    continue;
                }

                if (current == '"')
                {
                    int start = position;
                    position = ReadQuoted(sql, position, '"', "quoted identifier");
                    tokens.Add(new SqlToken(
                        SqlTokenKind.QuotedIdentifier,
                        sql.Substring(start, position - start),
                        start));
                    continue;
                }

                if (current == ':')
                {
                    int start = position;
                    position++;

                    if (Peek(sql, position) == '"')
                    {
                        int quotedStart = position;
                        position = ReadQuoted(sql, position, '"', "quoted bind name");
                        string quotedName = sql
                            .Substring(quotedStart + 1, position - quotedStart - 2)
                            .Replace("\"\"", "\"", StringComparison.Ordinal);
                        tokens.Add(new SqlToken(SqlTokenKind.Bind, quotedName, start));
                        continue;
                    }

                    int nameStart = position;
                    while (position < sql.Length && IsBindNameCharacter(sql[position]))
                    {
                        position++;
                    }

                    if (position > nameStart)
                    {
                        tokens.Add(new SqlToken(
                            SqlTokenKind.Bind,
                            sql.Substring(nameStart, position - nameStart),
                            start));
                    }
                    else
                    {
                        tokens.Add(new SqlToken(SqlTokenKind.Other, ":", start));
                    }

                    continue;
                }

                if (current == ';')
                {
                    tokens.Add(new SqlToken(SqlTokenKind.Semicolon, ";", position));
                    position++;
                    continue;
                }

                if (current == '(')
                {
                    tokens.Add(new SqlToken(SqlTokenKind.OpenParenthesis, "(", position));
                    position++;
                    continue;
                }

                if (current == ')')
                {
                    tokens.Add(new SqlToken(SqlTokenKind.CloseParenthesis, ")", position));
                    position++;
                    continue;
                }

                if (IsWordStart(current))
                {
                    int start = position;
                    position++;
                    while (position < sql.Length && IsWordPart(sql[position]))
                    {
                        position++;
                    }

                    tokens.Add(new SqlToken(
                        SqlTokenKind.Word,
                        sql.Substring(start, position - start),
                        start));
                    continue;
                }

                tokens.Add(new SqlToken(SqlTokenKind.Other, current.ToString(), position));
                position++;
            }

            return tokens;
        }

        private static int ReadQuoted(
            string sql,
            int start,
            char quote,
            string description)
        {
            int position = start + 1;
            while (position < sql.Length)
            {
                if (sql[position] != quote)
                {
                    position++;
                    continue;
                }

                if (Peek(sql, position + 1) == quote)
                {
                    position += 2;
                    continue;
                }

                return position + 1;
            }

            throw new SqlValidationException(
                $"Unterminated {description} at character {start}.");
        }

        private static int ReadAlternativeQuotedLiteral(string sql, int start)
        {
            if (start + 2 >= sql.Length)
            {
                throw new SqlValidationException(
                    $"Unterminated alternative quoted string at character {start}.");
            }

            char openingDelimiter = sql[start + 2];
            if (char.IsWhiteSpace(openingDelimiter) ||
                openingDelimiter == '\'' ||
                openingDelimiter == '\0')
            {
                throw new SqlValidationException(
                    $"Invalid alternative quote delimiter at character {start + 2}.");
            }

            char closingDelimiter = openingDelimiter switch
            {
                '[' => ']',
                '(' => ')',
                '{' => '}',
                '<' => '>',
                _ => openingDelimiter
            };

            int position = start + 3;
            while (position + 1 < sql.Length)
            {
                if (sql[position] == closingDelimiter && sql[position + 1] == '\'')
                {
                    return position + 2;
                }

                position++;
            }

            throw new SqlValidationException(
                $"Unterminated alternative quoted string at character {start}.");
        }

        private static bool IsWordStart(char value)
        {
            return char.IsLetter(value) || value == '_' || value == '$' || value == '#';
        }

        private static bool IsWordPart(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_' || value == '$' || value == '#';
        }

        private static bool IsBindNameCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_' || value == '$' || value == '#';
        }

        private static char Peek(string value, int position)
        {
            return position >= 0 && position < value.Length ? value[position] : '\0';
        }

        private enum SqlTokenKind
        {
            Word,
            QuotedIdentifier,
            Bind,
            Semicolon,
            OpenParenthesis,
            CloseParenthesis,
            Other
        }

        private sealed class SqlToken
        {
            public SqlToken(SqlTokenKind kind, string text, int position)
            {
                Kind = kind;
                Text = text;
                Position = position;
            }

            public SqlTokenKind Kind { get; }

            public string Text { get; }

            public int Position { get; }

            public bool IsKeyword(string keyword)
            {
                return Kind == SqlTokenKind.Word &&
                       string.Equals(Text, keyword, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    internal static class SqlApprovalFingerprint
    {
        public static string HashSql(string sql)
        {
            return HashText(sql.Trim());
        }

        public static string HashBindSchema(IReadOnlyList<SqlBindDefinition> definitions)
        {
            string canonical = string.Join(
                "\n",
                definitions
                    .OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(definition =>
                        $"{definition.Name.ToUpperInvariant()}|{definition.DataType}|{definition.Size?.ToString() ?? "-"}"));
            return HashText(canonical);
        }

        private static string HashText(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            byte[] digest = SHA256.HashData(bytes);
            return Convert.ToHexString(digest);
        }
    }
}
