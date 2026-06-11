# Clemoutis [KazaguruMouse Alternative] 設計書

> アプリケーション名 **Clemoutis** は、かざぐるまをモチーフとするオリジナルに対し、クレマチス（植物カザグルマの原種）をもじった命名。

作成日: 2026-06-11
ステータス: ユーザー承認済み（バイナリ移植ベース・入力捕捉は案 A）

## 背景と目的

「かざぐるマウス」（Kazaguru.exe, Static Flower 作）は Windows 8 までを対象に開発が終了したマウス拡張ユーティリティで、ソースコードは公開されていない。本プロジェクトは、その中核機能を **Windows 10/11 で安定動作する自分用ツール**として C#/.NET で再実装する。

実装の根拠は、公開ドキュメント（ReadMe・ヘルプ）だけでは挙動の細部（ジェスチャー判定しきい値・スクロール変換ロジック・スクロールバー検出方式など）を特定できないため、**手持ちのオリジナルバイナリの静的・動的解析で実挙動を復元し、それを基準に再実装する「バイナリ移植ベース」方式**とする。バイナリの内容そのものを転載・再配布することはせず、相互運用目的の挙動再現にとどめる（フリーウェア・自分用）。

## リバースエンジニアリング方針

### 静的解析で確定済みの事実（2026-06-11 時点）

手持ちバイナリの PE 解析により以下を確認した。

| 項目 | 確認内容 | 設計への含意 |
|---|---|---|
| パッキング | 無し。全モジュールのインポートテーブルが正常に読める | 静的解析でのAPI追跡・逆アセンブルが現実的 |
| フック方式 | `Kazahook.dll` に `shared` PE セクション、`UnhookWindowsHookEx` をインポート | オリジナルは **DLL インジェクション型のグローバルフック**（共有メモリでプロセス間状態共有） |
| UI 要素判定 | 全モジュールが `OLEACC`（MSAA）をインポート、`UIAccessible` シンボルあり | スクロールバー等の **UI 要素識別にアクセシビリティ API を使用** |
| 32bit 対応 | `Kazawow64.exe`（32bit EXE）+ `Kazahook32.dll`（32bit DLL） | 64bit 本体が 32bit プロセスへフック注入するためのブリッジ |
| 構成 | `Kazaguru.exe`（本体/GUI）, `Kazasub.dll`（補助・設定UI?）, `Kazahook*.dll`（フック本体） | 機能がフック DLL と本体プロセスに分離 |
| **アクション実行方式** | `Kazaguru.exe`/`Kazahook.dll` が **キー合成系**（`SendInput`, `keybd_event`, `MapVirtualKeyW`）と**ウィンドウメッセージ系**（`SendMessageW`, `PostMessageW`, `SendNotifyMessageW`, `SendMessageTimeoutW`）の両方をインポート。さらに `AttachThreadInput`, `SetForegroundWindow`, `GetGUIThreadInfo`, `RealChildWindowFromPoint`, `RealGetWindowClassW` | アクションは**ハイブリッド**。キー送信だけでなくウィンドウメッセージを直接送る。`SendNotifyMessageW` の存在から、ブラウザの戻る/進む/更新は **`WM_APPCOMMAND`**（`APPCOMMAND_BROWSER_*`）、ウィンドウ「閉じる」は **`WM_CLOSE`** の可能性が高い。タブ閉じ（Ctrl+W）は Win32 メッセージが無いためキー合成で、`AttachThreadInput`+`SetForegroundWindow` で対象ウィンドウへ確実配送している、と読める |

### 設定ファイル形式（実物 `Kazaguru.ini` の解析で確定）

ユーザー提供の実設定ファイルを解析し、以下を確定した。

- **形式**: BOM 無しの **UTF-16LE テキスト INI**（バイナリではない）。先頭 `5b 00 53 00…`、null 比率 0.5。セクションは `[Settings]` / `[Gestures]` / `[MouseAssignment]`
- **`[Gestures]` のキー**: ストローク方向列の文字列。`L`/`R`/`U`/`D`（左右上下ストローク）の連結（例 `DR`, `UDU`）。`R+WU`/`R+WD` は**右ボタン押下＋ホイール上/下のホイールジェスチャー**
- **`[Gestures]` の値（アクション）は2形式**:
  1. **整数** = 組み込みコマンドID（かざぐるマウス内部のアクション列挙値）。例 `L=1`, `R=2`, `UD=3`, `LR=101`, `DL=112`
  2. **`#1,VK,Shift,Ctrl,Alt,Win`** = ユーザー定義のキー送信。先頭 `1`=種別(キー送信)、以降 VK コードと修飾キーフラグ
