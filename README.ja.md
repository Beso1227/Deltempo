<div align="center">

  <a href="https://beso1227.github.io/Deltempo/">
    <img src="docs/social-preview.png" alt="Deltempo - 純粋な高精度 Windows クリーナー＆メモリオプティマイザー" width="100%" />
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

  <p><strong>Windows 10 &amp; 11 向けの無料オープンソースのディスククリーナーおよび RAM オプティマイザー。テンプファイル、AppData の不要データ、GPU シェーダーキャッシュから 10〜40GB 以上の領域を回収 — さらにディープなアプリのアンインストーラーと重複ファイル検出機能も搭載。テレメトリーゼロ、広告なし、インストーラー不要。</strong></p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48"><img alt="Download the latest Deltempo for Windows" src="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://beso1227.github.io/Deltempo/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48"><img alt="Visit the Deltempo official website" src="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/deltempo_cli.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48"><img alt="Download the Deltempo headless CLI" src="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48"><img alt="Star Deltempo on GitHub" src="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48" height="48" /></picture></a>
  </p>

  <p>
    <sub>ターミナルがお好みですか？1 行で最新リリースをインストールして検証できます &mdash;
      <code>irm https://beso1227.github.io/Deltempo/win | Invoke-Expression</code>
      &middot; バグや機能のアイデアを見つけましたか？<a href="https://github.com/Beso1227/Deltempo/issues/new/choose"><strong>Issue を開いてください</strong></a> &mdash; すべてのリクエストを確認します。
      Deltempo がディスク領域を回収してくれたなら、<a href="https://github.com/Beso1227/Deltempo"><strong>star</strong></a> は他の人にも見つけてもらうための確かな助けになります。</sub>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0&labelColor=16212B"><img alt="Latest Release" src="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/actions/workflows/ci.yml"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github&labelColor=16212B"><img alt="CI Build Status" src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github" height="28" /></picture></a>
    <a href="docs/TESTING.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/tests-726_passed-00F2B0?style=for-the-badge&label=QUALITY&labelColor=16212B"><img alt="Tests: 726 passed, 0 failed" src="https://img.shields.io/badge/tests-726_passed-00F2B0?style=for-the-badge&label=QUALITY" height="28" /></picture></a>
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
| **バージョン** | v3.0.0（本番リリース） |
| **ライセンス** | オープンソース（[MIT](LICENSE)） |
| **インターフェース** | モダンなデスクトップ GUI（WPF Fluent）とヘッドレスターミナル CLI |
| **配布形態** | ポータブルな単一ファイル実行ファイル（自己完結、インストーラー不要） |
| **テストカバレッジ** | 726 件の自動テスト（合格率 100%、失敗 0、スキップ 0）、敵対的ファイルシステムファズィング |
| **CLI の利用** | `deltempo` コマンドは GUI 初回起動時に自動インストール — 手動設定は不要 |
| **テレメトリー** | テレメトリーゼロ。スキャン、クリーンアップ、メモリ、アンインストーラーの操作は 100% オフラインで実行 |
| **セーフティエンジン** | 2 段階のプランニング（`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`）、5 段階のリスクティア、トランザクションジャーナリング |
| **アプリアンインストーラー** | 一括サイレントアンインストール、BCU エンジン、残存 AppData/レジストリのトレース削除、動作不良アプリの強制ワイプ |
| **復元ポイント** | アンインストール前の任意の Windows システム復元ポイント（ユーザー制御、デフォルト無効） |
| **サービスインテリジェンス** | 3 段階の安全判定、オフラインヒューリスティクス、マルチモデル AI を用いて *"これを無効にすると問題が起きますか？"* に回答 |
| **プロセスマネージャー** | リアルタイムのプロセス一覧、アプリアイコンのライブ高 DPI 抽出、メモリ使用量の分析 |
| **WinUtil 連携** | Chris Titus Tech WinUtil（CTT）ツールをツールバーから 1 クリックで起動 |
| **メモリエンジン** | Windows NT カーネルのネイティブコール（`NtSetSystemInformation`、`EmptyWorkingSet`） |
| **設定ハブ** | カテゴリ別 4 タブのコントロールセンター（*アップデート*、*一般*、*メモリ*、*ストレージと安全性*） |
| **トレイガーディアン** | 高 DPI ネイティブ Win32 アイコン（`LoadCrispTrayIcon`）、ライブ RAM テレメトリと 1 クリック Boost |
| **テーマとアクセシビリティ** | プロ級の目気に優しい Porcelain Slate ライトモード、Obsidian ダークモード、完全なアラビア語 RTL レイアウト対応 |
| **アップデート** | SHA-256 による暗号学的検証を備えた Stable & Beta リリースチャンネル、アトミックなステージング |

