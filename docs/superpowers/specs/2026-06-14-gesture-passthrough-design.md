# アプリ別ジェスチャー除外リスト 設計書

- 日付: 2026-06-14
- 対象: Clemotius（かざぐるマウス .NET リプロダクト）
- 関連タスク: #17

## 背景・課題

JaneStyle のように**アプリ自身が右ボタンマウスジェスチャー**（右ドラッグで閉じる／タブ切替など）を実装しているアプリで、Clemotius 起動中はそれらが一切効かなくなる。

### 根本原因

`GestureEngine.OnRightDown` は、適用プロファイルが有効（`GesturesEnabled=true`）なら**右ボタンDOWNを無条件で飲み込む**。低レベルフック（WH_MOUSE_LL）は DOWN 受信の瞬間に「飲む／通す」を即決する必要があり、後から遡ってアプリへ渡すことが原理的に難しい。

| 状況 | 現在の挙動 | 問題 |
|---|---|---|
| ストロークなし・ホイールなし | UP時に右クリック再生 | OK |
| ストローク一致 | アクション実行・飲み込み | OK |
| ストロークあり・不一致 | 全部飲み込み、アプリは何も受け取らない | アプリ独自ジェスチャーが消える |
| 右+ホイール（割当なし） | ホイールは通すがDOWNは飲み済み | アプリは「右押下なしのホイール」しか見えない |

MOVE はアプリへ通っているが起点の DOWN がアプリに届かないため、アプリ側の右ドラッグ追従が成立しない。

### 設計上の制約

同一アプリ内で「Clemotius のジェスチャー」と「アプリ独自の右ドラッグジェスチャー」を**両立させることは LL フックの仕組み上ほぼ不可能**。DOWN を通せばアプリは動くが Clemotius がメニュー抑制・自前ジェスチャーを正しく制御できず二重発火する。DOWN を飲めばアプリのドラッグが死ぬ。

→ **右ボタンの所有権はアプリ単位でどちらか一方に決める**のが唯一堅牢なモデル。本設計はこの前提に立ち、「特定アプリで Clemotius のジェスチャーを使わず、アプリ側に右ボタンを完全に明け渡す」除外機構を導入する。

なお現状コードでも、アプリ別プロファイルで `GesturesEnabled=false` にすると右ボタンは完全ネイティブ透過される（`OnRightDown` が `!Enabled` で素通し）。除外の土台は既にあり、不足しているのは「一覧で管理できる UX」と「プロファイルを作らずに除外指定する手段」。

## 方針（確定事項）

- 主軸: **プロセス除外**（非成立入力のリアルタイム透過は原理的に不確実なため採らない）
- 設定モデル: **専用の除外リスト**（ジェスチャーページ、プロファイルとは独立。プロファイルの「対象プロセス」と同じくカンマ区切りの単一テキストで指定）
- 除外範囲: **ジェスチャーのみ**（スクロール強化・タイトルバー操作は影響を受けない）

## 設計

### 1. データモデル

`src/Clemotius.Core/Config/GestureSettings.cs` に1フィールド追加:

```csharp
/// <summary>
/// ジェスチャーを適用しないプロセス名の一覧。ここに登録したアプリでは
/// 右ボタンを完全にアプリ側へ透過し、アプリ独自のマウスジェスチャーを使えるようにする。
/// 拡張子なし可・大文字小文字無視。
/// </summary>
public IReadOnlyList<string> ExcludedProcesses { get; init; } = Array.Empty<string>();
```

- `ConfigSerializer` は System.Text.Json + camelCase のため `excludedProcesses` として自動シリアライズ。
- **後方互換**: 既存 `config.json` に項目が無い場合は既定（空配列）で読み込まれる。マイグレーション不要。

### 2. プロセス名正規化の共有

現状 `ProfileResolver.NormalizeProcess`（private）が「trim → `.exe` 除去」を行う。これを Core の共有ヘルパーへ切り出し、除外判定とプロファイル解決で同一ロジックを使う。