- **`#1,…` フィールドの確証**: `R+WU=#1,9,1,1,0,0`(Ctrl+Shift+Tab) と `R+WD=#1,9,0,1,0,0`(Ctrl+Tab) が Shift 桁のみ差異。VK 値も `87=W`/`9=Tab`/`116=F5`/`36=Home`/`35=End` と全て妥当
- **アプリ振り分けはアプリ種別フラグ**: `AllowFlagsInIE`/`AllowFlagsInMozilla`/`AllowFlagsInWindow`/`AllowLevelInExplorer`/`BrowserCommandFlags`/`SwitchTabsFlags` 等のビットフラグで、IE / Mozilla / 一般ウィンドウ / Explorer のカテゴリ単位に機能を制御（プロセス名プロファイルではない）。→ **本プロジェクトでは近代化として「プロセス名/クラス名パターンのプロファイル」を採用**（カテゴリ方式の上位互換）。挙動の厳密一致は求めず、同等機能を再現する方針
- **判定パラメータの実デフォルト値**: `GestureRange=8`, `GestureTimeout=1000`, `PushHoldTime=500`, `Sensitivity=3`, `Acceleration=3`, `MergeWheelDelta=2`, `WheelResolution=1`
- **`[MouseAssignment]`**: マウスボタンチョード割り当て（`C+LC_1=202`(Ctrl+左クリック), `RC_5=213`, `RC_3=211`）。v1 スコープ外

> 重要: この ini は**ユーザー個人設定**であり**工場出荷時デフォルトではない**。組み込みコマンドID（`L=1` 等）の正確な意味と、デフォルトのジェスチャー割り当ては、別途 VM で ini 再生成 or 逆アセンブルで要確認。

### RE の進め方（実装フェーズで段階実施）

1. **静的解析**: PE インポート/エクスポート、文字列、リソース（設定ダイアログのレイアウト・既定値）、バージョン情報を抽出。設定ファイル/レジストリのフォーマットを特定
2. **動的解析**: オリジナルを VM 上で実行し、Spy++ / API Monitor で入力→出力の対応を観測。確定すべき項目:
   - **組み込みコマンドID の実機構**: `L=1`/`R=2` 等が `WM_APPCOMMAND`（lParam 値）か内部処理か。ID→意味の対応表を作る
   - **Chromium のアプリ種別判定**: Chrome/Edge が IE / Mozilla / 一般ウィンドウ のどれに分類され、どのフラグが効くか（ウィンドウクラス名で判定している可能性）
   - **工場出荷時デフォルトのジェスチャー割り当て**: ini を消して初回起動させ再生成して採取
   - ジェスチャー判定しきい値（`GestureRange` の画素換算）、スクロール変換の係数・条件を実測
3. **挙動の文書化**: 復元した仕様を本設計書に追記し、再実装の受け入れ基準（オリジナルと同じ入力で同じ出力）とする

### 移植方針の原則：忠実再現と近代化の線引き

- **挙動（外から見える結果）は忠実に再現する**: ジェスチャー軌跡の判定、スクロール変換の条件・量、スクロールバー検出の対象範囲
- **実装手段は近代化を許容する**: 入力捕捉の機構（インジェクション vs ローカルフック）は、実挙動を再現できる範囲で Win10/11 で安全・保守的な方式を選ぶ。下記「技術方式」の確認事項を参照

## スコープ

### 含む機能（v1）

1. **マウスジェスチャー**
   - 右ボタンドラッグの軌跡（↑↓←→ ストローク列）でアクション実行
   - アクションは**ハイブリッド**（静的解析でオリジナルもハイブリッドと判明）:
     - キーストローク送信（例: `Ctrl+W`, `Alt+Left`）。対象ウィンドウへ `AttachThreadInput`+`SetForegroundWindow` で確実配送
     - `WM_APPCOMMAND` 送信（戻る/進む/更新など `APPCOMMAND_BROWSER_*`）
     - `WM_CLOSE` 送信（ウィンドウを閉じる）
   - 各アクションが実際にどの機構を使うかは動的解析でオリジナルを実測して確定する
   - アプリごとのプロファイル（プロセス名パターンで適用先を決定、マッチなしはデフォルトプロファイル）
   - プロファイル単位でジェスチャー無効化可（ゲーム・リモートデスクトップ等の除外用）
