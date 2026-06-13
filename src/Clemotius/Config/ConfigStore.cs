using System.ComponentModel;
using System.IO;
using Clemotius.Core.Config;
using Clemotius.Core.Config.Json;

namespace Clemotius.Config;

/// <summary>
/// %APPDATA%\Clemotius\config.json の読み書きと監視。
/// 初回は既定設定を書き出す。手動編集は FileSystemWatcher で検知して即リロードする。
/// 破損時は壊れたファイルを .bak に退避し、既定設定で起動する（上書きしない）。
/// </summary>
internal sealed class ConfigStore : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Windows.Forms.Timer _debounce;

    /// <summary>現在の設定。リロードで差し替わる。</summary>
    public ClemotiusConfig Current { get; private set; }

    /// <summary>設定が変わったとき（リロード/保存）UI スレッドで発火。</summary>
    public event Action<ClemotiusConfig>? Changed;

    /// <summary>設定ファイルが破損していたとき UI スレッドで発火（退避先パスを渡す）。</summary>
    public event Action<string>? Corrupted;

    /// <param name="marshal">
    /// FileSystemWatcher のイベントを UI スレッドに乗せるための同期オブジェクト
    /// （トレイ常駐アプリの隠しコントロール）。
    /// </param>
    public ConfigStore(ISynchronizeInvoke marshal)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _dir = Path.Combine(appData, "Clemotius");
        _path = Path.Combine(_dir, "config.json");

        Directory.CreateDirectory(_dir);
        MigrateFromLegacyClemoutis(appData);
        Current = LoadOrCreate();

        // デバウンスも含めてすべて UI スレッドで処理する
        _debounce = new System.Windows.Forms.Timer { Interval = 250 };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Reload(); };

        _watcher = new FileSystemWatcher(_dir, "config.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            SynchronizingObject = marshal, // Changed 等を UI スレッドで発火
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Renamed += OnFileChanged;
    }

    /// <summary>
    /// 旧名 "Clemoutis" 時代の設定を一度だけ引き継ぐ（Clemoutis→Clemotius リネームの移行措置）。
    /// 新フォルダに config.json がまだ無く、旧 %APPDATA%\Clemoutis\config.json が
    /// 残っている場合だけコピーする。失敗しても既定設定で起動できるので握りつぶす。
    /// </summary>
    private void MigrateFromLegacyClemoutis(string appData)
    {
        try
        {
            string legacy = Path.Combine(appData, "Clemoutis", "config.json");
            if (File.Exists(legacy) && !File.Exists(_path))
                File.Copy(legacy, _path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private ClemotiusConfig LoadOrCreate()
    {
        if (!File.Exists(_path))
        {
            var def = ClemotiusConfig.CreateDefault();
            Save(def);
            return def;
        }
        return TryLoad(out var cfg, out _) ? cfg! : ClemotiusConfig.CreateDefault();
    }

    private bool TryLoad(out ClemotiusConfig? config, out string? backupPath)
    {
        config = null;
        backupPath = null;
        try
        {
            string json = File.ReadAllText(_path);
            config = ConfigSerializer.Deserialize(json);
            return true;
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or FormatException)
        {
            backupPath = BackupCorrupted();
            return false;
        }
    }

    private string? BackupCorrupted()
    {
        try
        {
            string bak = _path + ".bak";
            File.Copy(_path, bak, overwrite: true);
            return bak;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Save(ClemotiusConfig config)
    {
        // 監視による自己リロードを避けるため一時的に無効化
        bool wasEnabled = _watcher?.EnableRaisingEvents ?? false;
        if (_watcher is not null) _watcher.EnableRaisingEvents = false;
        try
        {
            string json = ConfigSerializer.Serialize(config);
            File.WriteAllText(_path, json);
            Current = config;
        }
        finally
        {
            if (_watcher is not null) _watcher.EnableRaisingEvents = wasEnabled;
        }
        Changed?.Invoke(config);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // 連続イベントをまとめる（エディタは複数回書く）。UI スレッド上で動く。
        _debounce.Stop();
        _debounce.Start();
    }

    private void Reload()
    {
        if (TryLoad(out var cfg, out _))
        {
            Current = cfg!;
            Changed?.Invoke(cfg!);
        }
        else
        {
            var def = ClemotiusConfig.CreateDefault();
            Current = def;
            // TryLoad が破損時に退避済み。退避先パスを取り直す
            string bak = _path + ".bak";
            if (File.Exists(bak))
                Corrupted?.Invoke(bak);
            Changed?.Invoke(def);
        }
    }

    public void Dispose()
    {
        _debounce.Dispose();
        _watcher.Dispose();
    }
}
