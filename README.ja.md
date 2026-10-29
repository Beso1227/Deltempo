<div align="center">

  <a href="https://beso1227.github.io/Deltempo/">
    <img src="docs/social-preview.png" alt="Deltempo - 純粋な精密さを重視した Windows クリーナー＆メモリオプティマイザー" width="100%" />
  </a>

  <br />

  # Deltempo: オープンソースの Windows クリーナー、アプリアンインストーラー、メモリオプティマイザー

  <p align="center">
    <strong>README:</strong>
    <a href="README.md">🇬🇧 English</a> ·
    <a href="README.es.md">🇪🇸 Español</a> ·
    <a href="README.zh-CN.md">🇨🇳 简体中文</a> ·
    <a href="README.hi.md">🇮🇳 हिन्दी</a> ·
    <a href="README.fr.md">🇫🇷 Français</a> ·
    <a href="README.pt-BR.md">🇧🇷 Português</a> ·
    <a href="README.ar.md">🇸🇦 العربية</a> ·
    <a href="README.ru.md">🇷🇺 Русский</a> ·
    <b>🇯🇵 日本語</b>
  </p>

  <p><strong>Windows 10 &amp; 11 向けの無料・オープンソースのディスククリーナーおよび RAM オプティマイザー。一時ファイル、AppData の不要データ、GPU シェーダーキャッシュから 10〜40 GB 以上の領域を回収 — さらにディープなアプリのアンインストーラーと重複ファイル検出機能も搭載。テレメトリゼロ、広告なし、インストーラー不要。</strong></p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48"><img alt="Download the latest Deltempo for Windows" src="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://beso1227.github.io/Deltempo/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48"><img alt="Visit the Deltempo official website" src="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/deltempo_cli.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48"><img alt="Download the Deltempo headless CLI" src="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48"><img alt="Star Deltempo on GitHub" src="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48" height="48" /></picture></a>
  </p>

  <p>
    <sub>ターミナルがお好みですか？1 行で最新リリースをインストールして検証できます &mdash;
      <code>irm https://beso1227.github.io/Deltempo/win | Invoke-Expression</code>
      &middot; バグや機能のアイデアを見つけましたか？<a href="https://github.com/Beso1227/Deltempo/issues/new/choose"><strong>Issue を開いてください</strong></a> &mdash; すべてのリクエストに目を通します。
      Deltempo がディスク容量を回収してくれたなら、<a href="https://github.com/Beso1227/Deltempo"><strong>star</strong></a> は他の人に見つけてもらうための大きな助けになります。</sub>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0&labelColor=16212B"><img alt="Latest Release" src="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/actions/workflows/ci.yml"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github&labelColor=16212B"><img alt="CI Build Status" src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github" height="28" /></picture></a>
    <a href="docs/TESTING.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/tests-1483_passed-00F2B0?style=for-the-badge&label=QUALITY&labelColor=16212B"><img alt="Tests: 1471 passed, 0 failed" src="https://img.shields.io/badge/tests-1483_passed-00F2B0?style=for-the-badge&label=QUALITY" height="28" /></picture></a>
    <a href="LICENSE"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github&labelColor=16212B"><img alt="License: MIT" src="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github" height="28" /></picture></a>
  </p>

  <p>
    <a href="https://dotnet.microsoft.com/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white&labelColor=16212B"><img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white" height="28" /></picture></a>
    <a href="https://learn.microsoft.com/dotnet/csharp/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white&labelColor=16212B"><img alt="C# 14" src="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white" height="28" /></picture></a>
    <a href="#at-a-glance"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows&labelColor=16212B"><img alt="Platform Support" src="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY&labelColor=16212B"><img alt="Portable Single-File" src="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#privacy"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY&labelColor=16212B"><img alt="Zero Telemetry, 100% Offline" src="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY" height="28" /></picture></a>
    <a href="docs/THREAT_MODEL.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY&labelColor=16212B"><img alt="STRIDE Hardened" src="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md#safety-pipeline"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY&labelColor=16212B"><img alt="Two-Phase Verified Safety" src="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY&labelColor=16212B"><img alt="NT Kernel Native Memory Engine" src="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#quick-start"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP&labelColor=16212B"><img alt="One-line install" src="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github&labelColor=16212B"><img alt="GitHub Downloads" src="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github&labelColor=16212B"><img alt="GitHub Stars" src="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/pulls"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING&labelColor=16212B"><img alt="PRs Welcome" src="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING" height="28" /></picture></a>
  </p>

  <p>
    <sub>
      <a href="docs/ARCHITECTURE.md">アーキテクチャ</a> &middot;
      <a href="docs/THREAT_MODEL.md">脅威モデル</a> &middot;
      <a href="docs/TESTING.md">テストガイド</a> &middot;
      <a href="docs/BENCHMARKS.md">ベンチマーク</a> &middot;
      <a href="docs/RELEASES.md">リリースエンジニアリング</a> &middot;
      <a href="SECURITY.md">セキュリティポリシー</a> &middot;
      <a href="#quick-start">クイックスタート</a>
    </sub>
  </p>

  <br />

  <video src="docs/deltempo.mp4" poster="docs/deltempo.webp" controls preload="metadata" width="100%"></video>

</div>

---

  <a id="at-a-glance"></a>

## 概要