2. **スクロール拡張**
   - 水平スクロールバー上で縦ホイール → 水平スクロールに変換
   - 修飾キー押下中のホイールを変換。オリジナル v1.67 に合わせ **6通り**（Shift / Ctrl / Ctrl+Shift / Alt / Shift+Alt / Ctrl+Alt）それぞれに挙動（"none" / "horizontal" / 未確定 "code:NN"）を割り当てる
3. **常駐 UI**
   - タスクトレイアイコン（設定を開く・一時停止・終了）
   - WinForms 設定画面（プロファイル一覧、ジェスチャー⇔アクション編集、スクロール設定）

### 含まない機能（YAGNI）

- カーソル直下スクロール（Win10 以降は OS 標準機能）
- スクロール加速
- ホイールによるウィンドウ操作・音量調節
- タスクバーボタン並べ替え
- ジェスチャーアクションのうち、キー送信・`WM_APPCOMMAND`・`WM_CLOSE` 以外（アプリ起動・任意のシステム操作・最小化/最大化等のウィンドウ操作）
- 既存 `Kazaguru.ini` のインポート/移行（新アプリは独自 JSON 設定をゼロから作る。ini は挙動解読の参考資料としてのみ使用）
- マウスボタンチョード（`[MouseAssignment]`）
- インストーラー・自動更新（自分用のため不要）

## 技術方式

共通: **.NET 8 / WinForms / トレイ常駐アプリ**。キー送信は `SendInput`。

入力捕捉の機構について、オリジナルが DLL インジェクション型グローバルフックである（前述の静的解析で確定）ことを踏まえ検討した結果、**案 A（振る舞いのみ忠実再現／内部は WH_MOUSE_LL で近代化）を採用**する。動的解析でオリジナル挙動を実測し、案 A で再現しきれない差異が判明した場合のみ案 B を再検討する。

### 案 A: WH_MOUSE_LL ローカルフック（採用）

- 純 C# 単一 EXE。`WH_MOUSE_LL` / `WH_KEYBOARD_LL` を P/Invoke で設置
- インジェクション不使用 → 32/64bit 問題なし、AV 誤検知リスク低、保守容易
- **トレードオフ**: グローバルローカルフックは UI スレッドで全イベントを処理するため、オリジナルのインプロセス・フックに比べ遅延耐性が低い。スクロールバー検出（MSAA）も別プロセス越しの呼び出しになり、オリジナルと挙動が一部異なる可能性

### 案 B: DLL インジェクション（オリジナル忠実移植）

- オリジナルと同じく `WH_MOUSE`/`WH_GETMESSAGE` 系のグローバルフック DLL を各プロセスに注入し、共有メモリで本体と連携
- **トレードオフ**: フック DLL はネイティブ（C++）が必要で C#/C++ 混成。32bit プロセス用に別 DLL とブリッジ EXE も必要（オリジナルの `Kazawow64.exe` 相当）。複雑度・ビルド難度が大きく上がる。AV 誤検知の可能性

不採用: **Raw Input 方式** — 監視はできるが入力ブロック不可で、ジェスチャー中の右クリック抑制ができない。

既知の制約（両案共通）: 管理者権限ウィンドウ（UIPI 保護）にはキー送信・フックが届かない。回避には本体を管理者実行する（README に明記）。

> 注: 「バイナリ移植ベース」は**外から見える挙動の忠実再現**を指し、入力捕捉の内部機構まで案 B で一致させる必要があるかは別問題。挙動の再現だけが目的なら案 A で足りる見込み。最終決定は動的解析でオリジナル挙動を実測してから確定する。

## アーキテクチャ

