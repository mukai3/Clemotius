# Clemoutis 実装計画

作成日: 2026-06-11
対象設計書: [2026-06-11-clemoutis-design.md](../specs/2026-06-11-clemoutis-design.md)
戦略: systematic（依存順の段階実装）

## 全体方針

設計書「実装順」に沿い、**各フェーズが単体で動作確認できる**ように積み上げる。前フェーズが安定してから次に進む（CLAUDE.md の「コミット前に動作確認」を遵守）。Win32 非依存のピュアロジック（ジェスチャー判定・アクション解釈・プロファイル解決・設定モデル）を先に固め、フック/SendInput 等の副作用層は薄いラッパに隔離する。

```
フェーズ0 ── ソリューション基盤
   └─▶ フェーズ1 ── フック基盤＋トレイ常駐（素通し）
          └─▶ フェーズ2 ── GestureEngine＋アクション実行（ハードコード設定）
                 ├─▶ フェーズ3 ── ConfigStore＋プロファイル
                 │        └─▶ フェーズ5 ── SettingsForm（GUI）
                 └─▶ フェーズ4 ── ScrollEnhancer
```

並行可能: フェーズ3 と 4 はフェーズ2 完了後に独立して進められる。

---

## フェーズ0: ソリューション基盤

**目的**: ビルド・テスト・実行の土台を用意する。

タスク:
- [ ] 0.1 .NET 8 ソリューション作成。`Clemoutis`（WinForms, `OutputType=WinExe`, `TargetFramework=net8.0-windows`, `UseWindowsForms=true`）、`Clemoutis.Core`（クラスライブラリ・Win32非依存ロジック）、`Clemoutis.Tests`（xUnit）の3プロジェクト
- [ ] 0.2 `.gitignore`（`bin/`, `obj/`）、`Directory.Build.props`（`Nullable=enable`, `LangVersion=latest`, `<PlatformTarget>x64</PlatformTarget>`）
- [ ] 0.3 P/Invoke 集約用の `Clemoutis/Interop/` 名前空間を用意（空でよい）

受け入れ基準: `dotnet build` 成功、`dotnet test` が0件でも成功で完走、`dotnet run --project Clemoutis` で空のトレイ常駐プロセスが起動。

---

## フェーズ1: フック基盤＋トレイ常駐

**目的**: 入力を捕捉して素通しするだけの安定した常駐アプリ。設計の「フック生存監視」「多重起動防止」もここで入れる。

タスク:
- [ ] 1.1 `MouseHook` / `KeyboardHook`: `SetWindowsHookEx(WH_MOUSE_LL / WH_KEYBOARD_LL)` を P/Invoke。コールバックは受信イベントを `InputRouter` へ渡すのみ（フック内処理は最小限）。デリゲートを static フィールドで保持しGC回収を防ぐ
- [ ] 1.2 `InputRouter`: この段階は全イベント素通し（`CallNextHookEx`）。後続フェーズの差し込み口を用意
- [ ] 1.3 `TrayIcon`: `NotifyIcon` + コンテキストメニュー（設定を開く=後でダイアログ・一時停止・終了）。「一時停止」でフック解除/再設置
- [ ] 1.4 フック生存監視: 30秒タイマーでフック有効性を確認し、外れていたら再設置＋トレイ通知
- [ ] 1.5 多重起動防止: 名前付き Mutex。2つ目起動時は既存インスタンスにメッセージ送出（設定画面を開く要求。画面未実装の間はトレイ通知でよい）
- [ ] 1.6 修飾キー状態の追跡（KeyboardHook 側）。後続のスクロール/ジェスチャーが参照する `IModifierState`

受け入れ基準: 起動して通常のマウス/キーボード操作が一切阻害されない。一時停止→解除が効く。タスクマネージャでフックを擬似的に外す（別アプリ起動等）状況でも自動再設置がログに出る。2重起動できない。

リスク: 低レベルフックのコールバック遅延でOSにフックを外される。→ コールバック内は割り当てゼロ・即return を徹底。

---

