# 設定画面WPF移行 実装計画

作成日: 2026-06-13
対象設計書: [2026-06-13-settings-ui-modernization-design.md](../specs/2026-06-13-settings-ui-modernization-design.md)
ブランチ: `feature/settings-ui-wpf`
戦略: systematic（フェーズごとに動作確認可能な状態を維持。旧WinForms画面はフェーズ4まで温存しロールバック可能に保つ）

## 全体方針

常駐コア（フック・トレイ・ウォッチドッグ・オーバーレイ）には一切手を入れない。新コードは `src/Clemoutis/SettingsUi/` に隔離し、`ConfigStore` との契約は現行の `Applied: Action<ClemoutisConfig>` を踏襲する。

```
フェーズ0 ── 基盤更新（net10 + NuGet + テーマ初期化）
   └─▶ フェーズ1 ── SettingsWindow骨格＋保存パイプライン
          └─▶ フェーズ2 ── 単純4ページ（拡張スクロール/ホイール/ウィンドウ/一般）
                 └─▶ フェーズ3 ── ジェスチャーページ＋フライアウト＋編集ダイアログ群
                        └─▶ フェーズ4 ── トレイ切替・パリティ確認・旧画面削除
```

---

## フェーズ0: 基盤更新

**目的**: net10.0-windows への更新と WPF-UI テーマ基盤を、既存機能を壊さずに入れる。

タスク:
- [ ] 0.1 全プロジェクトの TFM を `net8.0(-windows)` → `net10.0(-windows)` に更新、`dotnet build` / `dotnet test` 確認
- [ ] 0.2 `Clemoutis.csproj` に NuGet 追加: `WPF-UI`、`CommunityToolkit.Mvvm`
- [ ] 0.3 `Program.cs` の既存 `System.Windows.Application` 生成箇所で WPF-UI のテーマ辞書（`ThemesDictionary` + `ControlsDictionary`）を `Application.Resources` にマージ
- [ ] 0.4 既存動作の回帰確認: ジェスチャー（軌跡/コマンド表示の両モード）・拡張スクロール・タイトルバーアクション・トレイ・設定画面(旧)

受け入れ基準: 全テスト緑。既存機能がすべて従来通り動く（特にWPF軌跡オーバーレイの表示・透過が変わらないこと）。

リスク: net10更新による WinForms/WPF の細かな挙動差。→ フェーズ0を独立コミットにし、問題があれば切り戻し可能にする。

---

## フェーズ1: SettingsWindow骨格＋保存パイプライン

**目的**: 左ナビ5ページの空ウィンドウと、即時適用の配管を作る。

タスク:
- [ ] 1.1 `SettingsUi/SettingsWindow`: `FluentWindow` + `NavigationView`（ジェスチャー/拡張スクロール/ホイール/ウィンドウ/一般）。Mica背景・ダークモードOS追従・リサイズ可能
- [ ] 1.2 `SettingsUi/SettingsViewModel`: `ClemoutisConfig` の編集用コピー保持、`ClemoutisConfig` 再構築ロジック、`Applied` イベント
- [ ] 1.3 300msデバウンス保存（`DispatcherTimer`）。**再構築ロジックはユニットテスト対象**
- [ ] 1.4 デバッグ用の暫定起動口（トレイメニューに「設定(WPF preview)」を一時追加 — フェーズ4で正式切替）

受け入れ基準: 新ウィンドウが起動し5ページを切替可能（中身は空）。ダーク/ライト切替が追従する。旧設定画面も従来通り動く。

---

## フェーズ2: 単純4ページ

**目的**: コンボボックス/数値中心の4ページを移植し、即時適用の実挙動を確立する。

