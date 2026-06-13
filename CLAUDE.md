# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 概要

このリポジトリは「かざぐるマウス」（KazaguruMouse64）のバイナリ配布物を参考に、.NET(C#)でリプロダクトするプロジェクトです。

- **用途**: Windows 用マウス拡張ユーティリティ（マウスジェスチャー、スクロール強化など）
- **原作者**: Mitsuhal (Static Flower)
- **ライセンス**: 個人用途(配布予定なし)

## ファイル構成

| ファイル | 説明 |
|---|---|
| `Kazaguru.exe` | メイン実行ファイル |
| `Kazasub.dll` | サブ DLL |
| `Kazawow64.exe` | WOW64（32/64ビット橋渡し）ヘルパー |
| `hook\1.6.7.762\Kazahook.dll` | 64ビット用フック DLL |
| `hook\1.6.7.762\Kazahook32.dll` | 32ビット用フック DLL |
| `Kazaguru.chm` | ヘルプファイル |

## 注意事項

- KazaguruMouse64のバイナリ解析状況マップは docs/superpowers/specs/2026-06-11-analysis-status-map.md に記載。