---

## Deltempo とは？

**Deltempo** は、ストレージ領域を安全に回収し、しつこいソフトウェアを徹底的にアンインストールし、スタートアップによるブートへの影響を監視し、システムメモリを最適化するために設計された、モダンで高性能なオープンソースの Windows メンテナンススイートです。個人のドキュメント、ブラウザの認証情報、重要なオペレーティングシステムコンポーネントに触れることなく、使い捨て可能なアプリケーションキャッシュ、孤立したインストーラーの残骸、ビルド成果物、古いシステムログを削除します。

従来のクリーンアップツールは、不透明なブラックボックスとして動作し、バンドルされたアドウェアをインストールし、深いレジストリの残骸を残すことがよくあります。Deltempo は **安全性第一のアーキテクチャ** 上に設計されています: 候補パスは明示的なリスクティアに分類され、削除前にシミュレーションされ、許可されたディレクトリルート内に制限され、ファイルシステムの競合状態を防ぐため、削除の直前に再検証されます。

ディスククリーンアップに加えて、Deltempo には公式の Win32 および NT システムコールを通じてスタンバイページリストをフラッシュし、非アクティブなワーキングセットを切り詰める、低レベルの Windows NT カーネルメモリ管理ツールが含まれています。

---

## ⚔️ Deltempo と競合製品の比較

多くの Windows 向けクリーンアップ・最適化ツールは、商用アドウェアをバンドルするか、侵入的なバックグラウンドサービスを要求するか、主要機能を有料サブスクリプションの壁の向こうに閉じ込めるか、古いコードベースに依存しています。

Deltempo は完全にオープンソースで、テレメトリーゼロ、インストール不要。高精度クリーニング、ディープなソフトウェアのアンインストール、カーネルレベルのメモリ管理、スタートアップサービスのインテリジェンスを組み合わせたエンドツーエンドのスイートを提供します。