タスク（1ページ=1コミット目安、各ページで設定変更→即時反映→config.json確認→再起動復元を確認）:
- [ ] 2.1 `ScrollPage`: 修飾キー6種のコンボ（`ScrollBehaviorChoice` 流用）
- [ ] 2.2 `WheelPage`: 垂直/水平スクロールバー上の動作コンボ
- [ ] 2.3 `WindowPage`: タイトルバーアクション6種＋不透明度（`NumberBox`）
- [ ] 2.4 `GeneralPage`: トレイ設定、ジェスチャー詳細（判定距離/タイムアウト/長押し/軌跡描画/幅/色）。色選択はWinForms `ColorDialog` 続投

受け入れ基準: 4ページすべてで「変更→数百ms内に config.json 更新→動作に反映→アプリ再起動後も値が復元」が成立。数値の範囲制約が現行と同一。

リスク: 即時適用とFileSystemWatcherの自己反映ループ。→ ConfigStore の既存の自己保存抑止挙動を確認し、必要なら保存元識別を入れる。

---

## フェーズ3: ジェスチャーページ＋編集UI

**目的**: 最重量のジェスチャーページと編集ダイアログ群のWPF化。プロファイルUX改善の本丸。

タスク:
- [ ] 3.1 `GesturePage`: プロファイル選択ComboBox＋追加/編集/削除ボタン、ジェスチャー一覧（ストローク矢印表示＋アクション名）、右ボタン+ホイール上下の割当表示/変更
- [ ] 3.2 `ProfileEditFlyout`: 名前/対象プロセス/有効化。保存/キャンセルで明示確定。バリデーション（名前空→保存無効、`*` 単体入力不可）。グローバルは編集制限（現行踏襲）。**バリデーションはユニットテスト対象**
- [ ] 3.3 `GestureEditDialog`(WPF版): アクション種別（キー送信/プリセット/AppCommand）＋ラベル表示。`ContentDialog` ベース
- [ ] 3.4 `KeyCaptureBox`(WPF版): 実キー入力キャプチャ（Winキー対応含む現行仕様の移植）
- [ ] 3.5 `StrokeCaptureDialog`(WPF版): マウス操作でストローク入力（左右ボタン対応の現行仕様の移植）

受け入れ基準: 旧画面で出来たジェスチャー編集操作がすべて新画面で可能。プロファイル追加→対象プロセス設定→ジェスチャー追加→対象アプリで発火、の一連が成立。

---

## フェーズ4: 切替・パリティ確認・旧画面削除

タスク:
- [ ] 4.1 パリティ確認チェックリスト実施: 5ページ × {読込/変更/保存/再起動後の復元} ＋ 多重起動時の前面化、トレイダブルクリック起動
- [ ] 4.2 トレイの正式呼び出し先を `SettingsWindow` に切替（preview項目を削除）
- [ ] 4.3 旧 `Settings/` のWinForms画面（SettingsForm/GestureEditDialog/StrokeCaptureDialog/KeyCaptureBox）を削除。`MutableProfile`/`ActionDisplay`/`ScrollBehaviorChoice` 等の共用ロジックは `SettingsUi/` へ移動
- [ ] 4.4 TASK.md の「設定画面のWPF移行」項目を対応済みへ移動

受け入れ基準: 旧画面への参照が残っていない。全テスト緑。チェックリスト全項目パス。

---

## 動作確認チェックリスト（フェーズ4で使用）

| 確認項目 | ページ/機能 |
|---|---|
| 値の読込が現行configと一致 | 全5ページ |
| 変更が即時反映される（デバウンス含む） | 全5ページ |
| 再起動後に値が復元される | 全5ページ |
| プロファイル追加/編集/削除/グローバル制限 | ジェスチャー |
| ジェスチャー追加/編集/削除/発火 | ジェスチャー |
| 右ボタン+ホイール上下 | ジェスチャー |
| ダーク/ライト切替追従 | ウィンドウ全体 |
| 多重起動→既存ウィンドウ前面化 | SettingsWindow |
| 軌跡オーバーレイへの影響なし | 常駐部 |
