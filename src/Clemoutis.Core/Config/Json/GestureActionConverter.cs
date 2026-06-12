using System.Text.Json;
using System.Text.Json.Serialization;
using Clemoutis.Core.Actions;

namespace Clemoutis.Core.Config.Json;

/// <summary>
/// GestureAction の判別共用体を JSON に変換する。
/// 形式: { "type": "key", "keys": "Ctrl+W" }
///       { "type": "appcommand", "command": "BrowserBackward" }
///       { "type": "close" }
/// </summary>
public sealed class GestureActionConverter : JsonConverter<GestureAction>
{
    public override GestureAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        if (!root.TryGetProperty("type", out var typeProp))
            throw new JsonException("アクションに 'type' がありません。");

        string type = typeProp.GetString() ?? "";
        switch (type.ToLowerInvariant())
        {
            case "key":
                string keys = root.GetProperty("keys").GetString()
                    ?? throw new JsonException("key アクションに 'keys' がありません。");
                string? label = root.TryGetProperty("label", out var labelProp)
                    ? labelProp.GetString()
                    : null;
                return new KeyAction(KeyStrokeParser.Parse(keys), label);

            case "appcommand":
                string cmd = root.GetProperty("command").GetString()
                    ?? throw new JsonException("appcommand アクションに 'command' がありません。");
                if (!Enum.TryParse<AppCommand>(cmd, ignoreCase: true, out var command))
                    throw new JsonException($"未知の appcommand: '{cmd}'。");
                return new AppCommandAction(command);

            case "close":
                return new CloseAction();

            default:
                throw new JsonException($"未知のアクション type: '{type}'。");
        }
    }

    public override void Write(Utf8JsonWriter writer, GestureAction value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case KeyAction key:
                writer.WriteString("type", "key");
                writer.WriteString("keys", key.Stroke.ToString());
                if (key.Label is not null)
                    writer.WriteString("label", key.Label);
                break;
            case AppCommandAction cmd:
                writer.WriteString("type", "appcommand");
                writer.WriteString("command", cmd.Command.ToString());
                break;
            case CloseAction:
                writer.WriteString("type", "close");
                break;
            default:
                throw new JsonException($"未対応のアクション型: {value.GetType().Name}。");
        }
        writer.WriteEndObject();
    }
}
