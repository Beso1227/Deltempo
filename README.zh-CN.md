<div align="center">

  <a href="https://beso1227.github.io/Deltempo/">
    <img src="docs/social-preview.png" alt="Deltempo —— 纯净精准的 Windows 清理器与内存优化器" width="100%" />
  </a>

  <br />

  # Deltempo：开源 Windows 清理器、应用卸载器与内存优化器

  <p align="center">
    <strong>README:</strong>
    <a href="README.md">🇬🇧 English</a> ·
    <a href="README.es.md">🇪🇸 Español</a> ·
    <b>🇨🇳 简体中文</b> ·
    <a href="README.hi.md">🇮🇳 हिन्दी</a> ·
    <a href="README.fr.md">🇫🇷 Français</a> ·
    <a href="README.pt-BR.md">🇧🇷 Português</a> ·
    <a href="README.ar.md">🇸🇦 العربية</a> ·
    <a href="README.ru.md">🇷🇺 Русский</a> ·
    <a href="README.ja.md">🇯🇵 日本語</a>
  </p>

  <p><strong>适用于 Windows 10 与 11 的免费开源磁盘清理与 RAM 优化工具。回收 10–40+ GB 的临时文件、AppData 垃圾与 GPU 着色器缓存——外加深度应用卸载器与重复文件查找器。零遥测、无广告、无需安装程序。</strong></p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48"><img alt="下载最新版 Deltempo（Windows）" src="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://beso1227.github.io/Deltempo/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48"><img alt="访问 Deltempo 官方网站" src="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/deltempo_cli.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48"><img alt="下载 Deltempo 无界面 CLI" src="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48"><img alt="在 GitHub 上为 Deltempo 点星" src="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48" height="48" /></picture></a>
  </p>

  <p>
    <sub>更喜欢终端？一行命令即可安装并校验最新版本 &mdash;
      <code>irm https://beso1227.github.io/Deltempo/win | iex</code>
      &middot; 发现了 bug 或有功能建议？<a href="https://github.com/Beso1227/Deltempo/issues/new/choose"><strong>提交 Issue</strong></a> &mdash; 每一条反馈都会被阅读。
      如果 Deltempo 帮你找回了磁盘空间，一个 <a href="https://github.com/Beso1227/Deltempo"><strong>星标</strong></a> 真的能帮助更多人发现它。</sub>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0&labelColor=16212B"><img alt="最新版本" src="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/actions/workflows/ci.yml"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github&labelColor=16212B"><img alt="CI 构建状态" src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github" height="28" /></picture></a>
    <a href="docs/TESTING.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/tests-1519_passed-00F2B0?style=for-the-badge&label=QUALITY&labelColor=16212B"><img alt="测试：1519 通过，0 失败" src="https://img.shields.io/badge/tests-1519_passed-00F2B0?style=for-the-badge&label=QUALITY" height="28" /></picture></a>
    <a href="LICENSE"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github&labelColor=16212B"><img alt="许可证：MIT" src="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github" height="28" /></picture></a>
  </p>

  <p>
    <a href="https://dotnet.microsoft.com/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white&labelColor=16212B"><img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white" height="28" /></picture></a>
    <a href="https://learn.microsoft.com/dotnet/csharp/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white&labelColor=16212B"><img alt="C# 14" src="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white" height="28" /></picture></a>
    <a href="#at-a-glance"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows&labelColor=16212B"><img alt="平台支持" src="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY&labelColor=16212B"><img alt="便携式单文件" src="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#privacy"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY&labelColor=16212B"><img alt="零遥测，100% 离线" src="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY" height="28" /></picture></a>
    <a href="docs/THREAT_MODEL.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY&labelColor=16212B"><img alt="STRIDE 加固" src="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md#safety-pipeline"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY&labelColor=16212B"><img alt="两阶段校验的安全机制" src="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY&labelColor=16212B"><img alt="NT 内核原生内存引擎" src="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#quick-start"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP&labelColor=16212B"><img alt="一行命令安装" src="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github&labelColor=16212B"><img alt="GitHub 下载量" src="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github&labelColor=16212B"><img alt="GitHub 星标" src="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/pulls"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING&labelColor=16212B"><img alt="欢迎提交 PR" src="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING" height="28" /></picture></a>
  </p>

  <p>
    <sub>
      <a href="docs/ARCHITECTURE.md">架构</a> &middot;
      <a href="docs/THREAT_MODEL.md">威胁模型</a> &middot;
      <a href="docs/TESTING.md">测试指南</a> &middot;
      <a href="docs/BENCHMARKS.md">基准测试</a> &middot;
      <a href="docs/RELEASES.md">发布工程</a> &middot;
      <a href="SECURITY.md">安全策略</a> &middot;
      <a href="#quick-start">快速开始</a>
    </sub>
  </p>

  <br />

  <video src="docs/deltempo.mp4" poster="docs/deltempo.webp" controls preload="metadata" width="100%"></video>

</div>

---

  <a id="at-a-glance"></a>

## 概览