- 新規: `src/Clemotius.Core/Config/ProcessName.cs`（static）に `Normalize(string?)` を移設。
- `ProfileResolver` は `ProcessName.Normalize` を呼ぶよう変更（挙動不変）。
- 純粋関数のためユニットテスト可能。

### 3. 解決ロジック

`src/Clemotius/Gestures/ActiveConfigProvider.cs`:

- 除外集合を保持: `HashSet<string> _excluded`（`StringComparer.OrdinalIgnoreCase`、正規化済みプロセス名）。
- `Update(config)` 時に `_config.Gesture.ExcludedProcesses` を正規化して再構築（`_matcherCache.Clear()` と同じロック内）。
- `Resolve(startX, startY)` で**自プロセス判定の直後**、プロファイル解決より前に:

```csharp
string? process = ProcessNameResolver.FromWindow(target);
lock (_gate)
{
    if (process is not null && _excluded.Contains(ProcessName.Normalize(process)))
        return null; // 除外: 右ボタンを完全ネイティブ透過
    // …既存のプロファイル解決…
}
```

`return null` により `GestureEngine.OnRightDown` が `ctx is null` で素通しし、DOWN/MOVE/UP/右+ホイールすべてアプリへ透過する（既存挙動の再利用）。

### 4. UI（ジェスチャーページ）

`src/Clemotius/SettingsUi/Pages/GesturePage.xaml` の最下部に新セクション「ジェスチャーを無効にするアプリ」:

- プロファイルの「対象プロセス」と同じ書式の**カンマ区切り単一テキストボックス**（例: `JaneStyle, mintty`）。追加/削除ボタンや一覧 UI は設けない。
- 説明文: 「登録したアプリでは右ボタンをアプリ側へ透過し、アプリ独自のマウスジェスチャーをそのまま使えます（プロファイルに関わらず共通。カンマ区切りで複数指定可）。」

`src/Clemotius/SettingsUi/GestureViewModel.cs`（除外はジェスチャー設定 `GestureSettings` 由来のため、`GeneralViewModel` ではなくここに配置）:

- `[ObservableProperty] string ExcludedProcessesText`（読み込み時は config の配列を `", "` で連結。変更で `_changed()` を発火し即時適用に乗せる）。
- `BuildExcludedProcesses()`: カンマ分割 → `ProcessName.Normalize` → 空除去・大小文字無視で重複排除 → 配列化。
- `SettingsViewModel.Build()` で `GestureSettings.ExcludedProcesses` へ反映。

> 補足: UI は当初「全般ページに追加/削除リスト」で実装したが、(1) 全般ページが長くスクロールバーが自動非表示で発見しづらい、(2) ジェスチャー設定なのでジェスチャーページが自然、(3) プロファイルの対象プロセスと同じ書式の方が分かりやすい、という確認を経て現行案へ変更した。

### 5. テスト

`tests/Clemotius.Tests/`:

- `ProcessNameTests`: `Normalize` の正規化（trim、`.exe` 除去、null/空、大小文字）。
- 除外判定は `ProcessName.Normalize` + `HashSet` の組合せで表現できるため、必要なら判定を純粋関数に切り出して `ExcludedProcessesTests` でカバー。`ActiveConfigProvider` 自体は Win32 依存のためユニットテスト対象外。

## 範囲外（将来検討）

- 非成立ストロークのアプリへのリアルタイム透過（原理的に不確実なため不採用）。
- 「グローバル右+ホイール割当なしを全非ブラウザへ自動透過」する挙動（必要なら対象アプリを除外登録、または別タスク）。
- 実行中プロセス一覧からの選択 UI。
- 除外範囲を「Clemotius 機能すべて」へ拡張するオプション。

## 動作確認（コミット前）

- ビルド成功・全テスト合格。
- JaneStyle を除外登録 → 右ドラッグでの閉じる/タブ切替がアプリ側で動作する。
- 除外していないアプリでは従来どおり Clemotius のジェスチャーが動作する。
- 既存 config.json（`excludedProcesses` なし）が問題なく読み込める。