```
┌─────────────────────────────────────────────┐
│ Clemoutis.exe (.NET 8, WinForms)            │
│                                             │
│  ┌──────────────┐    ┌───────────────────┐  │
│  │ MouseHook    │───▶│ InputRouter       │  │
│  │ (WH_MOUSE_LL)│    │  どの機能に渡すか判定 │  │
│  └──────────────┘    └─────┬─────────────┘  │
│  ┌──────────────┐          │                │
│  │ KeyboardHook │──────────┤                │
│  │(WH_KEYBOARD_LL)│        ▼                │
│  └──────────────┘   ┌─────────────────────┐ │
│                     │ GestureEngine       │ │
│                     │ ScrollEnhancer      │ │
│                     └─────┬───────────────┘ │
│                           ▼                 │
│                     ┌─────────────────────┐ │
│                     │ ActionExecutor      │ │
│                     │ (SendInput でキー送信) │ │
│                     └─────────────────────┘ │
│  ┌──────────────┐   ┌─────────────────────┐ │
│  │ TrayIcon     │   │ ConfigStore (JSON)  │ │
│  │ SettingsForm │◀─▶│ ProfileResolver     │ │
│  └──────────────┘   └─────────────────────┘ │
└─────────────────────────────────────────────┘
```

### コンポーネントの責務

| コンポーネント | 責務 |
|---|---|
| MouseHook / KeyboardHook | P/Invoke による低レベルフック。受信イベントを即座に InputRouter へ渡す（フック内処理は最小限）。修飾キー状態の追跡 |
| InputRouter | カーソル位置のウィンドウ情報（プロセス名・クラス名）を取得し、GestureEngine / ScrollEnhancer に振り分け。イベントを「飲み込む」か「素通し」かの最終判断 |
| GestureEngine | 右ボタン押下からの軌跡を ↑↓←→ にエンコードし、プロファイルのジェスチャー定義とマッチング。一致したら ActionExecutor に依頼。動かず離したら通常右クリックを再生 |
| ScrollEnhancer | スクロールバー上の縦ホイール→水平変換、修飾キー押下中のホイール変換 |
| ActionExecutor | アクション定義を解釈し、機構別に実行する。(1) キーストローク: 文字列（`"Ctrl+W"` 等）をパースし、対象ウィンドウへ `AttachThreadInput`+`SetForegroundWindow` で焦点を移したうえ `SendInput` 送信。(2) `WM_APPCOMMAND`: `APPCOMMAND_BROWSER_BACKWARD/FORWARD/REFRESH` 等を `SendMessageW`/`SendNotifyMessageW` で対象へ送信。(3) `WM_CLOSE`: `PostMessageW` で送信。アクション種別はピュアロジックで判定し、Win32 呼び出しは薄いラッパに隔離 |
| TargetWindowResolver | アクション対象ウィンドウを決定（ジェスチャー開始位置のトップレベル窓 / 前面窓）。`WindowFromPoint`/`GetAncestor`/`GetForegroundWindow` を使用 |
| ProfileResolver | 前面アプリのプロセス名から適用プロファイルを決定 |
| ConfigStore | `%APPDATA%\Clemoutis\config.json` の読み書き。変更の即時反映（FileSystemWatcher） |
| TrayIcon / SettingsForm | トレイメニューと WinForms 設定画面 |

### 設計上の要点

フックコールバックは Windows により応答時間が監視され、遅いとフックを外される。ジェスチャーは「右クリックをブロックするか」をその場で決める必要があるため非同期化はできない。判定は同期のまま軽量に保ち、重い処理（ウィンドウ情報取得）はキャッシュで緩和する。

ジェスチャー判定・マッチング・キーストロークパーサ・プロファイル解決は Win32 非依存のピュアロジックとして分離し、ユニットテスト可能にする。

## データフロー

### マウスジェスチャー

1. 右ボタン押下 → GestureEngine が保留状態に入り、右ダウンを一旦飲み込む。押下位置のウィンドウからプロファイルを解決
2. カーソル移動 → 移動量がしきい値（オリジナルの `GestureRange=8` 相当。画素換算は動的解析で確定）を超えたら方向をストロークとして記録。確定モードに入り、現在のストロークを画面表示（例 `↓→`）
3. 右ボタン解放:
   - ストロークあり＆一致 → ActionExecutor がアクション種別（キー送信 / `WM_APPCOMMAND` / `WM_CLOSE`）に応じて実行（右クリックは発生させない）。対象ウィンドウは TargetWindowResolver が決定
   - ストロークあり＆一致なし → 何もしない（誤爆防止）
   - ストロークなし → 飲み込んだ右ダウン＋アップを SendInput で再生し、通常の右クリックを成立させる
