using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Clemotius.Core.Actions;
using Clemotius.Core.Gestures;

namespace Clemotius.Core.Config.Json;

/// <summary>
/// ClemotiusConfig の JSON 入出力。アクションの判別共用体変換を含む。
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

    public static string Serialize(ClemotiusConfig config)
        => JsonSerializer.Serialize(config, Options);

    public static ClemotiusConfig Deserialize(string json)
    {
        var config = JsonSerializer.Deserialize<ClemotiusConfig>(json, Options);
        if (config is null)
            throw new JsonException("設定の逆シリアライズ結果が null です。");
        // 旧モデル（プロファイルの wheelUp/wheelDown フィールド）からの移行: 一覧の wheel binding へ畳み込む。
        config = MigrateLegacyWheel(config, json);
        // 旧モデル（グローバル"*"プロファイル＋除外リスト）からの移行: "*" を取り除く。
        // 旧 excludedProcesses は未知プロパティとして自動的に無視される。
        return config.WithoutGlobalProfiles();
    }

    /// <summary>
    /// 旧形式の profiles[].wheelUp / wheelDown（独立フィールド）を、現行の Gestures 一覧の
    /// WheelUp/WheelDown トリガ binding へ移行する。既に wheel トリガの binding があるプロファイル
    /// （現行形式で保存されたもの）はそのまま。"*" 除去より前に行い、json の profile 順と揃える。
    /// </summary>
    private static ClemotiusConfig MigrateLegacyWheel(ClemotiusConfig config, string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("profiles", out var profilesEl)
            || profilesEl.ValueKind != JsonValueKind.Array)
            return config;

        var profiles = config.Profiles.ToArray();
        var profileEls = profilesEl.EnumerateArray().ToArray();
        bool changed = false;

        for (int i = 0; i < profiles.Length && i < profileEls.Length; i++)
        {
            var extra = new List<GestureBinding>();
            if (TryLegacyWheel(profileEls[i], "wheelUp", WheelStrokes.Up, profiles[i], out var up))
                extra.Add(up!);
            if (TryLegacyWheel(profileEls[i], "wheelDown", WheelStrokes.Down, profiles[i], out var down))
                extra.Add(down!);
            if (extra.Count > 0)
            {
                profiles[i] = profiles[i] with { Gestures = profiles[i].Gestures.Concat(extra).ToArray() };
                changed = true;
            }
        }

        return changed ? config with { Profiles = profiles } : config;
    }

    private static bool TryLegacyWheel(
        JsonElement profileEl, string propertyName, string wheelStrokes,
        GestureProfile profile, out GestureBinding? binding)
    {
        binding = null;
        // 現行形式で既に WU/WD binding を持つなら移行しない（二重登録を避ける）。
        if (profile.Gestures.Any(b => b.Strokes == wheelStrokes))
            return false;
        if (!profileEl.TryGetProperty(propertyName, out var actionEl)
            || actionEl.ValueKind == JsonValueKind.Null)
            return false;
        var action = actionEl.Deserialize<GestureAction>(Options);
        if (action is null)
            return false;
        binding = new GestureBinding(wheelStrokes, action);
        return true;
    }
}