## フェーズ2: GestureEngine＋アクション実行

**目的**: 右ドラッグジェスチャーで実アクションを発火。設定はこの段階ハードコード。

### ピュアロジック（Clemoutis.Core, TDD）
- [ ] 2.1 `StrokeEncoder`: マウス移動座標列 → `↑↓←→` ストローク列。しきい値 `range`（既定8）で方向確定、同方向連続は1ストロークに集約。**単体テスト必須**
- [ ] 2.2 `GestureMatcher`: ストローク列 → ジェスチャー定義のマッチング（完全一致）。**単体テスト必須**
- [ ] 2.3 アクションモデル: `KeyAction`（"Ctrl+W"等のパース→VK＋修飾フラグ）、`AppCommandAction`（`BrowserBackward`等→`APPCOMMAND_*`値）、`CloseAction`。キー文字列パーサは**単体テスト必須**

### 副作用層（Clemoutis）
- [ ] 2.4 `GestureEngine`: 右ボタンDOWN保留→移動でStrokeEncoderに供給→UP時にMatcher照合。設計のデータフロー通り(一致=実行/不一致=何もしない/ストローク無=右クリック再生)
- [ ] 2.5 `TargetWindowResolver`: ジェスチャー開始位置のトップレベル窓を特定（`WindowFromPoint`+`GetAncestor(GA_ROOT)`）
- [ ] 2.6 `ActionExecutor`:
  - KeyAction → `AttachThreadInput`+`SetForegroundWindow`+`SendInput`
  - AppCommandAction → `SendMessageW`/`SendNotifyMessageW(WM_APPCOMMAND)`
  - CloseAction → `PostMessageW(WM_CLOSE)`
- [ ] 2.7 ストローク無で離した場合の右クリック再生（飲み込んだ右DOWN/UPを`SendInput`で再注入）

受け入れ基準: ハードコードした「←=戻る」「↓→=Ctrl+W」等が Chrome/メモ帳で期待通り動く。ジェスチャーしない右クリックは通常メニューが正常に出る。不一致ジェスチャーは無反応（誤爆なし）。2.1–2.3 のテストが緑。

---

## フェーズ3: ConfigStore＋プロファイル

**目的**: ハードコードを排し、設計書の JSON 設定からジェスチャー/プロファイルを駆動。

### ピュアロジック（TDD）
- [ ] 3.1 設定モデル（`gesture`/`scroll`/`tray`/`profiles`）の C# レコード。設計書の既定値（ユーザーini由来: `range=8`, `timeoutMs=1000`, `drawStroke=false`, 色`#80FF00`/`#FFFF00` 等）を埋め込んだ `Default` を用意
- [ ] 3.2 `ProfileResolver`: 前面プロセス名/クラス名パターン → 適用プロファイル決定（マッチ無=Default）。**単体テスト必須**
- [ ] 3.3 JSON シリアライズ（`System.Text.Json`）。色は`#RRGGBB`文字列⇔COLORREF/Color 変換。アクションは設計書の`{"type":...}`判別ユニオン

### 副作用層
- [ ] 3.4 `ConfigStore`: `%APPDATA%\Clemoutis\config.json` 読み書き。初回は Default を書き出す。`FileSystemWatcher` で手動編集を即リロード（デバウンス付き）
- [ ] 3.5 破損時処理: パース失敗→壊れたファイルを`.bak`退避→Default起動→トレイ通知（上書きしない）
- [ ] 3.6 GestureEngine/ProfileResolver を ConfigStore 駆動に接続

受け入れ基準: config.json 編集→保存で再起動なしに反映。プロセス別プロファイル（例 chrome だけ別割当）が効く。壊れたJSONで起動してもクラッシュせず Default 動作＋`.bak`生成。3.1–3.3 テスト緑。

---

## フェーズ4: ScrollEnhancer

**目的**: スクロール拡張（設計の v1 スコープ: スクロールバー上水平化・修飾キー変換）。