4. ジェスチャー無効プロファイルのアプリでは 1 の時点で素通し

### スクロール拡張

ホイール受信 → カーソル下の UI 要素を判定 →

- 修飾キー押下中（Shift / Ctrl / Ctrl+Shift / Alt / Shift+Alt / Ctrl+Alt のいずれか完全一致）→ 対応スロットの挙動に従い変換
- 修飾キーなしで水平スクロールバー上 → `WM_MOUSEWHEEL` を飲み込み水平スクロールへ変換
- どちらでもない → 素通し

## 設定ファイル

`%APPDATA%\Clemoutis\config.json`。構造の骨子:

```json
{
  "gesture": {
    "range": 8,
    "timeoutMs": 1000,
    "pushHoldTimeMs": 500,
    "rapidFire": false,
    "drawStroke": false,
    "drawingType": 0,
    "strokeWidth": 2,
    "validStrokeColor": "#80FF00",
    "invalidStrokeColor": "#FFFF00"
  },
  "scroll": {
    "sensitivity": 3,
    "acceleration": 3,
    "acceleratedScroll": false,
    "scrollAlways": false,
    "onVerticalScrollbar": "code:53",
    "onHorizontalScrollbar": "code:58",
    "mergeWheelDelta": 2,
    "wheelResolution": 1,
    "autoWheelResolution": 3,
    "modifierScroll": {
      "shift": "none",
      "ctrl": "none",
      "ctrlShift": "none",
      "alt": "code:55",
      "shiftAlt": "none",
      "ctrlAlt": "none"
    }
  },
  "tray": {
    "showTrayIcon": true,
    "showBalloonTip": false,
    "menuStyle": 2
  },
  "profiles": [
    {
      "name": "Default",
      "processPattern": "*",
      "gesturesEnabled": true,
      "gestures": [
        { "strokes": "←",  "action": { "type": "appcommand", "command": "BrowserBackward" } },
        { "strokes": "→",  "action": { "type": "appcommand", "command": "BrowserForward" } },
        { "strokes": "↑",  "action": { "type": "appcommand", "command": "BrowserRefresh" } },
        { "strokes": "↓→", "action": { "type": "key", "keys": "Ctrl+W" } },
        { "strokes": "↑↓", "action": { "type": "close" } }
      ]
    }
  ]
}
```

`gesture` / `scroll` / `tray` の既定値は**ユーザーの実 `Kazaguru.ini` から採取した値**（使用感の踏襲）。全項目は GUI/JSON で変更可能。GUI で編集・保存時に即リロード。手動編集も FileSystemWatcher で反映。

### 既定値の出所（ユーザー ini 由来）