| 機能・対応範囲 | **Deltempo** | **CCleaner** | **BleachBit** | **Bulk Crap Uninstaller** | **Windows Storage Sense** |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **ライセンスとコードベース** | **MIT オープンソース（C# 14 / .NET 10）** | 専有ソフトウェア / 商用 | GPLv3 オープンソース（Python/GTK） | Apache 2.0 オープンソース（.NET） | 専有ソフトウェア（Microsoft） |
| **テレメトリーとプライバシー** | **テレメトリーゼロ（100% オフライン）** | ⚠️ トラッカーとデータ収集 | ✅ テレメトリーゼロ | 最小限のテレメトリー | Windows 診断テレメトリー |
| **同梱アドウェア / 追加販売** | **なし / 一切なし** | ⚠️ 過去にアドウェア同梱と追加販売の実績 | なし | なし | なし |
| **メモリエンジン** | **ネイティブ NT カーネル（`NtSetSystemInformation`）** | ⚠️ 基本機能のみ（有料 Pro のみ） | ❌ なし | ❌ なし | ❌ なし |
| **ディープアンインストール** | **サイレント一括 + 残存物の除去** | ⚠️ 基本機能のみ（深い削除は有料 Pro） | ❌ なし | ✅ 包括的 | ❌ 追加/削除プログラムのみ |
| **任意の復元ポイント** | **任意（ユーザーの選択、デフォルト無効）** | ⚠️ 自動化 / 有料機能 | ❌ なし | 任意 | 手動のシステム設定 |
| **スタートアップサービスのインテリジェンス** | **あり（「何か壊れますか？」3 段階判定）** | ❌ 単純なオン/オフ切り替えリスト | ❌ なし | 詳細なレジストリ一覧 | タスクマネージャーの基本情報 |
| **残存トレース掃除** | **AppData、ProgramData、レジストリ、ショートカット** | ⚠️ 有料 Pro のみ | ❌ なし | ✅ 手動のレジストリ検索 | ❌ なし |
| **開発者とシェーダーのキャッシュ** | **NuGet、npm、pip、Cargo、Gradle、GPU シェーダー** | ❌ ブラウザと Windows のみ | ⚠️ 部分的 | ❌ なし | ❌ なし |
| **大きなファイルの検出** | **AI 分類（>50MB、リスク分類付き）** | ❌ 基本的なファイル検索 | ❌ なし | ❌ なし | 基本的なドライブ別内訳 |
| **システムファイル修復** | **SFC、DISM、CHKDSK 統合** | ❌ 別途有料ツール | ❌ なし | ❌ なし | 手動のコマンドプロンプト |
| **WinUtil（Chris Titus）連携** | **1 クリック統合昇格ランチャー** | ❌ なし | ❌ なし | ❌ なし | ❌ なし |
| **モダン UI と目の快適性** | **Obsidian ダークと Porcelain Slate ライト（WPF）** | 古い / ガチャガチャ | 旧式 GTK2/3 インターフェース | 旧式 WinForms インターフェース | Windows 設定に内蔵 |
| **完全な CLI 対応** | **あり（`--json` とドライラン対応の `deltempo` CLI）** | ⚠️ 限られたコマンドスイッチ | 基本 CLI | 基本 CLI | ❌ なし |
| **配布形態** | **ポータブル単一ファイル（68MB、インストーラー不要）** | セットアップインストーラーとサービスが必要 | インストーラーまたはポータブル zip | インストーラーとランタイムが必要 | OS に内蔵 |

---

## 🌟 主要機能の詳細

### 1. 🧹 26 種以上のスコープに対応した高精度ストレージクリーナー

Deltempo は、システム・開発・ゲームの各環境にまたがる使い捨てデータを対象としつつ、ユーザードキュメント、アクティブな認証トークン、個人設定には一切触れません:

* **オペレーティングシステムのスコープ**: ユーザーの一時フォルダー（`%TEMP%`）、Windows 一時フォルダー（`C:\Windows\Temp`）、Prefetch、Windows Update のダウンロードキャッシュ（`SoftwareDistribution\Download`）、Windows アップグレードの残骸（`$WINDOWS.~BT`）、配信最適化キャッシュ、Windows エラーレポート（`WER`）、メモリーダンプ、フォント/サムネイルキャッシュ。
* **GPU とゲームのシェーダー**: NVIDIA App / GeForce Experience の OTA キャッシュ、AMD Radeon Software のキャッシュ、DirectX シェーダーキャッシュ（`D3DSCache`）、Vulkan パイプライン（`GLCache`）、Steam のシェーダープリキャッシュ、Epic Games ランチャーの webcache。
* **開発者エコシステム**: NuGet v3 ローカルキャッシュ、npm キャッシュ、pip キャッシュ、Rust の Cargo ターゲットキャッシュ、Gradle キャッシュ、Android Studio エミュレータの一時スナップショット、VS Code 拡張機能のキャッシュ。
* **モダンブラウザとコミュニケーションアプリ**: Chromium プロファイル（Chrome、Edge、Brave、Opera、Vivaldi、Arc）と Gecko プロファイル（Firefox）の使い捨てキャッシュディレクトリ（**ログインセッションは厳格に保持**）、および Discord、Slack、Spotify のメディアキャッシュ。
* **セーフティシールド（24 時間以内）**: 直近 24 時間に作成・変更されたファイルを除外するオプションの保護機能。バックグラウンドで進行中のインストーラーや稼働中のエディターと干渉しません。
* **TOCTOU ガード**: リンク解除の直前にファイル境界・属性・正規化パスを検証し、競合状態を排除します。