| 属性 | 详情 |
| :--- | :--- |
| **官方网站** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **平台** | Windows 10 与 11（64 位 / x64） |
| **版本** | v3.2.0（正式发布版） |
| **许可证** | 开源（[MIT](LICENSE)） |
| **界面** | 现代桌面 GUI（WPF Fluent）与无界面终端 CLI |
| **分发方式** | 便携式单文件可执行程序（自包含，无需安装程序） |
| **测试覆盖** | 1519 项自动化测试（100% 通过率，0 失败，0 跳过），对抗性文件系统模糊测试 |
| **CLI 可用性** | `deltempo` 命令在 GUI 首次启动时自动自安装 —— 无需手动配置 |
| **遥测** | 零遥测。扫描、清理、内存与卸载操作 100% 离线执行 |
| **安全引擎** | 两阶段规划（`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`），含 5 级风险分层与事务日志 |
| **有意拒绝** | 那些看似缓存、实则承载关键状态的路径，绝不会被列为清理目标 —— MSI 源缓存、按用户安装的 .NET SDK、裸驱动厂商根目录、Cargo 的 `registry\src`、Audacity 会话恢复数据。参见 [§2.1](#21-what-deltempo-refuses-to-delete)。 |
| **AppData 占用报告** | 只读的 AppData 空间占用报告，按体积从大到小排列，可在应用内或通过 `deltempo appdata-report` 使用。它只做报告：两个入口都不存在任何删除路径。 |
| **应用卸载器** | 批量静默卸载、BCU 引擎、残留 AppData/注册表痕迹清理、对损坏应用强制擦除 |
| **还原点** | 可选的卸载前 Windows 系统还原点（由用户控制，默认关闭） |
| **服务智能分析** | 回答：*"禁用它会不会出问题？"* —— 通过 3 级安全判定、离线启发式规则与多模型 AI |
| **进程管理器** | 实时进程列表，支持高 DPI 应用图标的即时提取与内存占用分析 |
| **WinUtil 集成** | [Chris Titus Tech WinUtil](https://christitus.com/win)（CTT）的一键提权启动器，作者为 Chris Titus |
| **内存引擎** | 原生 Windows NT 内核调用（`NtSetSystemInformation`、`EmptyWorkingSet`） |
| **偏好设置中心** | 分类式 4 标签控制中心（*更新*、*常规*、*内存*、*存储与安全*） |
| **托盘守护** | 高 DPI 原生 Win32 图标（`LoadCrispTrayIcon`），带实时 RAM 遥测与一键加速 |
| **主题与无障碍** | 专业护眼的瓷白浅色模式、黑曜石深色模式，以及完整的阿拉伯语 RTL 布局安全保障 |
| **更新** | 经密码学校验（SHA-256）的稳定版与 Beta 发布通道，支持原子化暂存 |

---

## Deltempo 是什么？

**Deltempo** 是一款现代化、高性能的开源 Windows 维护套件，旨在安全地回收存储空间、彻底卸载顽固软件、监控启动项对开机的影响，并优化系统内存。它会清除可丢弃的应用缓存、孤立的安装程序残留、构建产物与过期的系统日志，同时绝不触碰个人文档、浏览器凭据或关键操作系统组件。

传统清理工具往往如同不透明的黑盒，会捆绑广告软件，或在注册表中留下深层残留。Deltempo 基于**安全优先架构**构建：候选路径会被归入明确的风险等级，删除前先进行模拟，被限制在授权目录范围内，并在移除前即时重新校验，以防范文件系统竞态条件。

除磁盘清理外，Deltempo 还包含底层 Windows NT 内核内存管理工具，可通过官方 Win32 与 NT 系统调用刷新备用页列表并修剪非活动工作集。

---

## ⚔️ Deltempo 与竞品对比

大多数 Windows 清理与优化工具要么捆绑商业广告软件，要么需要侵入式的后台服务，要么将核心功能锁在付费订阅之后，要么依赖过时的代码库。

Deltempo 完全开源、零遥测、无需安装，并提供端到端的套件，将精准清理、深度根除式软件卸载、内核级内存管理与启动服务智能分析融为一体。

| 能力 / 特性 | **Deltempo** | **CCleaner** | **BleachBit** | **Bulk Crap Uninstaller** | **Windows 存储感知** |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **许可证与代码库** | **MIT 开源（C# 14 / .NET 10）** | 专有 / 商业 | GPLv3 开源（Python/GTK） | Apache 2.0 开源（.NET） | 专有（Microsoft） |
| **遥测与隐私** | **零遥测（100% 离线）** | ⚠️ 追踪器与数据收集 | ✅ 零遥测 | 少量遥测 | Windows 诊断遥测 |
| **捆绑广告 / 推销** | **无 / 从不** | ⚠️ 历史上曾捆绑广告与推销 | 无 | 无 | 无 |
| **内存引擎** | **原生 NT 内核（`NtSetSystemInformation`）** | ⚠️ 基础（仅付费 Pro） | ❌ 无 | ❌ 无 | ❌ 无 |
| **深度应用卸载器** | **静默批量 + 残留根除** | ⚠️ 基础（深度清理需付费 Pro） | ❌ 无 | ✅ 全面 | ❌ 仅基础添加/删除 |
| **可选还原点** | **可选（用户选择，默认关闭）** | ⚠️ 自动 / 付费功能 | ❌ 无 | 可选 | 手动系统开关 |
| **启动服务智能分析** | **是（"禁用会不会出问题？" 三级判定）** | ❌ 仅提供开/关切换的列表 | ❌ 无 | 详细的注册表列表 | 基础的任务管理器指标 |
| **残留痕迹清扫器** | **AppData、ProgramData、注册表与快捷方式** | ⚠️ 仅付费 Pro | ❌ 无 | ✅ 手动注册表搜索 | ❌ 无 |
| **开发者与着色器缓存** | **NuGet、npm、pip、Cargo、Gradle、GPU 着色器** | ❌ 仅浏览器与 Windows | ⚠️ 部分支持 | ❌ 无 | ❌ 无 |
| **大文件发现** | **AI 分类（>50MB，风险分级）** | ❌ 基础文件搜索 | ❌ 无 | ❌ 无 | 基础的磁盘分布统计 |
| **系统文件修复** | **内置 SFC、DISM 与 CHKDSK** | ❌ 独立付费工具 | ❌ 无 | ❌ 无 | 手动命令提示符 |
| **WinUtil（Chris Titus）集成** | **一键提权启动**（Chris Titus 的第三方项目） | ❌ 无 | ❌ 无 | ❌ 无 | ❌ 无 |
| **现代界面与护眼体验** | **黑曜石深色与瓷白浅色（WPF）** | 过时 / 杂乱 | 老旧的 GTK2/3 界面 | 老旧的 WinForms 界面 | Windows 内置设置 |
| **完整 CLI 对等能力** | **是（`deltempo` CLI 支持 `--json` 与试运行）** | ⚠️ 有限的命令开关 | 基础 CLI | 基础 CLI | ❌ 无 |
| **分发方式** | **便携式单文件（68 MB，无需安装程序）** | 需要安装程序与后台服务 | 安装程序或便携 zip | 需要安装程序与运行时 | 内置于操作系统 |

---

## 🌟 核心功能详解

### 1. 🧹 精准 26+ 范围存储清理器

Deltempo 面向系统、开发与游戏环境中的可丢弃数据，绝不触碰用户文档、有效身份验证令牌或个人设置：

* **操作系统范围**：用户 Temp（`%TEMP%`）、Windows Temp（`C:\Windows\Temp`）、Prefetch、Windows 更新下载缓存（`SoftwareDistribution\Download`）、Windows 升级残留（`$WINDOWS.~BT`）、传递优化缓存、Windows 错误报告（`WER`）、内存转储以及字体/缩略图缓存。
* **GPU 与游戏着色器**：NVIDIA App / GeForce Experience OTA 缓存、AMD Radeon Software 缓存、DirectX 着色器缓存（`D3DSCache`）、Vulkan 管线（`GLCache`）、Steam 着色器预缓存，以及 Epic Games 启动器网页缓存。
* **开发生态**：NuGet v3 本地缓存、npm 缓存、pip 缓存、Rust Cargo target 缓存、Gradle 缓存、Android Studio 模拟器临时快照以及 VS Code 扩展缓存。
* **现代浏览器与通信工具**：Chromium 配置文件（Chrome、Edge、Brave、Opera、Vivaldi、Arc）与 Gecko 配置文件（Firefox）的可丢弃缓存目录，**登录会话被严格保留**；以及 Discord、Slack 与 Spotify 的媒体缓存。
* **安全护盾（>24 小时）**：可选的安全防护，豁免最近 24 小时内创建或修改的任何文件，以避免与正在进行的后台安装程序或运行中的编辑器冲突。
* **TOCTOU 防护**：在删除文件之前立即校验文件边界、属性与规范路径，以消除竞态条件。
* **孤立商店应用检测**：所属 MSIX 包已不再安装的 AppData 目录，依据二进制 `AppModel` 仓库证据识别 —— 解析包系列名称、按发布者 ID 匹配 —— 而不是依赖目录名启发式。检测结果以 `LowRisk` 报告，且绝不预先勾选为删除目标。

<a id="21-what-deltempo-refuses-to-delete"></a>

### 2.1 Deltempo 拒绝删除的内容

一个只知道"什么可以安全删除"的清理器是危险的，因为"临时"与"可丢弃"往往是同一个谎言。有几条路径表面上看起来与构建或下载暂存区完全一样，实际上却承载着关键状态——Deltempo 从设计上就拒绝它们：这些路径不会出现在任何范围解析器中，并且被 `ProtectionPolicy` 独立拒绝，因此没有任何设置、开关或 CLI 选项能把它们变成清理目标：

| 被拒绝的路径 | 为什么删除它会造成破坏 |
| :--- | :--- |
| `%ProgramData%\Package Cache`<br>`%WinDir%\Installer\$PatchCache$` | `msiexec` 执行**修复**与**卸载**已安装产品时所需的 MSI 源缓存。删除它们会在整台机器上永久破坏"修改/修复"以及"程序和功能"中的卸载路径。 |
| `%LOCALAPPDATA%\Microsoft\dotnet` | 这是按用户安装的 .NET SDK 与运行时的**安装根目录**（`dotnet.exe` 宿主、`sdk\`、`packs\`、`shared\`），而不是缓存。只有临时的 `%TEMP%\.net` 构建暂存区才是可丢弃的。 |
| `C:\NVIDIA`、`C:\AMD`、`C:\Intel`、`C:\Realtek` | 驱动安装程序解包时使用的裸厂商根目录。若在安装中途执行清理，会删掉安装程序尚未使用、也无法重新获取的安装负载。只有特定的暂存子目录会被列为目标。 |
| `~\.cargo\registry\src` | `cargo build --offline` 编译时依赖的**已解压** crate 源码。其同级目录 `registry\cache` 会被清理，而 `src` 不会——因为在不同时清空两者属于全有或全无的操作，本工具的按目标清理模型无法安全地表达这一点。 |
| `%LOCALAPPDATA%\Audacity\SessionData` | 未保存会话的**恢复**数据，而非暂存文件。删除它会毁掉那些从未保存过的、正在编辑中的录音。 |
| `%ProgramData%\Microsoft\Windows Defender\Definition Updates\Backup` | Defender 病毒库定义的**回滚**副本。删除它们等于剥夺了在定义更新出问题时回退的能力。 |
| `%LOCALAPPDATA%\Electronic Arts\EA Desktop\UserConfig` | EA Desktop 的应用程序设置，而不是缓存。 |

这些路径被双重拒绝：既已从范围解析器中移除，*又*被 `ProtectionPolicy` 独立拒绝。因此，解析器出现回归、用户自定义的规则包或过期的设置项都无法把它们重新引入。

该列表以代码形式强制执行，并由 `CleanerServiceTests` 覆盖 —— 每一条排除项都有一个测试断言该路径不存在，*同时*还有一条对应的断言，证明同一列表中真正的暂存文件依然存在，从而防止这项修复退化为过度宽泛的删除。

---

### 2. 📦 深度应用卸载器与残留清扫

告别顽固的预装软件、卸载到一半的残留软件，以及混乱的卸载向导：

* **统一应用清单**：同时扫描 64 位与 32 位注册表配置单元（`HKLM`、`HKCU`）以及现代 Windows 商店（AppX/UWP）包。显示实际安装大小、发布者身份验证、版本与安装日期。
* **可选系统还原点**：与其他工具强制创建耗时 2 分钟的系统还原点或干脆跳过不同，Deltempo 把完整控制权交给用户。专属开关让你决定是否创建卸载前的还原检查点（**默认关闭**）。
* **多应用静默批量移除**：选中多个应用即可触发无人值守卸载，无需在几十个重复的安装对话框中反复点击。
* **深度残留清扫**：应用自身的卸载器结束后，Deltempo 的扫描器会搜寻孤立残留：
  * 注册表分支：`HKCU\Software\<Vendor>`、`HKLM\Software\<Vendor>`、`HKLM\Software\WOW6432Node\<Vendor>`。
  * 文件系统目录：`%LocalAppData%\<App>`、`%AppData%\<App>`、`%ProgramData%\<App>`、`%ProgramFiles%\<App>`。
  * 启动项与开始菜单中的孤立快捷方式。
* **对损坏软件强制擦除**：如果卸载程序已损坏、缺失或报错，Deltempo 会强制清理所有相关文件系统目录，并彻底清除其注册表项。

---

### 3. ⚡ 原生 Windows NT 内核内存优化器

与那些只是把内存挤进交换文件、反而拖慢电脑的消费级"内存清理器"不同，Deltempo 使用的是原生且有文档记载的 Windows NT 内核系统调用：

* **备用列表刷新**：以 `SystemMemoryListInformation`（类别 `80`）调用 `NtSetSystemInformation`，将未使用的缓存备用内存页冲刷回可用内存池，供高负载任务（游戏、编译、渲染）使用。
* **非活动工作集修剪**：借助提权进程令牌（`SeProfileSingleProcessPrivilege` 与 `SeDebugPrivilege`）使用 `EmptyWorkingSet`，释放非活动后台进程遗弃的工作集。
* **关键进程护盾**：Windows 核心组件（`csrss.exe`、`dwm.exe`、`explorer.exe`、`lsass.exe`、`services.exe`、`smss.exe`、`svchost.exe` 与 Windows Defender）会被自动保护，永不修剪。
* **后台自动加速**：可在后台监控内存压力，当物理 RAM 使用率超过用户设定的阈值（如 85%）时自动触发清理。

---

### 4. 🧠 启动项管理器与服务智能分析

别再猜测是哪些程序拖慢了你的开机速度：

* **"禁用它会不会出问题？"**：每个启动项与后台服务都会获得一个智能的三级判定徽章：
  * 🟢 **SafeToDisable**：便捷型启动器、游戏更新器与无需随 Windows 一同启动的通信应用。
  * 🟡 **CautionNeeded**：音频控制面板、触控板工具或外设软件——其热键或托盘菜单可能在手动打开之前处于失效状态。
  * 🔴 **EssentialKeep**：安全套件、云同步备份代理或关键硬件驱动。
* **双通道智能管线**：
  * **离线启发式**：基于数字签名、已验证的发布者身份、二进制路径与已知进程数据库，即时给出确定性分类。
  * **可选多提供商 AI**：按需生成详细运行摘要，支持 OpenAI、Anthropic Claude、Google Gemini、Groq、OpenRouter 以及本地离线模型（Ollama、LM Studio）。
* **100% 可逆的注册表开关**：被禁用的项目会安全地保存在 `Run_Deltempo_Disabled` 注册表项中。任何项目都可以一键重新启用。

---

### 5. 🔍 AI 分类大文件检查器

弄清楚究竟是什么在占用你的磁盘空间：

* **多磁盘扫描**：快速扫描 `C:\` 或任何其他固定磁盘，查找超过可自定义的大小阈值（>50 MB、>100 MB、>500 MB、>1 GB）的文件。
* **自动分类标记**：智能地将发现的文件归入压缩包（`.zip`、`.rar`、`.7z`）、磁盘映像（`.iso`、`.vhd`）、虚拟机磁盘（`.vmdk`、`.vhdx`）、安装程序（`.msi`、`.exe`）、视频/音频媒体以及过期日志文件。
* **安全风险分级**：每个大文件在你动手之前都会经过安全评估，防止误删虚拟机磁盘或重要的游戏安装目录。

---

### 6. 🛠️ Windows 系统修复与 CTT WinUtil 集成

直接在界面中诊断并修复 Windows 操作系统损坏：

* **SFC（系统文件检查器）**：在提权上下文中执行 `sfc /scannow`，修复损坏的系统文件。
* **DISM 组件存储修复**：检查、扫描并恢复 Windows 组件存储的健康状态（`/Cleanup-Image /RestoreHealth`）。
* **WinSxS 基础重置**：清理被取代的组件存储版本，在大型 Windows 更新后找回数 GB 空间。
* **CHKDSK 与网络重置**：安排下次启动时进行磁盘卷校验，或一键刷新 DNS 并重置 Winsock 协议栈。
 * **Chris Titus Tech WinUtil（CTT）**：一键启动器会在已提权的 PowerShell 会话中打开 WinUtil，用于系统精简、移除遥测和自动配置 winget。WinUtil 是 **[Chris Titus](https://christitus.com) 的第三方工具**——它不属于 Deltempo，也不在 Deltempo 的审计范围内。请参阅 [第三方声明](THIRD-PARTY-NOTICES.md)。

---

### 7. 📊 实时进程管理器

* **原生高 DPI 图标提取**：使用原生 Win32 `SHGetFileInfo` 与 `ExtractIconEx` 函数实时提取 32 位清晰的可执行文件图标。
* **内存与 PID 遥测**：实时的进程内存占用、进程 ID、发布者信息与文件路径。
* **安全终止**：受保护的终止函数可防止意外结束关键的 Windows 系统进程。

---

### 8. 📊 AppData 占用报告与孤立商店应用检测

`%LOCALAPPDATA%` 与 `%APPDATA%` 是普通 Windows 机器上最大、也最难以理解的磁盘空间占用者，同时也是破坏性最强的错误最容易发生的地方。Deltempo 默认以只读方式对待它们。

* **只读占用报告**：按体积从大到小列出究竟是什么占用了 AppData，可通过应用内的 **AppData 报告**按钮以及 CLI 中的 `deltempo appdata-report` 获取。它只负责测量、排序和显示。两个入口都没有 `--clean`，没有 `--yes`，也没有任何删除代码路径 —— 这是刻意为之，而非顺带如此。
* **重解析点安全**：旧版 AppData 兼容联接点（`Application Data`、`Local Settings`）指回 `Local`。顺着它们遍历会重复读取同一目录树，虚增总量，因此会被跳过。在真实用户配置文件上实测：该修正将 59.5 GB 的读数校正为真实值 32.6 GB（涉及 123 个文件夹）。
* **孤立的 Microsoft 商店应用**：`StoreAppOrphanResolver` 把 AppModel 包仓库（`HKCU` 与 `HKLM` 两个配置单元）当作二进制证据读取，并将每个 `LOCALAPPDATA\Packages` 文件夹与其所属 MSIX 包匹配。文件夹名（`Name_PublisherId`）通过在最后一个下划线处切分，并按发布者 ID 加名称前缀的方式，与仓库键（`Name_Version_Arch_ResourceId_PublisherId`）进行核对。若两个配置单元都无法打开，解析器将不返回任何内容，而不是猜测。孤立项以 `LowRisk`、未选中状态呈现，而 `ProtectionPolicy` 始终是拒绝规则的唯一事实来源。

> **正确的答案可以是"什么都没有"。** 在没有卸载过任何商店应用的机器上，报告返回零个孤立项。这正是预期行为：该功能以零误报而非产出量来评判。

### 9. 🎨 专业护眼主题与多语言 RTL 支持

以对用户体验的极致关注精心打造：

* **专业护眼浅色模式**：舒缓的 Fluent/macOS 瓷白与岩灰主题（`#F1F5F9`），彻底消除视觉疲劳，取代刺眼的白色屏幕，采用高对比度的海洋湛蓝（`#0284C7`）点缀。
* **黑曜石深色模式**：流畅的深空暗色主题，配以醒目的电光青色点缀、细腻的玻璃拟态与双层边框卡片。
* **多语言支持（进行中）**：为**英语、阿拉伯语、西班牙语、法语和德语**提供语言选择器。Cleaner 工作区已完成本地化，阿拉伯语启用原生 RTL 流程；模态对话框和其他工作区尚未翻译。
* **RTL 布局与数字保护**：阿拉伯语模式启用真正的从右到左（RTL）窗口流向，同时对指标数值、路径与进度指示器（`0.0 MB`、`32%`、`C:\...`）严格强制从左到右格式，确保数字与存储统计永不被颠倒或乱码。

---

### 10. 🔔 像素级完美的系统托盘守护

* **真正的高 DPI Win32 图标（`LoadCrispTrayIcon`）**：采用直接的 Win32 GDI 图标创建（`CreateIconIndirect`）与 32 位 ARGB alpha 透明度，在 100%、125%、150%、175% 及 200%+ 缩放的显示器上都能呈现锐利清晰的渲染，绝不模糊。
* **实时悬停遥测**：在托盘工具提示中显示实时内存使用情况：`RAM: 42% (13.4 GB / 31.9 GB)`。
* **快捷上下文操作**：右键即可触发**一键内存加速**或**快速智能清理**，无需打开主窗口。
* **资源管理器容错**：监听 Windows 的 `TaskbarCreated` 广播消息，在 `explorer.exe` 重启后自动恢复图标。

---

## 💻 CLI 参考与无界面自动化

Deltempo 包含一个高性能、可脚本化的 CLI（`deltempo_cli.exe` 或 `deltempo` 命令），专为计划任务、系统管理员与无界面环境设计。

```powershell
# 预览可清理的目标而不删除任何文件（试运行）
deltempo clean --dry-run

# 执行安全清理，并将被删除的文件送入 Windows 回收站
deltempo clean --safe --recycle-bin

# 刷新备用 RAM 并修剪进程工作集
deltempo boost

# 深度卸载某个应用，且不创建还原点
deltempo uninstall "Google Chrome" --silent --force

# 卸载过程中创建可选还原点
deltempo uninstall "Epic Games Launcher" --restore-point

# 以结构化 JSON 输出系统遥测与内存健康状况
deltempo status --json

# 查看究竟是什么占用了 AppData，按体积从大到小排列（只读；无法删除）
deltempo appdata-report --top 15
```

### CLI 命令速查

| 命令 | 用途 | 主要开关与选项 |
| :--- | :--- | :--- |
| `deltempo scan [filter]` | 扫描各目标位置中的可丢弃数据 | `--json`、`--silent` |
| `deltempo clean [filter]` | 清理可丢弃的缓存 | `--dry-run`、`--recycle-bin`、`--safe`、`--all`、`--json`、`--yes` |
| `deltempo smart-clean` | 快速清除已验证安全的缓存 | `--dry-run`、`--json`、`--yes` |
| `deltempo deep-clean` | 自动执行完整清理（RAM、DISM、范围） | `--dry-run`、`--json`、`--yes` |
| `deltempo restore-points` | 检查并清除系统还原点 | `--clean`、`--all`（需要提权） |
| `deltempo appdata-report` | 只读的 AppData 占用明细报告，按体积从大到小排列 | `--json`、`--top <n>` —— **不存在任何删除类开关** |
| `deltempo duplicates [path]` | 字节级重复文件查找器（三阶段哈希） | `--delete`、`--strategy` |
| `deltempo large [path]` | 扫描磁盘中的占空间文件 | `--min <size>`、`--type <cat>`、`--safe`、`--top <n>`、`--sort <size\|date>` |
| `deltempo large inspect <file>` | 检查文件的风险等级与安全判定 | `--json` |
| `deltempo large clean` | 将可丢弃的大文件移入回收站 | `--dry-run`、`--yes`、`--safe-only` |
| `deltempo large delete <file>` | 将某一个特定文件移入回收站 | `--yes` |
| `deltempo force-delete <path>` | 清除被锁定或顽固的文件/文件夹 | `--terminate-lockers`、`--recycle`、`--dry-run`、`--retry <n>`、`--force-override` |
| `deltempo force-delete scan <path>` | 检查顽固程度、锁定持有者与安全等级 | `--json` |
| `deltempo force-delete pending` | 列出将在下次重启时删除的文件 | `--json` |
| `deltempo boost` | 通过 NT 内核优化系统内存 | `--all`、`--standby`、`--cache`、`--workingsets`、`--json` |
| `deltempo procs` | 列出内存占用超过 20 MB 的后台进程 | `--json` |
| `deltempo procs trim <pid\|name>` | 修剪单个进程的工作集 | `--json` |
| `deltempo procs kill <pid\|name>` | 终止某个失控的后台进程 | `--json` |
| `deltempo uninstall <app>` | 深度根除式卸载与残留清除 | `--dry-run`、`--force`、`--silent`、`--restore-point`、`--json` |
| `deltempo startup [list]` | 检查启动项与开机影响 | `--high`、`--json` |
| `deltempo startup disable <app>` | 可逆地禁用一个启动项 | N/A |
| `deltempo startup enable <app>` | 恢复被禁用的启动项 | N/A |
| `deltempo repair [subcommand]` | Windows 完整性检查与服务修复 | `all`、`sfc`、`dism`、`winsxs`、`chkdsk`、`scanhealth`、`update`、`network` |
| `deltempo schedule` | 管理每周的计划任务维护作业 | install / remove / status |
| `deltempo shell` | 管理资源管理器右键菜单集成 | install / remove / status |
| `deltempo status` | 显示系统遥测与内存信息 | `--json` |
| `deltempo test` | 自检引擎（内存 API、已发现的范围） | N/A |
| `deltempo help` | 打印完整命令参考 | N/A |
| `deltempo update [check]` | 检查官方发布或应用更新 | `check`、`--dry-run` |
| `deltempo register` | 安装 / 检查 `deltempo` 命令本身 | `--status`、`--remove` |
| `deltempo unregister` | 移除 `register` 创建的所有产物 | N/A |
| `deltempo kill` | 关闭正在运行的 Deltempo 后台实例 | N/A |

**全局选项**适用于各个清理类命令：`--smart` / `--safe-only`、`--recycle-bin` / `-r`、`--unsafe`（关闭 24 小时护盾）、`--dry-run` / `-d`、`--yes` / `-y`、`--json` / `-j`、`--silent` / `-s`，以及用于生成带时间戳审计日志的 `--export <file>`。

---

  <a id="quick-start"></a>

## 🚀 快速开始

> ### ⚡ 一行命令即可运行
>
> ```powershell
> irm https://beso1227.github.io/Deltempo/win | iex
> ```
>
> 下载最新版本，校验其 SHA-256，缓存到 `%LOCALAPPDATA%\Deltempo\bin`，然后启动它。
> 更想要无界面控制台二进制？把 `win` 换成 [`win-cli`](https://beso1227.github.io/Deltempo/)。
>
> ### 💻 ……CLI 也已就绪
>
> 首次启动会为你安装 `deltempo` 命令 —— `cmd.exe`、PowerShell 与 <kbd>Win</kbd>+<kbd>R</kbd> 全都可用。
> 之后请打开一个**新的**终端窗口，然后：
>
> ```powershell
> deltempo test        # 自检引擎
> deltempo status      # 实时磁盘 + RAM 遥测
> deltempo register --status
> ```

### 方式一：便携式独立可执行程序（推荐）

1. 从 [最新发布](https://github.com/Beso1227/Deltempo/releases/latest) 页面下载 **`Deltempo.exe`**。
2. 直接运行 `Deltempo.exe`（无需安装程序，自包含单文件）。
3. 点击**立即扫描**或**一键深度清理**即可回收空间。

### 方式二：终端单行命令（PowerShell）

直接从终端启动最新版本 —— 无需浏览器，无需安装程序：

```powershell
irm https://beso1227.github.io/Deltempo/win | iex
```

引导脚本会下载最新的 `Deltempo.exe`，将其 SHA-256 与已发布的 `checksums.sha256` 进行比对校验，缓存到 `%LOCALAPPDATA%\Deltempo\bin`，然后启动它。如需无界面 CLI 二进制，请使用 `win-cli` 入口：

```powershell
irm https://beso1227.github.io/Deltempo/win-cli | iex
```

如果清单无法读取，或哈希值不匹配，安装程序会中止并不运行任何已下载的内容 —— 校验绝不会被跳过。请注意 `iex` 不接受任何参数，因此目标由入口点选择，而不是通过 `-Cli` 开关。

### 方式三：`deltempo` 命令

你完全不必手动配置。首次启动 Deltempo 时，它会创建一个控制台子系统 CLI
二进制，将其加入你的用户 `PATH`，注册 <kbd>Win</kbd>+<kbd>R</kbd> 别名，并向
你的 PowerShell 配置文件中安装一个 `deltempo` 函数 —— 如果发现旧版本安装留下的
过期注册，还会将其修复。

有两点值得了解：

- **打开一个新的终端窗口。** 已打开的 shell 会保持其启动时的 `PATH`。
- **首次离线运行？** 如果无法创建控制台二进制，则不会注册任何内容，也不会留下
  装了一半的命令。桌面应用完全不受影响 ——
  下次成功启动时会自动重试。

想自己管理？`deltempo register` 可按需完成同样的操作，而
`deltempo unregister` 会移除它创建的所有产物。使用 `deltempo register --status`
可以准确查看已安装了什么以及它指向哪个二进制。

> 涉及受保护系统状态的命令（`restore-points`、`deep-clean` 的 DISM 阶段）
> 在标准非提权终端中运行时会返回清晰的错误信息与非零退出码。
> 请从管理员 shell 运行它们，或使用桌面应用。

---

<a id="privacy"></a>

## 🔒 隐私与安全保障

Deltempo 的架构将安全与用户隐私作为不可妥协的基本原则：

1. **零遥测保证**：Deltempo 不含**任何遥测**、分析库、广告 SDK 或后台 ping 追踪器。日常扫描、清理、内存优化与卸载操作 **100% 离线**运行。
2. **确定性安全管线**：

   ```text
   ┌────────┐     ┌────────┐     ┌─────────┐     ┌────────────┐     ┌─────────┐
   │  SCAN  │ ──► │  PLAN  │ ──► │ PROTECT │ ──► │ REVALIDATE │ ──► │  CLEAN  │
   └────────┘     └────────┘     └─────────┘     └────────────┘     └─────────┘
   ```

   候选文件会先经过规划，再对照受保护的目录边界（文档、桌面、代码仓库、SSH 密钥、凭据）逐一校验，并在删除前即时重新验证。
3. **重解析点与遍历防御**：NTFS 目录联接点、符号链接与卷挂载点会被自动拒绝，以防止越过目标边界的遍历攻击。
4. **本地磁盘边界**：仅限本地固定磁盘；远程网络共享与 UNC 路径被阻止。
5. **AppData 持久状态保护**：Deltempo 拒绝删除仅仅"存放在 `AppData` 之下"的应用*状态*。OneDrive 同步数据库、Office 与 Outlook 凭据、Windows 凭据管理器中的凭据、最近文件跳转列表、SSH 与 GnuPG 密钥材料，以及 `NTUSER.DAT` / `UsrClass.dat` 外壳配置单元均被拒绝。这一强制执行是**按目录而非仅仅按文件**进行的：递归的 `Directory.Delete` 会在一次系统调用中移除整棵子树，不会走逐文件防护逻辑，因此残留清除操作会先遍历查找受保护的后代目录，将该遍历限制在 5,000 个目录之内，跳过重解析点，并在超出上限时**中止操作并返回失败**。
6. **发布产物的密码学校验**：自动更新检查强制使用 HTTPS，并对照已签名的 GitHub 发布清单校验二进制的 SHA-256 摘要。

> 清单签名校验器采用 ECDSA P-256 并内嵌了一个占位密钥，它与 Authenticode 检查均尚未接入实际更新流程。而针对发布资产的 SHA-256 摘要校验*是*被强制执行的。`docs/THREAT_MODEL.md` 将这些列为已知缺口，而非已实施的缓解措施。

---

## 🛠️ 从源码构建

### 环境要求

* Windows 10 或 11（64 位 / x64）
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* PowerShell 7+ 或 Windows PowerShell 5.1

### 编译与测试

```powershell
# 克隆仓库
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# 以 Release 配置编译整个解决方案
dotnet build deltempo.sln -c Release

# 像 CI 一样校验代码风格。请始终对 deltempo.sln 运行，切勿只针对
# 某一个项目：解决方案包含 WPF 应用、Core、CLI、WinUI 外壳以及测试项目，
# 因此按单个项目检查只会静默地漏掉解决方案的其余部分。
dotnet format deltempo.sln --verify-no-changes --no-restore

# 执行自动化测试套件（1519 个通过的单元与集成测试）
# xunit.v3 运行在 Microsoft.Testing.Platform 上，.NET 10 SDK 的 `dotnet test` 无法驱动它
#（"Testing with VSTest target is no longer supported"）。请先构建一次，
# 然后直接调用测试可执行文件。
dotnet build Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release
./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category!=Benchmark"

# 打包独立的单文件发布二进制（GUI 与 CLI）
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

生成的单文件可执行程序将发布到：

* `publish\Deltempo.exe`（GUI）
* `publish\deltempo_cli.exe`（CLI）

---

## 🙏 致谢与第三方工具

Deltempo 由 Deltempo 的贡献者构建与维护。其中一项功能内置了他人的成果，该项目在此完整署名：

| 项目 | 作者 | Deltempo 对它的处理方式 |
|---|---|---|
| **[Chris Titus Tech Windows Utility (WinUtil)](https://christitus.com/win)** | **[Chris Titus](https://christitus.com)** | 系统修复中的 **CTT WinUtil** 按钮会下载该脚本，并严格按照其官方文档的说明，在一个**已提权**的新 PowerShell 窗口中运行它 |

### 请注意

- WinUtil **并非采用 MIT 许可**，也**不属于** Deltempo。Deltempo 不会重新分发它、不会为其建立镜像、不会锁定其版本、也不会校验它，而且也无法这样做。
- 由于脚本的获取与执行在同一步骤内完成，**该 URL 返回的任何内容都会以管理员权限在你的机器上运行**。如果要用它，请先阅读脚本内容：<https://christitus.com/win>
- Deltempo 的安全引擎、删除前的重新校验以及发布产物签名校验，**仅覆盖 Deltempo 自身的代码**——绝不覆盖它从未见过的代码。
- 如果 WinUtil 对你有所帮助，请直接支持 [Chris Titus](https://christitus.com)。

完整细节（包括 Deltempo 实际执行的命令）见
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## 📜 许可证

Deltempo 是自由开源软件，遵循 **[MIT 许可证](LICENSE)** 授权。

```text
Copyright (c) 2026 Deltempo Contributors
```

---

## ❓ 常见问题

**Deltempo 真的免费吗？**
是的。Deltempo 完全免费，采用 MIT 开源许可 —— 没有付费层级、没有推销、无需账号、没有广告。

**Deltempo 是好的 CCleaner 替代品吗？**
是的。Deltempo 是 CCleaner 的免费、开源、零遥测替代品，还附带深度卸载器、重复文件查找器、大文件猎手与内置 Windows 系统修复 —— 且无需安装。

**Deltempo 会发送遥测或追踪我吗？**
不会。Deltempo 零遥测：没有分析库、没有跟踪像素、没有广告 SDK。每项操作都在 100% 离线环境下运行。

**Deltempo 需要安装程序吗？**
不需要。它是一个便携式单文件可执行程序（约 68 MB）。直接在原地运行 `Deltempo.exe` 即可 —— 系统中不会安装任何东西。

**清理浏览器缓存会把我登出账号吗？**
不会。只会移除可丢弃的缓存目录。登录会话、Cookie 与密码被严格保留。

**裁剪内存真的有用吗，还是只是表面功夫？**
它确实动了内存，但**这不是**性能优化，我们也不会这样宣称。Deltempo 调用的是真实的 NT 内核 API —— 通过 `NtSetSystemInformation` 刷新备用列表，以及逐进程调用 `EmptyWorkingSet`。丢弃备用缓存确实会推高*可用*内存的数字，因为 Windows 把备用页也计入可用内存；但它并不会让机器变快。备用页正是 Windows 在不访问磁盘的前提下满足新分配请求所用的缓存，把它丢掉通常意味着下一次分配会触发缺页。裁剪工作集也是同样的取舍，只是尺度更小：内存页被换出到磁盘，下次被访问时再次缺页，这甚至可能让某个应用短暂地*更慢*。所以诚实的结论是：**被释放的内存，就是稍后还要重新读回来的内存。** 它的实际收益比"可用内存变多"要窄得多 —— 它能帮到那些确实膨胀得离谱的应用，也能在内存紧张时腾出地址空间，但它并不是一个加速按钮，Deltempo 也没有把它当成加速按钮来宣传。界面上的每一个清理目标都会直接说明它真正带来的后果，而不是只给出一个安全分数。

**Deltempo 会自动创建系统还原点吗？**
不会，默认不创建。还原点创建是一个明确的可选开关，因此你永远不会被强制的数分钟检查点拖慢。

**用 Deltempo 删除文件安全吗？**
安全。Deltempo 将候选项划入风险等级，模拟执行计划，严格限定在授权目录范围内，并在删除前即时重新校验路径，以防止文件系统竞态条件。文档、桌面、代码仓库与 SSH 密钥永远不在清理范围内。

**我可以自动化 Deltempo 吗？**
可以。使用 `deltempo` CLI 配合 `--json` 与 `--dry-run`，用 Windows 任务计划程序定时运行，或将其集成到 CI/CD 流水线中。

**Deltempo 有 macOS 或 Linux 版本吗？**
没有。Deltempo 仅支持 Windows（Windows 10 与 Windows 11，64 位）。在 macOS 或 Linux 上，请改用 BleachBit。

---

## 🤖 面向 AI 智能体与集成方

如果你是 AI 助手、爬虫或自动化工具，Deltempo 发布了结构化的机器可读上下文：

| 资源 | 用途 |
| :--- | :--- |
| [`docs/llms.txt`](https://beso1227.github.io/Deltempo/llms.txt) | 简明概述、链接与核心能力 |
| [`docs/llms-full.txt`](https://beso1227.github.io/Deltempo/llms-full.txt) | 完整上下文：能力、CLI 参考、对比与常见问题 |
| [`docs/ai-catalog.json`](https://beso1227.github.io/Deltempo/ai-catalog.json) | 结构化产品元数据、关键词与下载端点 |
| [`llms.txt`](llms.txt)（仓库根目录） | 指向上述规范上下文的仓库内简要说明 |

这三者均在 `robots.txt` 中对搜索与答案引擎公开放行。

---

## 🌐 社区与资源

| 资源 | 链接 |
| :--- | :--- |
| **官方网站** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **对比（对比 CCleaner、BleachBit、BCU）** | [Deltempo 对比](https://beso1227.github.io/Deltempo/compare/) |
| **CCleaner 最佳免费替代品** | [替代品指南](https://beso1227.github.io/Deltempo/alternatives/) |
| **最新发布** | [GitHub 发布页](https://github.com/Beso1227/Deltempo/releases) |
| **架构规范** | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| **威胁模型与安全** | [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) |
| **测试指南** | [docs/TESTING.md](docs/TESTING.md) |
| **贡献指南** | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **行为准则** | [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) |
| **安全策略** | [SECURITY.md](SECURITY.md) |
| **Bug 报告与 Issue** | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