| 新 config | 既定値 | 由来 ini キー | 意味・状態 |
|---|---|---|---|
| `gesture.range` | 8 | `GestureRange` | ストローク認識距離（方向確定の最小移動量） |
| `gesture.timeoutMs` | 1000 | `GestureTimeout` | ジェスチャー入力のタイムアウト |
| `gesture.pushHoldTimeMs` | 500 | `PushHoldTime` | 右ボタン長押し判定時間 |
| `gesture.rapidFire` | false | `EnableGesRapidFire`=0 | 連射無効 |
| `gesture.drawStroke` | **false** | `DrawGesStroke`=0 | **軌跡描画はオフ**（ユーザー現状に合わせる） |
| `gesture.drawingType` | 0 | `GesDrawingType` | 描画方式（描画有効時）。enum 詳細は動的解析で確定 |
| `gesture.strokeWidth` | 2 | `GesStrokeWidth` | 線幅 px（描画有効時） |
| `gesture.validStrokeColor` | `#80FF00` | `ValidGesStrokeColor`=65408 | 有効ストローク色（COLORREF→RGB 変換） |
| `gesture.invalidStrokeColor` | `#FFFF00` | `InvalidGesStrokeColor`=65535 | 無効ストローク色 |
| `scroll.sensitivity` | 3 | `Sensitivity` | スクロール感度 |
| `scroll.acceleration` | 3 | `Acceleration` | スクロール加速度 |
| `scroll.acceleratedScroll` | false | `AcceleratedScroll`=0 | スクロール加速の有効/無効 |
| `scroll.scrollAlways` | false | `ScrollAlways`=0 | 常時カーソル下スクロール（v1未実装機能・値のみ保持） |
| `scroll.onVerticalScrollbar` | `code:53` | `ScrollExOnVScrlBar`=53 | 垂直スクロールバー上でホイール回転時の挙動。オリジナル「ホイール」タブのドロップダウン。意味未確定（D.4） |
| `scroll.onHorizontalScrollbar` | `code:58` | `ScrollExOnHScrlBar`=58 | 水平スクロールバー上でホイール回転時の挙動。同上。`horizontal` を選べば縦ホイール→水平スクロール |
| `scroll.mergeWheelDelta` | 2 | `MergeWheelDelta` | ホイールデルタ統合 |
| `scroll.wheelResolution` | 1 | `WheelResolution` | ホイール解像度 |
| `scroll.autoWheelResolution` | 3 | `AutoWheelResolution` | 自動ホイール解像度 |
| `scroll.modifierScroll` | shift/ctrl/ctrlShift/shiftAlt/ctrlAlt="none", alt="code:55" | `ScrollExShift/Ctrl/CtrlShift/ShiftAlt/CtrlAlt`=0, `ScrollExAlt`=55 | 修飾キー別の拡張スクロール動作。オリジナル v1.67 は **Shift / Ctrl / Ctrl+Shift / Alt / Shift+Alt / Ctrl+Alt** の6通りを選択でき、選択肢に「水平スクロール」を含む（ヘルプ「拡張スクロール」タブで確認）。ユーザー設定は Alt のみ 55（意味未確定）、他は 0。`53/55/58` 等コードの意味は動的解析 D.4 で確定 |
| `tray.showTrayIcon` | true | `ShowTrayIcon` | トレイアイコン表示 |
| `tray.showBalloonTip` | false | `ShowBalloonTip`=0 | バルーン通知 |
| `tray.menuStyle` | 2 | `TrayIconMenuStyle` | トレイメニュー様式 |

> 不透明な enum/bitflag 値（`GestureFlags=262`, `ScrollExFlags=18`, `ScrollEx*` の `53/55/58`, `GesDrawingType`）は、意味が未確定でも**生の値を既定として保持**し、使用感を保つ。各コードの意味は実装フェーズの動的解析でデコードして名前付き設定に昇格する。スコープ外機能（音量・タスクバー・オートスクロール等）の ini 値は取り込まない。

## エラー処理

| 事象 | 対処 |
|---|---|
| フックが OS に外される | 30 秒間隔の生存確認タイマーで検知し自動再設置。トレイ通知で知らせる |
| 設定ファイル破損 | エラーダイアログ＋既定設定で起動。壊れたファイルは `.bak` に退避し上書きしない |
| 多重起動 | Mutex で防止。2 つ目の起動時は既存インスタンスの設定画面を開く |
| UIPI（管理者ウィンドウ） | 検知不可のため README に「管理者アプリで使うには本体を管理者実行」と明記 |

## テスト方針

- **ユニットテスト（xUnit）**: 軌跡→ストローク変換、ストローク→アクションマッチング、アクション定義の解釈（key / appcommand / close の振り分けとキー文字列パース）、プロファイル解決。Win32 非依存のピュアロジックを対象。判定しきい値・変換係数は動的解析で実測したオリジナル値を期待値に用いる
- **挙動比較（移植の受け入れ基準）**: 同一の入力シナリオをオリジナルと再実装に与え、出力（送信されるキー、スクロール量・方向、右クリック抑制の有無）が一致することを確認。差異は設計書に記録し、許容/修正を判断
- **手動確認**: フック・SendInput 周りは確認手順書（チェックリスト）をリポジトリに置く（メモ帳でジェスチャー、Excel でスクロールバー水平スクロール、Chrome で Ctrl+W 送信、右クリックメニューが正常に出ること、一時停止が効くこと）

## 実装順

1. フック基盤＋トレイ常駐（素通しで安定動作確認）
2. GestureEngine＋キー送信（設定はハードコード）
3. ConfigStore＋プロファイル対応
4. ScrollEnhancer
5. SettingsForm（GUI）