---

### 2. 📦 ディープルート対応のアプリアンインストーラーと残存物スイーパー

しつこいブロートウェア、中途半端に削除されたソフトウェア、散らかったアンインストールウィザードに別れを告げます:

* **統合アプリケーションインベントリ**: 64 ビットと 32 ビットのレジストリハブ（`HKLM`、`HKCU`）に加え、モダンな Windows ストア（AppX/UWP）パッケージをスキャン。実際のインストールサイズ、発行元の検証情報、バージョン、インストール日を表示します。
* **任意のシステム復元ポイント**: 遅い 2 分間のシステム復元ポイントを強制するか、完全にスキップするだけの他ツールと異なり、Deltempo は制御を完全にユーザーに委ねます。専用のトグルで、アンインストール前の復元チェックポイントを作成するかどうかを選択できます（**デフォルトはオフ**）。
* **サイレント一括マルチアプリ削除**: 複数のアプリケーションを選択して、延々と続くインストーラーダイアログをクリックすることなく無人アンインストールを実行します。
* **ディープルートの残存物スイープ**: アプリケーションのアンインストーラーが完了した後、Deltempo のスキャナーが孤立した残骸を狩り立てます:
  * レジストリのブランチ: `HKCU\Software\<Vendor>`、`HKLM\Software\<Vendor>`、`HKLM\Software\WOW6432Node\<Vendor>`。
  * ファイルシステムのディレクトリ: `%LocalAppData%\<App>`、`%AppData%\<App>`、`%ProgramData%\<App>`、`%ProgramFiles%\<App>`。
  * スタートアップのエントリとスタートメニューの孤立ショートカット。
* **破損ソフトウェアの強制ワイプ**: アンインストーラーが壊れている・見つからない・エラーを吐く場合、Deltempo は関連するすべてのファイルシステムディレクトリを強制的にクリーンし、レジストリキーをきれいに登録解除します。

---

### 3. ⚡ ネイティブ Windows NT カーネルメモリオプティマイザー

メモリをスワップファイルに無理に押し込んで PC を遅くするだけの一般消費者向け「RAM クリーナー」と違い、Deltempo はネイティブで文書化された Windows NT カーネルのシステムコールを利用します:

* **スタンバイリストの無効化**: `NtSetSystemInformation` を `SystemMemoryListInformation`（クラス `80`）とともに呼び出し、未使用のキャッシュ済みスタンバイメモリページをフラッシュして、高負荷タスク（ゲーム、コンパイル、レンダリング）用の利用可能プールへ返します。
* **非アクティブワーキングセットの切り詰め**: 昇格したプロセストークン（`SeProfileSingleProcessPrivilege` と `SeDebugPrivilege`）で `EmptyWorkingSet` を活用し、非アクティブなバックグラウンドプロセスが放棄したワーキングセットを解放します。
* **重要プロセスシールド**: コアな Windows コンポーネント（`csrss.exe`、`dwm.exe`、`explorer.exe`、`lsass.exe`、`services.exe`、`smss.exe`、`svchost.exe`、および Windows Defender）は自動的に保護され、決して切り詰められません。
* **バックグラウンド自動ブースト**: バックグラウンドでメモリ圧を監視し、物理 RAM 使用率がユーザー定義のしきい値（例: 85%）を超えると自動的にクリーンをトリガーできます。

---

### 4. 🧠 スタートアップマネージャーとサービスインテリジェンス

どのプログラムが PC のブート時間を遅くしているのか、もう悩む必要はありません:

* **「これを無効にすると問題が起きますか？」**: すべてのスタートアップアプリケーションとバックグラウンドサービスは、インテリジェントな 3 段階の判定バッジで分析されます:
  * 🟢 **SafeToDisable**: Windows と同時にブートする必要のない、便利なランチャー、ゲームアップデーター、コミュニケーションアプリ。
  * 🟡 **CautionNeeded**: オーディオコントロールパネル、トラックパッドユーティリティ、周辺機器ソフトウェアなど。ホットキーやトレイメニューが手動で開くまで無効になる可能性があります。
  * 🔴 **EssentialKeep**: セキュリティスイート、クラウド同期バックアップエージェント、必須のハードウェアドライバー。