| 項目 | 詳細 |
| :--- | :--- |
| **公式サイト** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **プラットフォーム** | Windows 10 & 11（64 ビット / x64） |
| **バージョン** | v3.2.0（本番リリース） |
| **ライセンス** | オープンソース（[MIT](LICENSE)） |
| **インターフェース** | モダンなデスクトップ GUI（WPF Fluent）とヘッドレスのターミナル CLI |
| **配布形態** | ポータブルな単一ファイル実行ファイル（自己完結、インストーラー不要） |
| **テストカバレッジ** | 1471 件の自動テスト（合格率 100%、失敗 0、スキップ 0）、ファイルシステムに対する敵対的ファズィング |
| **CLI の利用** | `deltempo` コマンドは GUI 初回起動時に自動インストール — 手動設定は不要 |
| **テレメトリ** | テレメトリゼロ。スキャン、クリーンアップ、メモリ、アンインストーラーの操作は 100% オフラインで実行されます |
| **セーフティエンジン** | 2 段階の計画（`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`）、5 段階のリスクティア、トランザクションジャーナリング |
| **意図的な削除拒否** | キャッシュのように見えても、実際には重要な役割を担っているパス（MSI ソースキャッシュ、ユーザーごとの .NET SDK インストール、ドライバベンダーのルートディレクトリ、Cargo の `registry\src`、Audacity のセッション復旧データ）を、Deltempo は決して削除対象にしません。[§2.1](#21-what-deltempo-refuses-to-delete) を参照。 |
| **AppData 使用状況レポート** | AppData を占める容量を大きい順に示す読み取り専用レポート。アプリ内および `deltempo appdata-report` から利用できます。どちらの画面にも削除パスは存在しません。 |
| **アプリアンインストーラー** | 一括サイレントアンインストール、BCU エンジン、AppData/レジストリの残骸トレース削除、動作不良アプリの強制ワイプ |
| **復元ポイント** | アンインストール前の任意の Windows システム復元ポイント（ユーザー制御、デフォルト無効） |
| **サービスインテリジェンス** | 3 段階の安全判定、オフラインヒューリスティクス、マルチモデル AI を用いて「これを無効にすると問題が起きますか？」という疑問に答えます |
| **プロセスマネージャー** | リアルタイムのプロセス一覧、ライブな高 DPI アプリアイコン抽出、メモリ使用量の分析 |
| **WinUtil 連携** | [Chris Titus Tech WinUtil](https://christitus.com/win)（CTT）を 1 クリックで起動（Chris Titus によるサードパーティ製） |
| **メモリエンジン** | Windows NT カーネルのネイティブコール（`NtSetSystemInformation`、`EmptyWorkingSet`） |
| **設定ハブ** | カテゴリ別 4 タブのコントロールセンター（*アップデート*、*一般*、*メモリ*、*ストレージと安全性*） |
| **トレイガーディアン** | 高 DPI ネイティブ Win32 アイコン（`LoadCrispTrayIcon`）、ライブ RAM テレメトリと 1 クリックのブースト |
| **テーマとアクセシビリティ** | プロ級の目に優しい Porcelain Slate ライトモード、Obsidian ダークモード、アラビア語の RTL レイアウトへの完全な対応 |
| **アップデート** | SHA-256 による暗号学的検証を備えた Stable & Beta リリースチャンネル、アトミックなステージング |

---

## Deltempo とは？

**Deltempo** は、ディスク容量を安全に回収し、アンインストールしにくいソフトウェアを徹底的にアンインストールし、スタートアップ項目がブートに与える影響を監視し、システムメモリを最適化するために設計された、モダンで高性能なオープンソースの Windows メンテナンススイートです。個人のドキュメント、ブラウザの認証情報、重要なオペレーティングシステムコンポーネントには一切触れずに、使い捨てのアプリケーションキャッシュ、孤立したインストーラーの残骸、ビルド成果物、古いシステムログを削除します。

従来のクリーンアップツールは、不透明なブラックボックスとして動作し、同梱のアドウェアをインストールし、レジストリに深い残骸を残すことがよくあります。Deltempo は **安全第一のアーキテクチャ** に基づいて設計されています: 候補パスは明示的なリスクティアに分類され、削除前にシミュレーションされ、認可されたディレクトリルート内に制限され、ファイルシステムのレース条件を防ぐため、削除の直前に再検証されます。

ディスククリーンアップに加えて、Deltempo には、正規の Win32 および NT システムコールを通じてスタンバイページリストをフラッシュし、未使用のワーキングセットを切り詰める、低レベルの Windows NT カーネルメモリ管理ツールが含まれています。

---

## ⚔️ Deltempo と競合製品の比較

多くの Windows 向けクリーンアップ・最適化ツールは、商用アドウェアを同梱するか、侵入的なバックグラウンドサービスを要求するか、主要機能を有料サブスクリプションの背後に隠すか、古いコードベースに依存しています。

Deltempo は完全にオープンソースで、テレメトリゼロ、インストール不要。精密なクリーニング、ディープなソフトウェアのアンインストール、カーネルレベルのメモリ管理、スタートアップサービスのインテリジェンスを組み合わせたエンドツーエンドのスイートを提供します。

| 機能・対応範囲 | **Deltempo** | **CCleaner** | **BleachBit** | **Bulk Crap Uninstaller** | **Windows Storage Sense** |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **ライセンスとコードベース** | **MIT オープンソース（C# 14 / .NET 10）** | 専有ソフトウェア / 商用 | GPLv3 オープンソース（Python/GTK） | Apache 2.0 オープンソース（.NET） | 専有ソフトウェア（Microsoft） |
| **テレメトリとプライバシー** | **テレメトリゼロ（100% オフライン）** | ⚠️ トラッカーとデータ収集 | ✅ テレメトリゼロ | 最小限のテレメトリ | Windows 診断テレメトリ |
| **同梱アドウェア / 追加販売** | **なし / 一切なし** | ⚠️ 過去にアドウェア同梱と追加販売の実績 | なし | なし | なし |
| **メモリエンジン** | **ネイティブ NT カーネル（`NtSetSystemInformation`）** | ⚠️ 基本機能のみ（有料 Pro のみ） | ❌ なし | ❌ なし | ❌ なし |
| **ディープアンインストール** | **サイレント一括 + 残骸の除去** | ⚠️ 基本機能のみ（深い削除は有料 Pro） | ❌ なし | ✅ 包括的 | ❌ 追加/削除プログラムのみ |
| **任意の復元ポイント** | **任意（ユーザーの選択、デフォルト無効）** | ⚠️ 自動化 / 有料機能 | ❌ なし | 任意 | システム側での手動切り替え |
| **スタートアップサービスのインテリジェンス** | **あり（「何か壊れますか？」3 段階判定）** | ❌ 単純なオン/オフ切り替えリスト | ❌ なし | 詳細なレジストリ一覧 | タスクマネージャーの基本的な指標 |
| **残骸トレーススイープ** | **AppData、ProgramData、レジストリ、ショートカット** | ⚠️ 有料 Pro のみ | ❌ なし | ✅ 手動のレジストリ検索 | ❌ なし |
| **開発者向け・シェーダーのキャッシュ** | **NuGet、npm、pip、Cargo、Gradle、GPU シェーダー** | ❌ ブラウザと Windows のみ | ⚠️ 部分的 | ❌ なし | ❌ なし |
| **大きなファイルの検出** | **AI 分類（>50 MB、リスク分類付き）** | ❌ 基本的なファイル検索 | ❌ なし | ❌ なし | 基本的なドライブ別内訳 |
| **システムファイル修復** | **SFC、DISM、CHKDSK 統合** | ❌ 別途有料ツール | ❌ なし | ❌ なし | 手動のコマンドプロンプト |
| **WinUtil（Chris Titus）連携** | **1 クリックで管理者権限起動**（Chris Titus によるサードパーティ製） | ❌ なし | ❌ なし | ❌ なし | ❌ なし |
| **モダン UI と目の負担軽減** | **Obsidian ダークモードと Porcelain Slate ライトモード（WPF）** | 古い / 雑多 | 旧式 GTK2/3 インターフェース | 旧式 WinForms インターフェース | Windows 設定に内蔵 |
| **完全な CLI 対応** | **あり（`--json` とドライラン対応の `deltempo` CLI）** | ⚠️ 限られたコマンドスイッチ | 基本 CLI | 基本 CLI | ❌ なし |
| **配布形態** | **ポータブル単一ファイル（68 MB、インストーラー不要）** | セットアップインストーラーとサービスが必要 | インストーラーまたはポータブル zip | インストーラーとランタイムが必要 | OS に内蔵 |

---

## 🌟 主要機能の詳細

### 1. 🧹 26 種類以上のスコープに対応した精密なストレージクリーナー

Deltempo は、系统・開発・ゲームの各環境にまたがる使い捨てデータを対象にしながら、ユーザードキュメント、アクティブな認証トークン、個人設定には一切触れません:

* **オペレーティングシステムのスコープ**: ユーザーの一時フォルダー（`%TEMP%`）、Windows 一時フォルダー（`C:\Windows\Temp`）、Prefetch、Windows Update のダウンロードキャッシュ（`SoftwareDistribution\Download`）、Windows アップグレードの残骸（`$WINDOWS.~BT`）、配信最適化キャッシュ、Windows エラーレポート（`WER`）、メモリダンプ、フォント/サムネイルキャッシュ。
* **GPU とゲームのシェーダー**: NVIDIA App / GeForce Experience の OTA キャッシュ、AMD Radeon Software のキャッシュ、DirectX シェーダーキャッシュ（`D3DSCache`）、Vulkan パイプライン（`GLCache`）、Steam のシェーダー事前キャッシュ、Epic Games ランチャーの Web キャッシュ。
* **開発者エコシステム**: NuGet v3 ローカルキャッシュ、npm キャッシュ、pip キャッシュ、Rust の Cargo ターゲットキャッシュ、Gradle キャッシュ、Android Studio エミュレータの一時スナップショット、VS Code 拡張機能のキャッシュ。
* **モダンブラウザとコミュニケーションアプリ**: Chromium プロファイル（Chrome、Edge、Brave、Opera、Vivaldi、Arc）と Gecko プロファイル（Firefox）の使い捨てキャッシュディレクトリ（**ログインセッションは厳格に保持**）、および Discord、Slack、Spotify のメディアキャッシュ。
* **セーフティシールド（24 時間以内）**: 直近 24 時間に作成・変更されたファイルを除外する、任意で有効にできる保護機能。バックグラウンドで進行中のインストーラーや稼働中のエディターと干渉しません。
* **TOCTOU ガード**: ファイルを削除する直前にファイル境界・属性・正規化されたパスを検証し、レース条件を除外します。
* **孤立したストアアプリの検出**: 対応する MSIX パッケージがインストールされなくなった AppData ディレクトリは、ディレクトリ名のヒューリスティックではなく、バイナリ形式の `AppModel` リポジトリを参照した根拠（解析されたパッケージファミリー名とパブリッシャー ID による照合）から特定されます。検出結果は `LowRisk` として報告され、削除対象として事前選択されることはありません。

<a id="21-what-deltempo-refuses-to-delete"></a>

### 2.1 Deltempo が削除を拒否するもの

削除してよいものだけを知るクリーナーは危険です。「一時的なもの」と「使い捨てのもの」は、しばしば同じ嘘にすぎないからです。表面的にはビルドやダウンロードの作業領域とまったく同じように見えるパスが、実際には重要な役割を担っており、Deltempo は設計上これらを拒否します — これらのパスはスコープリゾルバから除外されるうえ、`ProtectionPolicy` によって独立に拒否されるため、設定・フラグ・CLI オプションのいずれに対しても対象として提示されることはありません:

| 拒否するパス | 削除が破壊的になる理由 |
| :--- | :--- |
| `%ProgramData%\Package Cache`<br>`%WinDir%\Installer\$PatchCache$` | `msiexec` がインストール済み製品を **修復** および **アンインストール** するために必要とする MSI ソースキャッシュです。削除すると、マシン全体で変更・修復と「プログラムと機能」からのアンインストール経路が恒久的に機能停止します。 |
| `%LOCALAPPDATA%\Microsoft\dotnet` | ユーザーごとの .NET SDK とランタイムの **インストールルート**（`dotnet.exe` ホスト、`sdk\`、`packs\`、`shared\`）であり、キャッシュではありません。削除してよいのは一時的なビルド作業領域 `%TEMP%\.net` だけです。 |
| `C:\NVIDIA`、`C:\AMD`、`C:\Intel`、`C:\Realtek` | ドライバーのインストーラーが展開するベンダーのルートディレクトリです。インストール中にクリーンアップを実行すると、インストーラーがまだ消費しておらず再取得もできないペイロードを削除してしまいます。対象になるのは特定のステージング用サブディレクトリのみです。 |
| `~\.cargo\registry\src` | `cargo build --offline` がコンパイルの基準とする **展開済み** クレートソースです。同階層の `registry\cache` はクリーンされますが、`src` はされません。片方だけを対象にすることは「すべてか無か」の操作であり、このターゲット単位のモデルでは安全に表現できないからです。 |
| `%LOCALAPPDATA%\Audacity\SessionData` | 未保存セッションの **復旧** データであり、作業領域ではありません。削除すると、一度も保存されなかった進行中の録音が失われます。 |
| `%ProgramData%\Microsoft\Windows Defender\Definition Updates\Backup` | Defender の定義更新を **ロールバック** するためのコピーです。削除すると、不具合のあった定義更新を以前の状態に戻す機能が失われます。 |
| `%LOCALAPPDATA%\Electronic Arts\EA Desktop\UserConfig` | EA Desktop のアプリケーション設定であり、キャッシュではありません。 |

これらは二重に拒否されています: パスの除外はスコープリゾルバ側で行われ、*さらに* `ProtectionPolicy` が独立に拒否します。したがって、リゾルバのリグレッション、ユーザー提供のルールパック、古い設定エントリが、これらを再導入することはありません。

このリストはコードで強制され、`CleanerServiceTests` でカバーされています — 除外項目ごとに、そのパスが存在しないことを検証するテストと、同じリスト内の本来の対象である作業領域が依然として存在することを証明する対照アサーションの両方が用意されており、この除外ルールが過度に広範な削除へ退化することはありません。

---

### 2. 📦 ディープルート対応のアプリアンインストーラーと残骸スイープ

しつこいブロートウェア、中途半端に削除されたソフトウェア、散らかったアンインストールウィザードにさよならを言いましょう:

* **統合アプリケーションインベントリ**: 64 ビットと 32 ビットのレジストリハイブ（`HKLM`、`HKCU`）に加え、モダンな Windows ストア（AppX/UWP）パッケージをスキャン。実際のインストールサイズ、発行元の検証情報、バージョン、インストール日を表示します。
* **任意のシステム復元ポイント**: 低速な 2 分間のシステム復元ポイントを強制するか、完全にスキップするだけの他ツールと異なり、Deltempo は判断をユーザーに委ねます。専用のトグルで、アンインストール前の復元ポイントを作成するかどうかを選択できます（**デフォルトはオフ**）。
* **サイレント一括マルチアプリ削除**: 複数のアプリケーションを選択して、延々と続くインストーラーダイアログを操作することなく、無人アンインストールを実行します。
* **ディープルートの残骸スイープ**: アプリケーションのアンインストーラーが完了した後、Deltempo のスキャナーが孤立した残骸を洗い出します:
  * レジストリのブランチ: `HKCU\Software\<Vendor>`、`HKLM\Software\<Vendor>`、`HKLM\Software\WOW6432Node\<Vendor>`。
  * ファイルシステムのディレクトリ: `%LocalAppData%\<App>`、`%AppData%\<App>`、`%ProgramData%\<App>`、`%ProgramFiles%\<App>`。
  * スタートアップのエントリとスタートメニューの孤立ショートカット。
* **破損ソフトウェアの強制ワイプ**: アンインストーラーが壊れている・見つからない・エラーを返す場合、Deltempo は関連するすべてのファイルシステムディレクトリを強制的にクリーンし、関連レジストリキーも登録解除します。

---

### 3. ⚡ ネイティブ Windows NT カーネルメモリオプティマイザー

メモリをスワップファイルに無理に押し込んで PC を遅くするだけの一般消費者向け「RAM クリーナー」とは異なり、Deltempo はネイティブで公式に文書化された Windows NT カーネルのシステムコールを利用します:

* **スタンバイリストの無効化**: `NtSetSystemInformation` を `SystemMemoryListInformation`（クラス `80`）とともに呼び出し、未使用のキャッシュ済みスタンバイメモリページをフラッシュして、メモリ需要の高いタスク（ゲーム、コンパイル、レンダリング）用の利用可能プールへ返します。
* **未使用ワーキングセットの切り詰め**: 管理者権限に昇格したプロセスのトークン（`SeProfileSingleProcessPrivilege` と `SeDebugPrivilege`）で `EmptyWorkingSet` を活用し、未使用のバックグラウンドプロセスが放棄したワーキングセットを解放します。
* **重要プロセスシールド**: 中核となる Windows コンポーネント（`csrss.exe`、`dwm.exe`、`explorer.exe`、`lsass.exe`、`services.exe`、`smss.exe`、`svchost.exe`、および Windows Defender）は自動的に保護され、決して切り詰められません。
* **バックグラウンド自動ブースト**: バックグラウンドでメモリ圧を監視し、物理 RAM 使用率がユーザー定義のしきい値（例: 85%）を超えると自動的にクリーンをトリガーできます。

---

### 4. 🧠 スタートアップマネージャーとサービスインテリジェンス

どのプログラムが PC のブート時間を遅くしているのか、もう悩む必要はありません:

* **「これを無効にすると問題が起きますか？」**: すべてのスタートアップアプリケーションとバックグラウンドサービスが、3 段階の判定バッジで分析されます:
  * 🟢 **SafeToDisable**: Windows と同時にブートする必要のない、便利なランチャー、ゲームアップデーター、コミュニケーションアプリ。
  * 🟡 **CautionNeeded**: オーディオコントロールパネル、トラックパッドユーティリティ、周辺機器ソフトウェアなど。ホットキーやトレイメニューが手動で起動するまで使えなくなる場合があります。
  * 🔴 **EssentialKeep**: セキュリティスイート、クラウド同期バックアップエージェント、必須のハードウェアドライバー。
* **デュアルインテリジェンスピプライン**:
  * **オフラインヒューリスティクス**: デジタル署名、検証済みのベンダー ID、バイナリパス、既知のプロセスデータベースに基づく、即座かつ決定論的な分類。
  * **オプションのマルチプロバイダー AI**: 必要に応じて詳細な動作サマリーを表示します。OpenAI、Anthropic Claude、Google Gemini、Groq、OpenRouter、およびローカルオフラインモデル（Ollama、LM Studio）に対応。
* **100% 可逆なレジストリトグル**: 無効化した項目は `Run_Deltempo_Disabled` レジストリキーに安全に保存されます。どの項目も 1 クリックで再有効化できます。

---

### 5. 🔍 AI 分類対応の大ファイルインスペクター

ディスク容量を実際に消費しているものが何かを見極めます:

* **マルチドライブスキャン**: `C:\` または任意のセカンダリ固定ドライブを高速にスキャンし、カスタマイズ可能なサイズしきい値（>50 MB、>100 MB、>500 MB、>1 GB）を超えるファイルを検出します。
* **自動カテゴリタグ付け**: 発見されたファイルをアーカイブ（`.zip`、`.rar`、`.7z`）、ディスクイメージ（`.iso`、`.vhd`）、仮想マシンドィスク（`.vmdk`、`.vhdx`）、インストーラー（`.msi`、`.exe`）、ビデオ/オーディオメディア、古いログファイルに自動的にグループ化します。
* **安全リスクのティアリング**: 操作する前にすべての大きなファイルの安全性を評価し、ハイパーバイザーディスクや重要なゲームのインストール先を誤って削除することを防ぎます。

---

### 6. 🛠️ Windows システム修復と CTT WinUtil 連携

インターフェースから直接 Windows オペレーティングシステムの破損を診断・修復します:

* **SFC（システムファイルチェッカー）**: 管理者権限のコンテキストで `sfc /scannow` を実行し、破損したシステムファイルを修復します。
* **DISM サービシング**: Windows コンポーネントストアの健全性をチェック・スキャン・復元します（`/Cleanup-Image /RestoreHealth`）。
* **WinSxS ベースリセット**: 古くなったコンポーネントストアのバージョンをクリーンアップし、大規模な Windows アップグレード後に数 GB を回収します。
* **CHKDSK とネットワークリセット**: 次回のブート時にディスクボリュームの検証をスケジュールするか、DNS フラッシュと Winsock スタックのリセットを 1 クリックで実行します。
 * **Chris Titus Tech WinUtil（CTT）**: 1 クリックのランチャーから、管理者権限の PowerShell セッションで WinUtil を起動し、デブロート・テレメトリの除去・winget の自動設定に利用できます。WinUtil は **[Chris Titus](https://christitus.com) が提供するサードパーティ製ユーティリティ**であり、Deltempo の一部ではなく、Deltempo の監査対象外です。[サードパーティに関する通知](THIRD-PARTY-NOTICES.md) を参照してください。

---

### 7. 📊 リアルタイムプロセスマネージャー

* **ネイティブ高 DPI アイコン抽出**: ネイティブの Win32 `SHGetFileInfo` と `ExtractIconEx` ルーチンを用い、32 ビットの鮮明な実行ファイルアイコンをリアルタイムに抽出します。
* **メモリと PID のテレメトリ**: リアルタイムのプロセスメモリ使用量、プロセス ID、発行元情報、ファイルパス。
* **安全な終了**: 保護された終了処理が、重要な Windows システムプロセスの誤終了を防ぎます。

---

### 8. 📊 AppData 使用状況レポートと孤立したストアアプリの検出

`%LOCALAPPDATA%` と `%APPDATA%` は、一般的な Windows マシンで最も多くのディスク容量を占める一方で、最も理解されていない領域です。壊滅的な誤りが起きやすい場所でもあります。Deltempo は既定ではこれらを読み取り専用として扱います。

* **読み取り専用の使用状況レポート**: AppData を実際に占めているものを大きい順に示す内訳です。アプリ内の **AppData レポート** ボタン、または CLI の `deltempo appdata-report` から参照できます。測定・並べ替え・表示のみを行います。どちらのインターフェースにも `--clean` も `--yes` も、削除コードパスも存在しません —— 偶然ではなく、意図的なものです。
* **リパースポイントの安全性**: 旧来の AppData 互換ジャンクション（`Application Data`、`Local Settings`）は `Local` を指しています。そこを辿ると同じツリーを再読込し合計値が水増しになるため、読み飛ばします。実際のプロファイルで測定したところ、この補正により 123 フォルダにわたる 59.5 GB という計測値は、実際の 32.6 GB に修正されました。
* **孤立した Microsoft ストアアプリ**: `StoreAppOrphanResolver` は AppModel パッケージリポジトリ（`HKCU` と `HKLM` の両方のハイブ）をバイナリ形式の根拠として読み取り、各 `LOCALAPPDATA\Packages` フォルダを対応する MSIX パッケージと照合します。フォルダ名（`Name_PublisherId`）は最後のアンダースコアで分割し、パブリッシャー ID と名前の接頭辞で照合することで、リポジトリキー（`Name_Version_Arch_ResourceId_PublisherId`）と突合されます。どちらのハイブも開けない場合、リゾルバは推測せずに何も返しません。孤立した項目は `LowRisk` として未選択で提示され、拒否ルールの単一の情報源は `ProtectionPolicy` のままです。

> **正しい答えは「何もない」であってよいのです。** ストアアプリを一件も削除していないマシンでは、レポートでは孤立アプリが 0 件と表示されます。これこそが意図された挙動です。この機能は検出数ではなく、偽陽性ゼロで評価されます。

### 9. 🎨 プロ級の目に優しいテーマと多言語 RTL 対応

ユーザー体験に対する細やかなこだわりをもって設計されています:

* **プロ級の目に優しいライトモード**: 目の負担を完全に軽減し、まぶしい白い画面を置き換える、心地よい Fluent/macOS 風の磁器とスレートのテーマ（`#F1F5F9`）。高コントラストの Ocean Azure（`#0284C7`）アクセントを採用。
* **Obsidian ダークモード**: 鮮やかなエレクトリックシアンのアクセント、控えめなグラスモーフィズム、ダブルベゼルのカードを備えた、洗練された深宇宙風のダークテーマ。
* **多言語対応（進行中）**: **英語、アラビア語、スペイン語、フランス語、ドイツ語**の言語セレクター。クリーナーのワークスペースはローカル化され、アラビア語ではネイティブ RTL フローが有効ですが、モーダルダイアログと残りのワークスペースはまだ翻訳されていません。
* **RTL レイアウトと数値保護**: アラビア語モードでは本物の右から左（RTL）のウィンドウフローを有効にしつつ、メトリクス・パス・進捗表示（`0.0 MB`、`32%`、`C:\...`）には左から右（LTR）の書式を厳格に適用。数値やストレージ情報が逆さまになったり読みにくくなったりすることはありません。

---

### 10. 🔔 ピクセルパーフェクトなシステムトレイガーディアン

* **本物の高 DPI Win32 アイコン（`LoadCrispTrayIcon`）**: 32 ビット ARGB アルファ透過を備えた Win32 GDI の直接アイコン生成（`CreateIconIndirect`）を使用。100%、125%、150%、175%、200% 以上のスケーリングのディスプレイでもぼやけない超鮮明なレンダリングを実現。
* **ライブホバーテレメトリ**: トレイのツールチップにリアルタイムのメモリ使用量を表示: `RAM: 42% (13.4 GB / 31.9 GB)`。
* **クイックコンテキストアクション**: メインウィンドウを開かずに右クリックで **1 クリックメモリブースト** または **クイックスマートクリーン** をトリガー。
* **Explorer のレジリエンス**: `explorer.exe` が再起動した場合に自動的にアイコンを復元するため、Windows の `TaskbarCreated` ブロードキャストメッセージをリッスンします。

---

## 💻 CLI リファレンスとヘッドレス自動化

Deltempo には、スケジュールタスク、システム管理者、ヘッドレス環境向けに設計された高性能でスクリプト可能な CLI（`deltempo_cli.exe` または `deltempo` コマンド）が含まれています。

```powershell
# 削除せずにクリーン可能な対象をプレビュー（ドライラン）
deltempo clean --dry-run

# 安全なクリーンアップを実行し、削除ファイルを Windows ごみ箱へ送る
deltempo clean --safe --recycle-bin

# スタンバイ RAM をフラッシュし、プロセスのワーキングセットを切り詰める
deltempo boost

# 復元ポイントを作成せずにアプリケーションをディープアンインストールする
deltempo uninstall "Google Chrome" --silent --force

# アンインストール時に任意で復元ポイントを作成する
deltempo uninstall "Epic Games Launcher" --restore-point

# システムテレメトリとメモリの健全性を構造化 JSON で出力する
deltempo status --json

# AppData を実際に占めているものを大きい順に見る（読み取り専用、削除は不可）
deltempo appdata-report --top 15
```

### CLI コマンド概要

| コマンド | 目的 | 主なフラグとオプション |
| :--- | :--- | :--- |
| `deltempo scan [filter]` | 対象をスキャンして使い捨てデータを検出 | `--json`、`--silent` |
| `deltempo clean [filter]` | 使い捨てキャッシュをクリーン | `--dry-run`、`--recycle-bin`、`--safe`、`--all`、`--json`、`--yes` |
| `deltempo smart-clean` | 検証済みの安全なキャッシュをクイックに除去 | `--dry-run`、`--json`、`--yes` |
| `deltempo deep-clean` | 自律的なフルクリーンアップ（RAM、DISM、スコープ） | `--dry-run`、`--json`、`--yes` |
| `deltempo restore-points` | システム復元ポイントを調査・消去 | `--clean`、`--all`（管理者権限への昇格が必要） |
| `deltempo appdata-report` | AppData の使用状況を読み取り専用・大きい順に表示 | `--json`、`--top <n>` — **削除フラグは存在しません** |
| `deltempo duplicates [path]` | バイト単位の重複ファイルファインダー（3 段階ハッシュ） | `--delete`、`--strategy` |
| `deltempo large [path]` | 容量を消費するファイルをドライブからスキャン | `--min <size>`、`--type <cat>`、`--safe`、`--top <n>`、`--sort <size\|date>` |
| `deltempo large inspect <file>` | ファイルのリスクティアと安全性判定を調査 | `--json` |
| `deltempo large clean` | 使い捨ての大きなファイルをごみ箱へ | `--dry-run`、`--yes`、`--safe-only` |
| `deltempo large delete <file>` | 特定のファイル 1 つをごみ箱へ | `--yes` |
| `deltempo force-delete <path>` | ロック中または除去困難なファイル/フォルダーを強制削除 | `--terminate-lockers`、`--recycle`、`--dry-run`、`--retry <n>`、`--force-override` |
| `deltempo force-delete scan <path>` | 除去されにくさ、ロックの所有者、安全ティアを調査 | `--json` |
| `deltempo force-delete pending` | 次回再起動時の削除待ちリストを表示 | `--json` |
| `deltempo boost` | NT カーネル経由でシステムメモリを最適化 | `--all`、`--standby`、`--cache`、`--workingsets`、`--json` |
| `deltempo procs` | 20 MB を超える重いバックグラウンドプロセスを一覧表示 | `--json` |
| `deltempo procs trim <pid\|name>` | 1 つのプロセスのワーキングセットを切り詰め | `--json` |
| `deltempo procs kill <pid\|name>` | 暴走しているバックグラウンドプロセスを 1 つ終了 | `--json` |
| `deltempo uninstall <app>` | ディープルートのアンインストールと残骸の除去 | `--dry-run`、`--force`、`--silent`、`--restore-point`、`--json` |
| `deltempo startup [list]` | スタートアップアプリとブートへの影響を調査 | `--high`、`--json` |
| `deltempo startup disable <app>` | スタートアッププログラムを可逆的に無効化 | N/A |
| `deltempo startup enable <app>` | 無効化したスタートアッププログラムを再有効化 | N/A |
| `deltempo repair [subcommand]` | Windows 整合性チェックとサービシング修復 | `all`、`sfc`、`dism`、`winsxs`、`chkdsk`、`scanhealth`、`update`、`network` |
| `deltempo schedule` | 週次タスクスケジューラのメンテナンスを管理 | install / remove / status |
| `deltempo shell` | Explorer の右クリックコンテキストメニュー連携を管理 | install / remove / status |
| `deltempo status` | システムテレメトリとメモリ情報を表示 | `--json` |
| `deltempo test` | エンジンのセルフチェック（メモリ API、検出済みスコープ） | N/A |
| `deltempo help` | コマンドリファレンス全体を出力 | N/A |
| `deltempo update [check]` | 公式リリースの確認またはアップデートの適用 | `check`、`--dry-run` |
| `deltempo register` | `deltempo` コマンド自体のインストール/調査 | `--status`、`--remove` |
| `deltempo unregister` | `register` が作成したすべての生成物を削除 | N/A |
| `deltempo kill` | 実行中の Deltempo バックグラウンドインスタンスを終了 | N/A |

**グローバルオプション** は各クリーン系コマンドに共通して適用されます: `--smart` / `--safe-only`、`--recycle-bin` / `-r`、`--unsafe`（24 時間シールドを無効化）、`--dry-run` / `-d`、`--yes` / `-y`、`--json` / `-j`、`--silent` / `-s`、およびタイムスタンプ付きの監査ログを出力する `--export <file>`。

---

<a id="quick-start"></a>

## 🚀 クイックスタート

> ### ⚡ 1 行で実行
>
> ```powershell
> irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
> ```
>
> 最新リリースをダウンロードし、SHA-256 を検証して `%LOCALAPPDATA%\Deltempo\bin` にキャッシュし、起動します。
> ヘッドレスのコンソールバイナリをお好みですか？`win` を [`win-cli`](https://beso1227.github.io/Deltempo/) に差し替えてください。
>
> ### 💻 …そして CLI もすぐに使えます
>
> 初回起動時に `deltempo` コマンドが自動インストールされます — `cmd.exe`、PowerShell、<kbd>Win</kbd>+<kbd>R</kbd> のいずれでも動作します。
> その後、**新規の**ターミナルウィンドウを開いて次を実行してください:
>
> ```powershell
> deltempo test        # エンジンのセルフチェック
> deltempo status      # ライブのディスク + RAM テレメトリ
> deltempo register --status
> ```

### オプション 1: ポータブル単一実行ファイル（推奨）

1. [最新リリース](https://github.com/Beso1227/Deltempo/releases/latest) ページから **`Deltempo.exe`** をダウンロードします。
2. `Deltempo.exe` を直接実行します（インストーラー不要、自己完結の単一ファイル）。
3. **今すぐスキャン** または **1 クリックディープクリーン** をクリックして空き領域を回収します。

### オプション 2: ターミナルのワンライナー（PowerShell）

ブラウザもインストーラーも不要で、ターミナルから直接最新リリースを起動します:

```powershell
irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
```

ブートストラップスクリプトは最新の `Deltempo.exe` をダウンロードし、公開されている `checksums.sha256` に対して SHA-256 を検証し、`%LOCALAPPDATA%\Deltempo\bin` にキャッシュして起動します。ヘッドレス CLI バイナリの場合は `win-cli` エントリポイントを使用してください:

```powershell
irm https://beso1227.github.io/Deltempo/win-cli | Invoke-Expression
```

マニフェストが読み取れない場合、またはハッシュが一致しない場合、処理は中止され、ダウンロードしたものは一切実行されません — 検証が省略されることはありません。なお、`Invoke-Expression` は引数を受け取らないため、ターゲットは `-Cli` スイッチではなくエントリポイントによって選択されます。

### オプション 3: `deltempo` コマンド

これらを手動で設定する必要はまったくありません。Deltempo を初めて起動すると、コンソールサブシステムの
CLI バイナリを用意し、ユーザーの `PATH` に追加し、<kbd>Win</kbd>+<kbd>R</kbd> エイリアスを登録し、
PowerShell プロファイルに `deltempo` 関数をインストールします
（古いインストールが残した古い登録が見つかった場合は修復します）。

知っておくべきことが 2 つあります:

- **新しいターミナルウィンドウを開いてください。** 開いているシェルは起動時の `PATH` を保持したままです。
- **オフラインでの初回起動？** コンソールバイナリを用意できなかった場合、何も登録されず、
  中途半端にインストールされたコマンドも残りません。デスクトップアプリにはまったく影響しません —
  次の成功した起動時に再試行されます。

自分で管理したいですか？`deltempo register` がオンデマンドで同じことを行い、
`deltempo unregister` は作成したすべての生成物を削除します。
`deltempo register --status` で何がインストールされ、どのバイナリを指しているかを正確に確認できます。

> 保護されたシステム状態に触れるコマンド（`restore-points`、`deep-clean` の DISM ステージ）は、
> 標準的な非昇格ターミナルから実行すると、明確なエラーと非ゼロの終了コードを返します。
> 管理者権限に昇格したシェルから実行するか、デスクトップアプリをご利用ください。

---

<a id="privacy"></a>

## 🔒 プライバシーとセキュリティの保証

Deltempo は、セキュリティとユーザープライバシーを揺るぎない基本原則として設計されています:

1. **ゼロテレメトリの保証**: Deltempo には **テレメトリゼロ**、アナリティクスライブラリ、広告 SDK、バックグラウンドの ping トラッカーは一切含まれません。通常のスキャン、クリーンアップ、メモリ最適化、アンインストールは **100% オフライン** で実行されます。
2. **決定的安全パイプライン**:

   ```text
   ┌────────┐     ┌────────┐     ┌─────────┐     ┌────────────┐     ┌─────────┐
   │  SCAN  │ ──► │  PLAN  │ ──► │ PROTECT │ ──► │ REVALIDATE │ ──► │  CLEAN  │
   └────────┘     └────────┘     └─────────┘     └────────────┘     └─────────┘
   ```

   候補ファイルは計画され、保護されたディレクトリ境界（ドキュメント、デスクトップ、コードリポジトリ、SSH 鍵、認証情報）に対して検証され、削除の直前に再検証されます。
3. **リパースポイントとトラバーサル防御**: NTFS のディレクトリジャンクション、シンボリックリンク、ボリュームマウントポイントは自動的に拒否され、ターゲット境界外へのトラバーサル攻撃を防ぎます。
4. **ローカルドライブ境界**: ローカル固定ドライブにのみ制限されます。リモートネットワーク共有と UNC パスはブロックされます。
5. **AppData の永続的状態の保護**: Deltempo は、`AppData` の配下に置かれているだけのアプリケーション *状態* を削除することを拒否します。OneDrive の同期データベース、Office と Outlook の認証情報、Windows 資格情報マネージャーの保管庫、最近使ったファイルのジャンプリスト、SSH と GnuPG の鍵素材、そして `NTUSER.DAT` / `UsrClass.dat` のシェルハイブはいずれも拒否されます。これは **ファイル単位ではなくディレクトリ単位で強制** されます: 再帰的な `Directory.Delete` は 1 回のシステムコールでサブツリー全体を削除し、ファイルごとのガードを参照しないため、残骸消去の処理はまず保護された子孫の有無を走査し、その走査は 5,000 ディレクトリを上限として打ち切られ、リパースポイントをスキップし、上限を超えた場合は **フェイルクローズ** します。
6. **暗号学的リリース検証**: 自動アップデートチェックは HTTPS を強制し、署名済み GitHub リリースマニフェストに対してバイナリの SHA-256 ダイジェストを検証します。

> マニフェスト署名検証には ECDSA P-256 方式を採用していますが、埋め込まれているのはプレースホルダー鍵であり、Authenticode のチェックを含め、いずれもまだ実際の更新経路には組み込まれていません。リリースアセットに対する SHA-256 ダイジェスト検証 *は* 強制されています。`docs/THREAT_MODEL.md` は、これらを緩和策ではなく既知のギャップとして記載しています。

---

## 🛠️ ソースからのビルド

### 前提条件

* Windows 10 または 11（64 ビット / x64）
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* PowerShell 7+ または Windows PowerShell 5.1

### コンパイルとテスト

```powershell
# リポジトリをクローン
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# ソリューション全体を Release 構成でコンパイル
dotnet build deltempo.sln -c Release

# CI と同じ方法でコードスタイルを検証する。deltempo.sln に対して実行し、
# 単一プロジェクトに対して実行しないこと: ソリューションには WPF アプリ、Core、CLI、WinUI シェル、
# テストプロジェクトが含まれます。プロジェクトごとのチェックではソリューションの一部しか検証されません。
dotnet format deltempo.sln --verify-no-changes --no-restore

# 自動テストスイートを実行（ユニットテスト・統合テスト 1471 件合格）
# xunit.v3 は Microsoft.Testing.Platform 上で動作し、.NET 10 SDK の `dotnet test` では
# 実行できません（"Testing with VSTest target is no longer supported"）。一度ビルドしてから、
# テスト実行ファイルを直接呼び出してください。
dotnet build Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release
./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category!=Benchmark"

# スタンドアロン単一ファイルのリリースバイナリをパッケージ（GUI & CLI）
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

生成された単一ファイルの実行ファイルは次の場所に出力されます:

* `publish\Deltempo.exe`（GUI）
* `publish\deltempo_cli.exe`（CLI）

---

## 🙏 クレジットとサードパーティツール

Deltempo はコントリビューターによって開発・保守されています。1 つの機能は
他人の成果物を利用しており、そのプロジェクトについてはここでクレジットを明記します。

| プロジェクト | 作者 | Deltempo での扱い |
|---|---|---|
| **[Chris Titus Tech Windows Utility (WinUtil)](https://christitus.com/win)** | **[Chris Titus](https://christitus.com)** | システム修復画面の **CTT WinUtil** ボタンはこのスクリプトをダウンロードし、WinUtil 自身のドキュメントの指示どおりに、**管理者権限**で新しい PowerShell ウィンドウを開いて実行します |

### ご注意ください

- WinUtil は **MIT ライセンスではなく**、**Deltempo の一部でもありません**。Deltempo は再配布もミラーもせず、バージョンの pin や検証も行いません — そもそもできません。
- スクリプトの取得と実行が 1 ステップで行われるため、**その URL から配信されるものは何であっても、マシン上で管理者権限で実行されます**。実行する前に必ず確認してください: <https://christitus.com/win>
- Deltempo のセーフティエンジン、削除前の再検証、リリース署名の検証が対象とするのは **Deltempo 自身のコードだけ**です。Deltempo がまだ確認していないコードが対象になることはありません。
- WinUtil が役に立った場合は、[Chris Titus](https://christitus.com) を直接支援してください。

Deltempo が実行する正確なコマンドを含む詳細については、
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を参照してください。

## 📜 ライセンス

Deltempo は **[MIT ライセンス](LICENSE)** の下で提供される、無料でオープンソースのソフトウェアです。

```text
Copyright (c) 2026 Deltempo Contributors
```

---

## ❓ よくある質問

**Deltempo は本当に無料ですか？**
はい。Deltempo は完全に無料の MIT オープンソースです — 有料プラン、追加販売、アカウント、広告は一切ありません。

**Deltempo は優れた CCleaner の代替になりますか？**
はい。Deltempo は、ディープアンインストーラー、重複ファイルファインダー、大ファイルハンター、Windows システム修復の内蔵まで備えた、無料・オープンソース・テレメトリゼロの CCleaner 代替です — インストールも不要です。

**Deltempo はテレメトリやトラッキングを行いますか？**
いいえ。Deltempo のテレメトリはゼロです — アナリティクスライブラリ、トラッキングピクセル、広告 SDK はありません。すべての操作は 100% オフラインで実行されます。

**Deltempo にインストーラーは必要ですか？**
いいえ。ポータブルな単一ファイルの実行ファイル（約 68 MB）です。`Deltempo.exe` をその場で直接実行するだけです — システムに何もインストールされません。

**ブラウザのキャッシュをクリーンすると、アカウントからログアウトされますか？**
いいえ。使い捨てのキャッシュディレクトリのみが削除されます。ログインセッション、Cookie、パスワードは厳格に保持されます。

**本当に RAM を解放できるのですか、それとも見た目だけですか？**
Deltempo は実際の NT カーネル API（`NtSetSystemInformation` によるスタンバイリストのフラッシュと、プロセスごとの `EmptyWorkingSet`）を呼ぶので、メモリは確かに移動します。ただしこれは **パフォーマンスの最適化ではなく**、速度が改善すると主張するつもりはありません。スタンバイキャッシュを破棄すると、Windows はスタンバイページを空きメモリとして数えるため *利用可能メモリ* の数値は上がりますが、マシンが速くなるわけではありません。スタンバイは Windows が新しいメモリ割り当てをディスクに触れずに満たすために使うキャッシュそのものなので、捨てると次の割り当てでページフォルトが発生します。ワーキングセットの切り詰めも同じトレードオフの縮小版で、ページがディスクへ押し出され、次に参照されたときに再びフォールトするため、アプリケーションが一時的に *遅く* なる、甚至は遅くなることがあります。正直にまとめると、**解放されたメモリは、あとで読み戻されるメモリ**です。現実的な効果は「空き RAM が増える」よりもずっと狭く、純粋に膨れ上がったアプリケーションの負担を軽くする、またはメモリが逼迫している環境でアドレス空間を確保できる程度です。スピードボタンではありません。

**Deltempo は自動でシステム復元ポイントを作成しますか？**
いいえ、デフォルトでは作成しません。復元ポイントの作成は明示的なオプションのトグルであり、強制的な数分かかるチェックポイントで遅くなることは決してありません。

**Deltempo でファイルを削除するのは安全ですか？**
はい。Deltempo は候補をリスクティアに分類し、計画をシミュレートし、認可されたディレクトリルート内に留まり、ファイルシステムのレース条件を防ぐため削除の直前にパスを再検証します。ドキュメント、デスクトップ、コードリポジトリ、SSH 鍵が対象になることは決してありません。

**Deltempo を自動化できますか？**
はい。`--json` と `--dry-run` を備えた `deltempo` CLI を使用するか、Windows タスクスケジューラでスケジュールするか、CI/CD パイプラインに統合できます。

**Deltempo は macOS や Linux で使えますか？**
いいえ。Deltempo は Windows 専用です（Windows 10 および Windows 11、64 ビット）。macOS や Linux では BleachBit をご利用ください。

---

## 🤖 AI エージェント・インテグレーター向け

AI アシスタント、クローラー、自動化ツールの場合、Deltempo は構造化された機械可読のコンテキストを公開しています:

| リソース | 目的 |
| :--- | :--- |
| [`docs/llms.txt`](https://beso1227.github.io/Deltempo/llms.txt) | 簡潔な概要、リンク、コア機能 |
| [`docs/llms-full.txt`](https://beso1227.github.io/Deltempo/llms-full.txt) | 完全なコンテキスト: 機能、CLI リファレンス、比較、FAQ |
| [`docs/ai-catalog.json`](https://beso1227.github.io/Deltempo/ai-catalog.json) | 構造化された製品メタデータ、キーワード、ダウンロードエンドポイント |
| [`llms.txt`](llms.txt)（リポジトリのルート） | 上記の正規コンテキストを指す、リポジトリ内の短い概要 |

これら 3 つすべては、検索エンジンと回答エンジンに対して `robots.txt` で明示的に許可されています。

---

## 🌐 コミュニティとリソース

| リソース | リンク |
| :--- | :--- |
| **公式サイト** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **他製品との比較（CCleaner、BleachBit、BCU）** | [Deltempo 比較表](https://beso1227.github.io/Deltempo/compare/) |
| **CCleaner に代わる無料の代替ツール** | [代替ツールガイド](https://beso1227.github.io/Deltempo/alternatives/) |
| **最新リリース** | [GitHub Releases](https://github.com/Beso1227/Deltempo/releases) |
| **アーキテクチャ仕様** | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| **脅威モデルとセキュリティ** | [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) |
| **テストガイド** | [docs/TESTING.md](docs/TESTING.md) |
| **コントリビューションガイド** | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **行動規範** | [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) |
| **セキュリティポリシー** | [SECURITY.md](SECURITY.md) |
| **バグ報告と Issue** | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
