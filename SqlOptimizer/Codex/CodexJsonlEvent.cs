using System.Text.Json;

namespace Project_auto_push_to_gitlab.SqlOptimizer.Codex;

public sealed record CodexTokenUsage(
    long? InputTokens,
    long? CachedInputTokens,
    long? OutputTokens,
    long? ReasoningOutputTokens);

/// <summary>
/// A deliberately reduced view of one Codex JSONL event.
/// Raw JSON and tool arguments are not retained so bearer tokens and bind values
/// cannot accidentally be persisted by UI code.
/// </summary>
public sealed record CodexJsonlEvent(
    string Type,
    string? ThreadId = null,
    string? ItemId = null,
    string? ItemType = null,
    string? Status = null,
    string? Message = null,
    CodexTokenUsage? Usage = null);

public static class CodexJsonlEventParser
{
    public static bool TryParse(
        string line,
        out CodexJsonlEvent? parsedEvent,
        out string? error)
    {
        parsedEvent = null;
        error = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "空のJSONL行です。";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;

            if (!TryGetString(root, "type", out string? type) ||
                string.IsNullOrWhiteSpace(type))
            {
                error = "Codex JSONLイベントにtypeがありません。";
                return false;
            }

            TryGetString(root, "thread_id", out string? threadId);
            TryGetString(root, "status", out string? status);
            string? message = GetErrorMessage(root);
            string? itemId = null;
            string? itemType = null;

            if (root.TryGetProperty("item", out JsonElement item) &&
                item.ValueKind == JsonValueKind.Object)
            {
                TryGetString(item, "id", out itemId);
                TryGetString(item, "type", out itemType);
                if (TryGetString(item, "status", out string? itemStatus))
                {
                    status = itemStatus;
                }

                // Agent messages and reasoning summaries are safe progress text.
                // MCP tool arguments and command strings are intentionally ignored.
                if (string.Equals(itemType, "agent_message", StringComparison.Ordinal) ||
                    string.Equals(itemType, "reasoning", StringComparison.Ordinal))
                {
                    TryGetString(item, "text", out message);
                }
            }

            CodexTokenUsage? usage = ParseUsage(root);
            parsedEvent = new CodexJsonlEvent(
                type,
                threadId,
                itemId,
                itemType,
                status,
                message,
                usage);
            return true;
        }
        catch (JsonException exception)
        {
            error = $"Codex JSONLの解析に失敗しました: {exception.Message}";
            return false;
        }
    }

    private static string? GetErrorMessage(JsonElement root)
    {
        if (TryGetString(root, "message", out string? message))
        {
            return message;
        }

        if (root.TryGetProperty("error", out JsonElement errorElement))
        {
            if (errorElement.ValueKind == JsonValueKind.String)
            {
                return errorElement.GetString();
            }

            if (errorElement.ValueKind == JsonValueKind.Object &&
                TryGetString(errorElement, "message", out message))
            {
                return message;
            }
        }

        return null;
    }

    private static CodexTokenUsage? ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out JsonElement usage) ||
            usage.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new CodexTokenUsage(
            GetInt64(usage, "input_tokens"),
            GetInt64(usage, "cached_input_tokens"),
            GetInt64(usage, "output_tokens"),
            GetInt64(usage, "reasoning_output_tokens"));
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        out string? value)
    {
        value = null;
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static long? GetInt64(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) &&
               property.TryGetInt64(out long value)
            ? value
            : null;
    }
}