* **デュアルインテリジェンスピプライン**:
  * **オフラインヒューリスティクス**: デジタル署名、検証済みベンダー ID、バイナリパス、既知のプロセスデータベースに基づく即時の決定的分類。
  * **オプションのマルチプロバイダー AI**: オンデマンドの詳細な動作要約。OpenAI、Anthropic Claude、Google Gemini、Groq、OpenRouter、およびローカルオフラインモデル（Ollama、LM Studio）に対応。
* **100% 可逆なレジストリトグル**: 無効化した項目は `Run_Deltempo_Disabled` レジストリキーに安全に保存されます。どの項目も 1 クリックで再有効化できます。

---

### 5. 🔍 AI 分類対応の大ファイルインスペクター

ディスク容量を実際に消費しているものを見極めます:

* **マルチドライブスキャン**: `C:\` または任意のセカンダリ固定ドライブを、カスタマイズ可能なサイズしきい値（>50 MB、>100 MB、>500 MB、>1 GB）を超えるファイルについて高速にスキャン。
* **自動カテゴリタグ付け**: 発見されたファイルをアーカイブ（`.zip`、`.rar`、`.7z`）、ディスクイメージ（`.iso`、`.vhd`）、仮想マシンドィスク（`.vmdk`、`.vhdx`）、インストーラー（`.msi`、`.exe`）、ビデオ/オーディオメディア、古いログファイルにインテリジェントにグループ化します。
* **安全リスクのティアリング**: 触れる前にすべての大きなファイルの安全性を評価し、ハイパーバイザーディスクや重要なゲームのインストールの誤削除を防ぎます。

---

### 6. 🛠️ Windows システム修復と CTT WinUtil 連携

インターフェースから直接 Windows オペレーティングシステムの破損を診断・修復します:

* **SFC（システムファイルチェッカー）**: 昇格したコンテキストで `sfc /scannow` を実行し、破損したシステムファイルを修復します。
* **DISM サービシング**: Windows コンポーネントストアの健全性をチェック・スキャン・復元します（`/Cleanup-Image /RestoreHealth`）。
* **WinSxS ベースリセット**: 上書きされたコンポーネントストアのバージョンをクリーンアップし、大規模な Windows アップグレード後に数 GB を回収します。
* **CHKDSK とネットワークリセット**: 次回のブート時にディスクボリュームの検証をスケジュールするか、DNS フラッシュと Winsock スタックのリセットを 1 クリックで実行します。
* **Chris Titus Tech WinUtil（CTT）**: 統合 1 クリックランチャーが、著名な昇格 PowerShell の WinUtil スイートを実行し、デバloat、テレメトリ除去、winget ソフトウェアの自動セットアップを行います。

---

### 7. 📊 リアルタイムプロセスマネージャー

* **ネイティブ高 DPI アイコン抽出**: ネイティブ Win32 の `SHGetFileInfo` と `ExtractIconEx` ルーチンによる、32 ビットの鮮明な実行ファイルアイコンのライブ抽出。
* **メモリと PID のテレメトリ**: リアルタイムのプロセスメモリ使用量、プロセス ID、発行元情報、ファイルパス。
* **安全な終了**: 保護されたキルルーチンが、重要な Windows システムプロセスの誤終了を防ぎます。

---

### 8. 🎨 プロ級の目に優しいテーマと多言語 RTL 対応

ユーザーエクスペリエンスへの妥協なきこだわりで設計されています:

* **プロ級の目に優しいライトモード**: 目の疲れを完全に取り除き、まぶしい白い画面を置き換える、心地よい Fluent/macOS 風の磁器とスレートのテーマ（`#F1F5F9`）。高コントラストの Ocean Azure（`#0284C7`）アクセントを採用。
* **Obsidian ダークモード**: 明るいエレクトリックシアンのアクセント、控えめなグラスモーフィズム、ダブルベゼルのカードを備えた、洗練された深宇宙風のダークテーマ。
* **包括的な多言語対応**: **英語、アラビア語、スペイン語、フランス語、ドイツ語**の完全なネイティブ翻訳。
* **RTL レイアウトと数値保護**: アラビア語モードでは本物の右から左（RTL）のウィンドウフローを有効にしつつ、メトリクス・パス・進捗表示（`0.0 MB`、`32%`、`C:\...`）には左から右（LTR）の書式を厳格に適用。数値やストレージ情報が逆転・化けることはありません。

