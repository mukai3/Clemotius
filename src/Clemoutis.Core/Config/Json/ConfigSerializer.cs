using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clemoutis.Core.Config.Json;

/// <summary>
/// ClemoutisConfig の JSON 入出力。アクションの判別共用体変換を含む。
/// </summary>
public static class ConfigSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var opt = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            // "+" 等を \u00XX にエスケープさせず、人が読み書きできる JSON にする
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        opt.Converters.Add(new GestureActionConverter());
        return opt;
    }

    public static string Serialize(ClemoutisConfig config)
        => JsonSerializer.Serialize(config, Options);

    public static ClemoutisConfig Deserialize(string json)
    {
        var config = JsonSerializer.Deserialize<ClemoutisConfig>(json, Options);
        return config ?? throw new JsonException("設定の逆シリアライズ結果が null です。");
    }
}
