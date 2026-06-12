# 設定画面のWPF移行・モダン化 設計書

作成日: 2026-06-13
ステータス: ユーザー承認済み

## 目的

現行のWinForms設定画面（タブ形式・固定ピクセル配置）を WPF + WPF-UI で再構築する。

主目的（優先順）:
1. **項目整理とUX改善** — 特にプロファイル操作の分かりづらさの解消
2. **見た目の刷新** — Windows 11「設定」アプリ風のFluentデザイン、ダークモード追従

主目的ではないもの: コード構造の全面改善（MVVMは必要最小限）、技術的な学び。

## 技術選定（決定事項）

| 項目 | 決定 | 備考 |
|---|---|---|
| UIフレームワーク | WPF + [WPF-UI](https://github.com/lepoco/wpfui) (NuGet, MIT) | WinUI 3はWindows App SDKランタイムを避けたいため不採用 |
| TFM | `net8.0-windows` → `net10.0-windows` (LTS) | 全プロジェクト一括更新 |
| MVVM | CommunityToolkit.Mvvm (NuGet) | コードビハインド肥大化の防止。最小限の分離のみ |
| ホスト構成 | **WinFormsホスト維持（案A）** | 常駐コア（フック・トレイ・ウォッチドッグ）は無変更。純WPF化は将来の別タスク |

### ホスト構成の検討記録

純WPF化は技術的に可能（Dispatcherでフックは動作、`Dispatcher.BeginInvoke` でマーシャリング代替可能）だが、以下の機能差から見送り:
- WinForms `NotifyIcon` の `TaskbarCreated` 自動再登録（explorer再起動時のトレイ復元）の代替が枯れていない
- `ShowBalloonTip`（トースト通知化）の代替実装が必要
- WPFに `ColorDialog` 相当が標準でなく、WPF-UIにもカラーピッカー未収録

WPF-UIテーマは起動時の `System.Windows.Application` インスタンス（既存・オーバーレイ用）にテーマ辞書をマージして適用する。既存のWPF軌跡オーバーレイへの影響がないことを確認する。

## 画面構成（決定事項）

- `FluentWindow` + `NavigationView`（左サイドナビ）。Mica背景、ダークモードOS追従
- 固定ピクセル配置を全廃し Grid＋自動サイズ。ウィンドウはリサイズ可能
- **OK/キャンセル/適用ボタンは廃止し即時適用**（Windows 11設定アプリ流）

### ナビゲーション（5ページ）

| ページ | 内容 |
|---|---|
| ジェスチャー | プロファイル選択＋ジェスチャー一覧＋右ボタン+ホイール |
| 拡張スクロール | 修飾キー6種の動作 |
| ホイール | スクロールバー上での動作 |
| ウィンドウ | タイトルバーアクション6種＋不透明度 |
| 一般 | トレイ設定、ジェスチャー詳細（判定距離・タイムアウト・軌跡描画） |

### プロファイル操作のUX改善（決定事項）

- ジェスチャーページ上部に「プロファイル選択（ComboBox）＋追加・編集・削除」を集約
- 名前・対象プロセス・有効/無効の編集は**専用フライアウトに分離**（✎ボタンで開く、保存/キャンセルで明示的に確定）。
  現行の「常時表示＋フォーカス喪失で暗黙保存」をやめる
- グローバルプロファイルは従来通り: 先頭固定・削除不可・対象プロセス編集不可

## コンポーネント構成

新規コードは `src/Clemoutis/SettingsUi/` 配下（既存 `Settings/` とは別フォルダ、プロジェクト分割なし）。

```
SettingsWindow (FluentWindow + NavigationView)
 ├─ GesturePage      + GesturePageViewModel
 ├─ ScrollPage       + ScrollPageViewModel
 ├─ WheelPage        + WheelPageViewModel
 ├─ WindowPage       + WindowPageViewModel
 ├─ GeneralPage      + GeneralPageViewModel
 ├─ ProfileEditFlyout（名前・対象プロセス・有効化）
 ├─ GestureEditDialog（WPF版: ストローク＋アクション編集）
 ├─ StrokeCaptureDialog（WPF版: マウスでストローク入力）
 └─ KeyCaptureBox（WPF版: 実キー入力でキャプチャ）
```

- 編集ダイアログ類もWPFで作り直す（新画面から旧WinFormsダイアログを出すと統一感が崩れるため）
- 色選択はWinFormsの `ColorDialog` を継続使用（ホスト構成Aのため問題なし）

## データフロー（即時適用）

1. 各ViewModelは `ClemoutisConfig` の編集用コピー（既存 `MutableProfile` を流用）を保持
2. プロパティ変更 → ルート `SettingsViewModel` が `ClemoutisConfig` を再構築 → 現行と同じ `Applied` イベント → `ConfigStore.Save()`
3. 保存は **300msデバウンス**（数値連打・スライダー操作でのファイル書き込み抑制）。フライアウト・ダイアログ系は保存ボタン押下時に確定
4. `ConfigStore.Changed` → エンジン反映の流れは現行のまま無変更（トレイ・ConfigStore側の契約変更なし）

## バリデーション・エラー処理

- プロファイル名: 空なら保存ボタン無効化＋エラーメッセージ
- 対象プロセス: `*` 単体は入力不可（グローバル専用）。空は未割当プロファイルとして許容（現行踏襲）
- 数値項目: `NumberBox` で現行のMin/Max範囲を踏襲
- 設定ファイル破損時の処理はConfigStore側で対応済み（変更なし）

## テスト

- Config再構築・バリデーションのロジックはViewModelに置き、可能な範囲でxUnitテストを追加
- UIは手動確認。チェックリスト: 5ページ × 読込/変更/保存/再起動後の復元

## 移行手順（コミット単位の目安）

1. TFM更新（net10.0-windows）＋ NuGet追加 — 全テスト・既存動作の確認
2. テーマ初期化＋空のSettingsWindow＋ナビ骨格 — 旧WinForms設定画面と並行起動可能な状態
3. ページを1枚ずつ実装（拡張スクロール → ホイール → ウィンドウ → 一般 → ジェスチャー。単純なページから着手し、最後に最重量のジェスチャー＋ダイアログ群）
4. トレイの呼び出し先を新 `SettingsWindow` に切替
5. 機能パリティ確認後、旧 `Settings/` のWinForms画面を削除

**ロールバック安全策**: 手順4まで旧画面はコードに残るため、問題発生時はトレイの呼び出し先を戻すだけで復旧可能。

## スコープ外（このプロジェクトではやらない）

- 常駐部の純WPF化（トレイ・メッセージループのWPF移行）— 将来の別タスク候補
- 設定項目の新規追加・機能追加（画面の再構築に集中する）
- WPF用カラーピッカーの導入