---

### 9. 🔔 ピクセルパーフェクトなシステムトレイガーディアン

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
```

### CLI コマンド概要

| コマンド | 目的 | 主なフラグとオプション |
| :--- | :--- | :--- |
| `deltempo scan [filter]` | 対象をスキャンして使い捨てデータを検出 | `--json`、`--silent` |
| `deltempo clean [filter]` | 使い捨てキャッシュをクリーン | `--dry-run`、`--recycle-bin`、`--safe`、`--all`、`--json`、`--yes` |
| `deltempo smart-clean` | 検証済みの安全なキャッシュをクイックに除去 | `--dry-run`、`--json`、`--yes` |
| `deltempo deep-clean` | 自律的なフルクリーンアップ（RAM、DISM、スコープ） | `--dry-run`、`--json`、`--yes` |
| `deltempo boost` | NT カーネル経由でシステムメモリを最適化 | `--all`、`--standby`、`--cache`、`--workingsets`、`--json` |
| `deltempo uninstall <app>` | ディープルートのアンインストールと残存物の除去 | `--dry-run`、`--force`、`--silent`、`--restore-point`、`--json` |
| `deltempo large [path]` | 容量を消費するファイルをドライブからスキャン | `--min <size>`、`--type <cat>`、`--safe`、`--top <n>`、`--sort <size\|date>` |
| `deltempo large inspect <file>` | ファイルのリスクティアと安全性判定を調査 | `--json` |
| `deltempo large clean` | 使い捨ての大きなファイルをごみ箱へ | `--dry-run`、`--yes`、`--safe-only` |
| `deltempo startup [list]` | スタートアップアプリとブートへの影響を調査 | `--high`、`--json` |
| `deltempo startup disable <app>` | スタートアッププログラムを可逆的に無効化 | N/A |
| `deltempo startup enable <app>` | 無効化したスタートアッププログラムを復元 | N/A |
| `deltempo repair [subcommand]` | Windows 整合性チェックとサービシング修復 | `sfc`、`dism`、`winsxs`、`chkdsk`、`update`、`network` |
| `deltempo status` | システムテレメトリとメモリ情報を表示 | `--json` |
| `deltempo test` | エンジンのセルフチェック（メモリ API、検出済みスコープ） | N/A |
| `deltempo help` | コマンドリファレンス全体を出力 | N/A |
| `deltempo update [check]` | 公式リリースの確認またはアップデートの適用 | `check`、`--dry-run` |
| `deltempo register` | `deltempo` コマンド自体のインストール/調査 | `--status`、`--remove` |
| `deltempo unregister` | `register` が作成したすべての成果物を除去 | N/A |

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
3. **今すぐスキャン** または **1 クリックディープクリーン** をクリックして領域を回収します。

### オプション 2: ターミナルのワンライナー（PowerShell）

ブラウザもインストーラーも不要で、ターミナルから直接最新リリースを起動します:

```powershell
irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
```

ブートストラップスクリプトは最新の `Deltempo.exe` をダウンロードし、公開されている `checksums.sha256` に対して SHA-256 を検証し、`%LOCALAPPDATA%\Deltempo\bin` にキャッシュして起動します。ヘッドレス CLI バイナリの場合は `win-cli` エントリポイントを使用してください:

```powershell
irm https://beso1227.github.io/Deltempo/win-cli | Invoke-Expression
```

マニフェストが読み取れない場合、またはハッシュが一致しない場合、インストーラーは中止し、ダウンロードしたものを何も実行しません — 検証が省略されることはありません。なお、`Invoke-Expression` は引数を受け取らないため、ターゲットは `-Cli` スイッチではなくエントリポイントによって選択されます。

### オプション 3: `deltempo` コマンド

これらを手動で接続する必要はまったくありません。Deltempo を初めて起動すると、コンソールサブシステムの CLI バイナリを用意し、ユーザーの `PATH` に追加し、<kbd>Win</kbd>+<kbd>R</kbd> エイリアスを登録し、PowerShell プロファイルに `deltempo` 関数をインストールします（古いインストールが残した陳腐な登録が見つかった場合は修復します）。

知っておくべきことが 2 つあります:

- **新しいターミナルウィンドウを開いてください。** 開いているシェルは起動時の `PATH` を保持したままです。
- **オフラインでの初回起動？** コンソールバイナリを用意できなかった場合、何も登録されず、中途半端にインストールされたコマンドも残りません。デスクトップアプリにはまったく影響しません — 次の成功した起動時に再試行されます。

自分で管理したいですか？`deltempo register` がオンデマンドで同じことを行い、`deltempo unregister` は作成したすべての成果物を除去します。`deltempo register --status` で何がインストールされ、どのバイナリを指しているかを正確に確認できます。

> 保護されたシステム状態に触れるコマンド（`restore-points`、`deep-clean` の DISM ステージ）は、
> 標準的な非昇格ターミナルから実行すると、明確なエラーと非ゼロの終了コードを返します。
> 管理者シェルから実行するか、デスクトップアプリをご利用ください。

---

<a id="privacy"></a>

## 🔒 プライバシーとセキュリティの保証

Deltempo は、セキュリティとユーザープライバシーを譲れない基本原則として設計されています:

1. **ゼロテレメトリの保証**: Deltempo には **テレメトリーゼロ**、アナリティクスライブラリ、広告 SDK、バックグラウンドの ping トラッカーは一切含まれません。通常のスキャン、クリーンアップ、メモリ最適化、アンインストールは **100% オフライン** で実行されます。
2. **決定的安全パイプライン**:

   ```text
   ┌────────┐     ┌────────┐     ┌─────────┐     ┌────────────┐     ┌─────────┐
   │  SCAN  │ ──► │  PLAN  │ ──► │ PROTECT │ ──► │ REVALIDATE │ ──► │  CLEAN  │
   └────────┘     └────────┘     └─────────┘     └────────────┘     └─────────┘
   ```

   候補ファイルはプランニングされ、保護されたディレクトリ境界（ドキュメント、デスクトップ、コードリポジトリ、SSH 鍵、認証情報）に対して検証され、削除の直前に再検証されます。
3. **リパースポイントとトラバーサル防御**: NTFS のディレクトリジャンクション、シンボリックリンク、ボリュームマウントポイントは自動的に拒否され、ターゲット境界外へのトラバーサル攻撃を防ぎます。
4. **ローカルドライブ境界**: ローカル固定ドライブにのみ制限されます。リモートネットワーク共有と UNC パスはブロックされます。
5. **暗号学的リリース検証**: 自動アップデートチェックは HTTPS を強制し、署名済み GitHub リリースマニフェストに対してバイナリの SHA-256 ダイジェストを検証します。

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

# 自動テストスイートを実行（ユニットテスト・統合テスト 726 件合格）
dotnet test deltempo.sln -c Release

# スタンドアロン単一ファイルのリリースバイナリをパッケージ（GUI & CLI）
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

生成された単一ファイルの実行ファイルは次の場所に出力されます:

* `publish\Deltempo.exe`（GUI）
* `publish\deltempo_cli.exe`（CLI）

---

## 📜 ライセンス

Deltempo は **[MIT ライセンス](LICENSE)** の下で提供される、無料でオープンソースのソフトウェアです。

```text
Copyright (c) 2026 Beso1227 / Deltempo Project
```

---

## ❓ よくある質問

**Deltempo は本当に無料ですか？**
はい。Deltempo は完全に無料の MIT オープンソースです — 有料プラン、追加販売、アカウント、広告は一切ありません。

**Deltempo は優れた CCleaner の代替になりますか？**
はい。Deltempo は、ディープアンインストーラー、重複ファイルファインダー、大ファイルハンター、Windows システム修復の内蔵まで備えた、無料・オープンソース・テレメトリーゼロの CCleaner 代替です — インストールも不要です。

**Deltempo はテレメトリーやトラッキングを行いますか？**
いいえ。Deltempo のテレメトリーはゼロです — アナリティクスライブラリ、トラッキングピクセル、広告 SDK はありません。すべての操作は 100% オフラインで実行されます。

**Deltempo にインストーラーは必要ですか？**
いいえ。ポータブルな単一ファイルの実行ファイル（約 68 MB）です。`Deltempo.exe` をその場で直接実行するだけです — システムに何もインストールされません。

**ブラウザのキャッシュをクリーンすると、アカウントからログアウトされますか？**
いいえ。使い捨てのキャッシュディレクトリのみが削除されます。ログインセッション、Cookie、パスワードは厳格に保持されます。

**本当に RAM を解放できるのですか、それとも見た目だけですか？**
Deltempo は本物の NT カーネル API — `NtSetSystemInformation` によるスタンバイリストのフラッシュとプロセスごとの `EmptyWorkingSet` — を使用し、実際に物理メモリを解放します。タスクマネージャーを良く見せるためにワーキングセットを縮めるだけのツールとは異なります。

**Deltempo は自動でシステム復元ポイントを作成しますか？**
いいえ、デフォルトでは作成しません。復元ポイントの作成は明示的なオプションのトグルであり、強制的な数分かかるチェックポイントで遅くなることは決してありません。

**Deltempo でファイルを削除するのは安全ですか？**
はい。Deltempo は候補をリスクティアに分類し、プランをシミュレートし、許可されたディレクトリルート内に留まり、ファイルシステムの競合状態を防ぐため削除の直前にパスを再検証します。ドキュメント、デスクトップ、コードリポジトリ、SSH 鍵が対象になることは決してありません。

**Deltempo を自動化できますか？**
はい。`--json` と `--dry-run` を備えた `deltempo` CLI を使用するか、Windows タスクスケジューラでスケジュールするか、CI/CD パイプラインに統合できます。

**Deltempo は macOS や Linux で使えますか？**
いいえ。Deltempo は Windows 専用です（Windows 10 および Windows 11、64 ビット）。macOS や Linux では BleachBit をご利用ください。

---

## 🤖 AI エージェントと連携開発者向け

AI アシスタント、クローラー、自動化ツールの場合、Deltempo は構造化された機械可読のコンテキストを公開しています:

| リソース | 目的 |
| :--- | :--- |
| [`docs/llms.txt`](https://beso1227.github.io/Deltempo/llms.txt) | 簡潔な概要、リンク、コア機能 |
| [`docs/llms-full.txt`](https://beso1227.github.io/Deltempo/llms-full.txt) | 完全なコンテキスト: 機能、CLI リファレンス、比較、FAQ |
| [`docs/ai-catalog.json`](https://beso1227.github.io/Deltempo/ai-catalog.json) | 構造化された製品メタデータ、キーワード、ダウンロードエンドポイント |

これら 3 つすべては、検索エンジンと回答エンジンに対して `robots.txt` で明示的に許可されています。

---

## 🌐 コミュニティとリソース

| リソース | リンク |
| :--- | :--- |
| **公式サイト** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **最新リリース** | [GitHub Releases](https://github.com/Beso1227/Deltempo/releases) |
| **アーキテクチャ仕様** | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| **脅威モデルとセキュリティ** | [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) |
| **テストガイド** | [docs/TESTING.md](docs/TESTING.md) |
| **コントリビューションガイド** | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **セキュリティポリシー** | [SECURITY.md](SECURITY.md) |
| **バグ報告と Issue** | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