- [ ] 4.1 `ScrollEnhancer`: ホイール受信時、カーソル下UI要素を判定
- [ ] 4.2 水平スクロールバー検出: `WindowFromPoint`+クラス名（`ScrollBar`）/ 必要なら MSAA（`OLEACC`）で要素ロール判定。設計どおりオリジナルは MSAA 使用 → まず Win32 で実装し不足なら MSAA 追加
- [ ] 4.3 縦ホイール→`WM_MOUSEHWHEEL` 変換送出（元`WM_MOUSEWHEEL`は飲み込む）
- [ ] 4.4 修飾キー変換: 設定 `scroll.modifierRules`（既定 Alt 有効）に従い変換。`mergeWheelDelta`/`wheelResolution` を反映
- [ ] 4.5 該当しなければ素通し

受け入れ基準: 横スクロールバー上で縦ホイール→水平移動（Excel等）。Alt+ホイールが設定通り動作。通常のスクロールは無変更で素通し。

注: コード値（`ScrollExAlt=55`等）の意味は未確定。実装は「生コードを保持しつつ、判明した分から名前付き動作へマッピング」する設計書方針に従う。

---

## フェーズ5: SettingsForm（GUI）

**目的**: WinForms 設定画面で全項目を編集可能に。

- [ ] 5.1 タブ構成: ［ジェスチャー］［スクロール］［プロファイル］［一般/トレイ］
- [ ] 5.2 プロファイル一覧 ＋ ジェスチャー⇔アクション編集（ストローク入力UI、アクション種別=key/appcommand/close の選択）
- [ ] 5.3 gesture/scroll/tray の各設定（range, timeout, 色ピッカー, 修飾キールール 等）
- [ ] 5.4 保存→ConfigStore 経由で即反映。トレイ「設定を開く」と多重起動時の起動要求に接続
- [ ] 5.5 入力バリデーション（数値範囲・キー文字列の妥当性・色形式）

受け入れ基準: 画面操作だけでジェスチャー追加・編集・プロファイル作成ができ、保存で即時反映。不正値は保存前に弾く。

---

## 動的解析タスク（設計書 RE 方針・実装と並行）

実装をブロックしないが、忠実度を上げるため早期着手。結果は設計書へ追記し、対応する既定値/マッピングを更新する。
- [ ] D.1 組み込みコマンドID（`L=1`,`R=2`,`LR=101`,`DL=112`,`U=5`,`UD=3`）の実機構（`WM_APPCOMMAND` lParam か内部処理か）を Spy++/API Monitor で確定
- [ ] D.2 Chromium のアプリ種別判定（IE/Mozilla/一般窓のどれ・判定はクラス名か）
- [ ] D.3 工場出荷時デフォルトのジェスチャー割り当て（ini削除→初回起動で再生成して採取）
- [ ] D.4 `GestureRange` の画素換算、スクロール変換係数、`ScrollEx*` コード（53/55/58）の意味
- [ ] D.5 各アクションの送出メッセージを実測し、再実装と挙動一致を検証（受け入れ基準の根拠）

---

## 横断的事項

- **テスト**: フェーズ2,3 のピュアロジックは TDD。フック/SendInput は設計書のチェックリスト（メモ帳ジェスチャー/Excel水平スクロール/Chrome Ctrl+W/右クリックメニュー/一時停止）で手動確認。手順書を `docs/manual-test-checklist.md` に置く
- **挙動比較**: 同一入力をオリジナルと Clemoutis に与え出力一致を確認（D.5）。差異は設計書に記録し許容/修正判断
- **権限**: 管理者ウィンドウ（UIPI）にはフック/キー送信が届かない → README に「管理者アプリ操作時は本体も管理者実行」
- **コミット規律**: 各フェーズ完了時に動作確認してからコミット（CLAUDE.md 準拠）

## 完了の定義（v1）

設計書 v1 スコープ（ジェスチャー＝key/appcommand/close、スクロール＝バー上水平化＋修飾キー変換、トレイ常駐、JSON設定、GUI）が Win10/11 で安定動作し、ユーザーの ini 由来既定値で「現状のかざぐるマウスの使用感」を踏襲できている状態。
