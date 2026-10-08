<div align="center">

  <a href="https://beso1227.github.io/Deltempo/">
    <img src="docs/social-preview.png" alt="Deltempo — чистый и точный очистщик Windows и оптимизатор памяти" width="100%" />
  </a>

  <br />

  # Deltempo: открытый очистщик Windows, деинсталлятор приложений и оптимизатор памяти

  <p align="center">
    <strong>README:</strong>
    <a href="README.md">English</a> ·
    <a href="README.es.md">Español</a> ·
    <a href="README.zh-CN.md">简体中文</a> ·
    <a href="README.hi.md">हिन्दी</a> ·
    <a href="README.fr.md">Français</a> ·
    <a href="README.pt-BR.md">Português</a> ·
    <a href="README.ar.md">العربية</a> ·
    <b>Русский</b> ·
    <a href="README.ja.md">日本語</a>
  </p>

  <p><strong>Бесплатный очистщик диска и оптимизатор ОЗУ с открытым исходным кодом для Windows 10 &amp; 11. Возвращайте 10–40+ ГБ: временные файлы, мусор из AppData и кэши шейдеров GPU — плюс глубокий деинсталлятор приложений и поиск дубликатов файлов. Нулевая телеметрия, без рекламы, без установщика.</strong></p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48"><img alt="Скачать последнюю версию Deltempo для Windows" src="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://beso1227.github.io/Deltempo/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48"><img alt="Посетить официальный сайт Deltempo" src="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/deltempo_cli.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48"><img alt="Скачать консольный CLI Deltempo без интерфейса" src="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48"><img alt="Поставить звезду Deltempo на GitHub" src="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48" height="48" /></picture></a>
  </p>

  <p>
    <sub>Предпочитаете терминал? Одна строка устанавливает и проверяет последний релиз &mdash;
      <code>irm https://beso1227.github.io/Deltempo/win | Invoke-Expression</code>
      &middot; Нашли ошибку или есть идея? <a href="https://github.com/Beso1227/Deltempo/issues/new/choose"><strong>Создайте issue</strong></a> &mdash; каждое обращение читается.
      Если Deltempo вернул вам дисковое пространство, <a href="https://github.com/Beso1227/Deltempo"><strong>звезда</strong></a> действительно поможет другим его найти.</sub>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0&labelColor=16212B"><img alt="Последний релиз" src="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/actions/workflows/ci.yml"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github&labelColor=16212B"><img alt="Статус сборки CI" src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github" height="28" /></picture></a>
    <a href="docs/TESTING.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/tests-726_passed-00F2B0?style=for-the-badge&label=QUALITY&labelColor=16212B"><img alt="Тесты: 726 пройдено, 0 провалено" src="https://img.shields.io/badge/tests-726_passed-00F2B0?style=for-the-badge&label=QUALITY" height="28" /></picture></a>
    <a href="LICENSE"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github&labelColor=16212B"><img alt="Лицензия: MIT" src="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github" height="28" /></picture></a>
  </p>

  <p>
    <a href="https://dotnet.microsoft.com/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white&labelColor=16212B"><img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white" height="28" /></picture></a>
    <a href="https://learn.microsoft.com/dotnet/csharp/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white&labelColor=16212B"><img alt="C# 14" src="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white" height="28" /></picture></a>
    <a href="#at-a-glance"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows&labelColor=16212B"><img alt="Поддержка платформы" src="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY&labelColor=16212B"><img alt="Переносимый однофайловый" src="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#privacy"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY&labelColor=16212B"><img alt="Нулевая телеметрия, 100% офлайн" src="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY" height="28" /></picture></a>
    <a href="docs/THREAT_MODEL.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY&labelColor=16212B"><img alt="Защищено по STRIDE" src="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md#safety-pipeline"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY&labelColor=16212B"><img alt="Двухфазная проверенная безопасность" src="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY&labelColor=16212B"><img alt="Нативный движок памяти на ядре NT" src="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#quick-start"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP&labelColor=16212B"><img alt="Установка одной строкой" src="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github&labelColor=16212B"><img alt="Загрузки GitHub" src="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github&labelColor=16212B"><img alt="Звёзды GitHub" src="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/pulls"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING&labelColor=16212B"><img alt="PR приветствуются" src="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING" height="28" /></picture></a>
  </p>

  <p>
    <sub>
      <a href="docs/ARCHITECTURE.md">Архитектура</a> &middot;
      <a href="docs/THREAT_MODEL.md">Модель угроз</a> &middot;
      <a href="docs/TESTING.md">Руководство по тестированию</a> &middot;
      <a href="docs/BENCHMARKS.md">Бенчмарки</a> &middot;
      <a href="docs/RELEASES.md">Процесс релизов</a> &middot;
      <a href="SECURITY.md">Политика безопасности</a> &middot;
      <a href="#quick-start">Быстрый старт</a>
    </sub>
  </p>

  <br />

  <video src="docs/deltempo.mp4" poster="docs/deltempo.webp" controls preload="metadata" width="100%"></video>

</div>

---

  <a id="at-a-glance"></a>

## Общий обзор

| Свойство | Значение |
| :--- | :--- |
| **Официальный сайт** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Платформа** | Windows 10 и 11 (64-битная / x64) |
| **Версия** | v3.0.0 (стабильный релиз) |
| **Лицензия** | Открытый исходный код ([MIT](LICENSE)) |
| **Интерфейсы** | Современный настольный GUI (WPF Fluent) и консольный CLI без интерфейса |
| **Распространение** | Переносимый однофайловый исполняемый файл (автономный, установщик не требуется) |
| **Покрытие тестами** | 726 автоматических тестов (100% успешных, 0 ошибок, 0 пропущено), враждебный фаззинг файловой системы |
| **Доступность CLI** | Команда `deltempo` устанавливается автоматически при первом запуске GUI — ручная настройка не требуется |
| **Телеметрия** | Нулевая телеметрия. Операции сканирования, очистки, памяти и деинсталляции выполняются на 100% офлайн |
| **Движок безопасности** | Двухфазное планирование (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) с 5 уровнями риска и транзакционным журналированием |
| **Деинсталлятор приложений** | Массовый тихий деинсталлятор, движок BCU, очистка остатков в AppData и реестре, принудительная очистка неработающих приложений |
| **Точки восстановления** | Необязательные точки восстановления Windows перед деинсталляцией (управляет пользователь, по умолчанию отключено) |
| **Анализ служб** | Объясняет: *«Сломается ли что-то, если я это отключу?»* — с помощью 3 уровней безопасности, офлайн-эвристик и мульти-модельного ИИ |
| **Диспетчер процессов** | Список процессов в реальном времени с извлечением иконок приложений в высоком DPI и анализом потребления памяти |
| **Интеграция WinUtil** | Запуск утилиты Chris Titus Tech WinUtil (CTT) в один клик прямо из панели инструментов |
| **Движок памяти** | Нативные вызовы ядра Windows NT (`NtSetSystemInformation`, `EmptyWorkingSet`) |
| **Центр настроек** | Категоризированный центр управления с 4 вкладками (*Обновления*, *Основные*, *Память*, *Хранилище и безопасность*) |
| **Страж трея** | Нативная иконка Win32 для высокого DPI (`LoadCrispTrayIcon`) с живой телеметрией ОЗУ и ускорением в один клик |
| **Темы и доступность** | Профессиональный светлый режим Porcelain Slate для комфорта глаз, тёмный режим Obsidian и полная безопасность RTL-компоновки для арабского языка |
| **Обновления** | Криптографически проверенные (SHA-256) каналы стабильных и бета-релизов с атомарным развёртыванием |

---

## Что такое Deltempo?

**Deltempo** — современный высокопроизводительный набор средств обслуживания Windows с открытым исходным кодом, созданный для безопасного освобождения дискового пространства, глубокой деинсталляции упрямого ПО, мониторинга влияния автозагрузки на запуск системы и оптимизации системной памяти. Он очищает одноразовые кэши приложений, осиротевшие остатки установщиков, артефакты сборки и устаревшие системные журналы, не затрагивая личные документы, учётные данные браузера и критические компоненты операционной системы.

Традиционные утилиты очистки часто работают как непрозрачные «чёрные ящики», устанавливают встроенный adware или оставляют глубокие следы в реестре. Deltempo построен на **архитектуре «безопасность прежде всего»**: пути-кандидаты классифицируются по явным уровням риска, моделируются перед удалением, ограничиваются разрешёнными корнями каталогов и повторно проверяются непосредственно перед удалением для защиты от гонок файловой системы.

Помимо очистки диска, Deltempo включает низкоуровневые инструменты управления памятью ядра Windows NT для очистки списков страниц ожидания и обрезки неактивных рабочих наборов с помощью официальных системных вызовов Win32 и NT.

---

## ⚔️ Как Deltempo сравнивается с конкурентами

Большинство утилит очистки и оптимизации Windows либо встраивают коммерческий adware, либо требуют навязчивых фоновых служб, либо закрывают ключевые функции за платными подписками, либо опираются на устаревшие кодовые базы.

Deltempo полностью открыт исходный код, не содержит телеметрии, не требует установки и предоставляет сквозной набор, сочетающий точную очистку, глубокую деинсталляцию ПО, управление памятью на уровне ядра и анализ служб автозагрузки.

| Возможность / Функция | **Deltempo** | **CCleaner** | **BleachBit** | **Bulk Crap Uninstaller** | **Windows Storage Sense** |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Лицензия и кодовая база** | **MIT, открытый исходный код (C# 14 / .NET 10)** | Проприетарная / коммерческая | GPLv3, открытый исходный код (Python/GTK) | Apache 2.0, открытый исходный код (.NET) | Проприетарная (Microsoft) |
| **Телеметрия и конфиденциальность** | **Нулевая телеметрия (100% офлайн)** | ⚠️ Трекеры и сбор данных | ✅ Нулевая телеметрия | Минимальная телеметрия | Диагностическая телеметрия Windows |
| **Встраиваемый adware / допродажи** | **Отсутствуют / никогда** | ⚠️ Исторически встроенный adware и допродажи | Нет | Нет | Нет |
| **Движок памяти** | **Нативное ядро NT (`NtSetSystemInformation`)** | ⚠️ Базовый (только в платном Pro) | ❌ Нет | ❌ Нет | ❌ Нет |
| **Глубокий деинсталлятор приложений** | **Тихая массовая очистка + удаление остатков** | ⚠️ Базовый (глубокий — в платном Pro) | ❌ Нет | ✅ Комплексный | ❌ Только базовое «Установка и удаление» |
| **Необязательные точки восстановления** | **Необязательно (выбор пользователя, по умолчанию выкл.)** | ⚠️ Автоматически / платная функция | ❌ Нет | Необязательно | Ручное системное переключение |
| **Анализ служб автозагрузки** | **Да («Сломается ли что-то?» — 3 уровня вердиктов)** | ❌ Простой список переключателей вкл/выкл | ❌ Нет | Подробный список в реестре | Базовые метрики диспетчера задач |
| **Очистка остатков** | **AppData, ProgramData, реестр и ярлыки** | ⚠️ Только в платном Pro | ❌ Нет | ✅ Ручной поиск в реестре | ❌ Нет |
| **Кэши разработчика и шейдеров** | **NuGet, npm, pip, Cargo, Gradle, шейдеры GPU** | ❌ Только браузеры и Windows | ⚠️ Частично | ❌ Нет | ❌ Нет |
| **Поиск больших файлов** | **Классификация ИИ (>50 МБ, по уровню риска)** | ❌ Базовый поиск файлов | ❌ Нет | ❌ Нет | Базовая разбивка по дискам |
| **Восстановление системных файлов** | **Встроенные SFC, DISM и CHKDSK** | ❌ Отдельная платная утилита | ❌ Нет | ❌ Нет | Ручной запуск командной строки |
| **Интеграция WinUtil (Chris Titus)** | **Встроенный запуск с повышением прав в один клик** | ❌ Нет | ❌ Нет | ❌ Нет | ❌ Нет |
| **Современный интерфейс и комфорт для глаз** | **Obsidian Dark и Porcelain Slate Light (WPF)** | Устаревший / перегруженный | Устаревший интерфейс GTK2/3 | Устаревший интерфейс WinForms | Встроенные параметры Windows |
| **Полный паритет с CLI** | **Да (CLI `deltempo` с `--json` и dry-run)** | ⚠️ Ограниченные ключи команд | Базовый CLI | Базовый CLI | ❌ Нет |
| **Распространение** | **Переносимый однофайловый (68 МБ, без установщика)** | Требуется установщик и службы | Установщик или портативный zip | Требуется установщик и среда выполнения | Встроен в ОС |

---

## 🌟 Подробный разбор основных функций

### 1. 🧹 Точный очиститель хранилища с 26+ областями

Deltempo работает с одноразовыми данными в системной, разработческой и игровой среде, не затрагивая пользовательские документы, активные токены аутентификации и личные настройки:

* **Системные области**: Пользовательский Temp (`%TEMP%`), Windows Temp (`C:\Windows\Temp`), Prefetch, кэши загрузок Windows Update (`SoftwareDistribution\Download`), остатки обновления Windows (`$WINDOWS.~BT`), кэши Delivery Optimization, Windows Error Reporting (`WER`), дампы памяти, а также кэши шрифтов и миниатюр.
* **Шейдеры GPU и игр**: Кэш NVIDIA App / GeForce Experience OTA, кэш AMD Radeon Software, кэши шейдеров DirectX (`D3DSCache`), конвейеры Vulkan (`GLCache`), предварительное кэширование шейдеров Steam и веб-кэши лаунчера Epic Games.
* **Экосистема разработки**: Локальный кэш NuGet v3, кэш npm, кэш pip, кэш сборок Rust Cargo, кэши Gradle, временные снапшоты эмулятора Android Studio и кэши расширений VS Code.
* **Современные браузеры и коммуникации**: Профили Chromium (Chrome, Edge, Brave, Opera, Vivaldi, Arc) и профили Gecko (Firefox) — одноразовые каталоги кэша, при этом **сеансы входа строго сохраняются**; медиа-кэши Discord, Slack и Spotify.
* **Защитный щит (>24 ч)**: Необязательный защитный механизм, исключающий любой файл, созданный или изменённый за последние 24 часа, чтобы не конфликтовать с активными фоновыми установщиками и запущенными редакторами.
* **Защита TOCTOU**: Проверяет границы файла, атрибуты и канонические пути непосредственно перед удалением ссылки, чтобы устранить гонки.

---

### 2. 📦 Глубокий деинсталлятор приложений и очистка остатков

Забудьте о навязчивом бloatware, наполовину удалённом ПО и неудобных мастерах деинсталляции:

* **Единый реестр приложений**: Сканирует как 64-битные, так и 32-битные ветви реестра (`HKLM`, `HKCU`), а также современные пакеты Windows Store (AppX/UWP). Показывает реальный размер установки, проверку издателя, версию и даты установки.
* **Необязательная точка восстановления системы**: В отличие от других утилит, которые принудительно создают медленную (2 минуты) точку восстановления или пропускают её полностью, Deltempo предоставляет полный контроль пользователю. Отдельный переключатель решает, создавать ли контрольную точку перед деинсталляцией (**по умолчанию отключено**).
* **Тихое массовое удаление нескольких приложений**: Выберите несколько приложений и запустите несопровождаемую деинсталляцию без десятков однообразных диалогов установщика.
* **Глубокая очистка остатков**: После завершения деинсталлятора приложения сканер Deltempo находит осиротевшие остатки:
  * Ветви реестра: `HKCU\Software\<Vendor>`, `HKLM\Software\<Vendor>`, `HKLM\Software\WOW6432Node\<Vendor>`.
  * Каталоги файловой системы: `%LocalAppData%\<App>`, `%AppData%\<App>`, `%ProgramData%\<App>`, `%ProgramFiles%\<App>`.
  * Записи автозагрузки и осиротевшие ярлыки меню «Пуск».
* **Принудительная очистка повреждённого ПО**: Если деинсталлятор сломан, отсутствует или выдаёт ошибки, Deltempo принудительно очищает все связанные каталоги файловой системы и корректно снимает регистрацию его ключей реестра.

---

### 3. ⚡ Нативный оптимизатор памяти на ядре Windows NT

В отличие от потребительских «чистильщиков ОЗУ», которые просто выгружают память в файл подкачки и замедляют работу компьютера, Deltempo использует нативные, документированные системные вызовы ядра Windows NT:

* **Инвалидация списка ожидания**: Вызывает `NtSetSystemInformation` с `SystemMemoryListInformation` (класс `80`), чтобы освободить неиспользуемые закэшированные страницы из списка ожидания обратно в пул доступной памяти для ресурсоёмких задач (игры, компиляция, рендеринг).
* **Обрезка неактивных рабочих наборов**: Использует `EmptyWorkingSet` с повышенными токенами процесса (`SeProfileSingleProcessPrivilege` и `SeDebugPrivilege`), чтобы освободить заброшенные рабочие наборы неактивных фоновых процессов.
* **Щит критических процессов**: Основные компоненты Windows (`csrss.exe`, `dwm.exe`, `explorer.exe`, `lsass.exe`, `services.exe`, `smss.exe`, `svchost.exe` и Windows Defender) автоматически защищаются и никогда не обрезаются.
* **Фоновое автоускорение**: Может отслеживать нагрузку на память в фоновом режиме и автоматически запускать очистку, когда использование физического ОЗУ превышает заданный пользователем порог (например, 85%).

---

### 4. 🧠 Диспетчер автозагрузки и анализ служб

Хватит гадать, какие программы замедляют загрузку вашего компьютера:

* **«Сломается ли что-то, если я это отключу?»**: Каждое приложение автозагрузки и фоновая служба анализируются с помощью интеллектуального значка с 3 уровнями вердикта:
  * 🟢 **SafeToDisable**: Удобные лаунчеры, обновляторы игр и коммуникационные приложения, которым не нужно запускаться вместе с Windows.
  * 🟡 **CautionNeeded**: Панели управления звуком, утилиты тачпада или ПО периферии, у которых горячие клавиши или меню трея могут не работать, пока их не открыть вручную.
  * 🔴 **EssentialKeep**: Антивирусные комплекты, агенты облачной синхронизации и резервного копирования или критически важные драйверы устройств.
* **Двойной контур анализа**:
  * **Офлайн-эвристики**: Мгновенная детерминированная классификация на основе цифровых подписей, проверенных данных издателей, путей к бинарным файлам и известных баз процессов.
  * **Необязательный ИИ от нескольких провайдеров**: Подробные оперативные сводки по запросу с поддержкой OpenAI, Anthropic Claude, Google Gemini, Groq, OpenRouter и локальных офлайн-моделей (Ollama, LM Studio).
* **Настройки реестра, полностью обратимые**: Отключённые элементы безопасно хранятся в ключах реестра `Run_Deltempo_Disabled`. Любой элемент можно включить обратно одним щелчком.

---

### 5. 🔍 Инспектор больших файлов с классификацией ИИ

Узнайте, что действительно занимает место на диске:

* **Сканирование нескольких дисков**: Быстро сканируйте `C:\` или любой другой фиксированный диск в поисках файлов, превышающих настраиваемые пороги размера (>50 МБ, >100 МБ, >500 МБ, >1 ГБ).
* **Автоматическая разметка категорий**: Умно группирует находки в архивы (`.zip`, `.rar`, `.7z`), образы дисков (`.iso`, `.vhd`), диски виртуальных машин (`.vmdk`, `.vhdx`), установщики (`.msi`, `.exe`), видео- и аудиомедиа, а также устаревшие файлы журналов.
* **Оценка риска безопасности**: Каждый большой файл оценивается на безопасность до того, как вы к нему прикоснётесь, что предотвращает случайное удаление дисков гипервизора или важных игровых установок.

---

### 6. 🛠️ Восстановление системы Windows и интеграция CTT WinUtil

Диагностика и исправление повреждений операционной Windows прямо из интерфейса:

* **SFC (System File Checker)**: Выполняет `sfc /scannow` с повышенными правами для восстановления повреждённых системных файлов.
* **Обслуживание DISM**: Проверяет, сканирует и восстанавливает состояние хранилища компонентов Windows (`/Cleanup-Image /RestoreHealth`).
* **Базовый сброс WinSxS**: Очищает устаревшие версии хранилища компонентов, позволяя вернуть гигабайты после крупных обновлений Windows.
* **CHKDSK и сброс сети**: Планируйте проверку тома диска при следующей загрузке или очищайте DNS и сбрасывайте стеки Winsock одним щелчком.
* **Chris Titus Tech WinUtil (CTT)**: Встроенный запуск в один клик запускает известный набор WinUtil на PowerShell с повышенными правами для очистки от лишнего ПО, удаления телеметрии и автоматической настройки ПО через winget.

---

### 7. 📊 Диспетчер процессов в реальном времени

* **Нативное извлечение иконок в высоком DPI**: Живое извлечение чётких 32-битных иконок исполняемых файлов с помощью нативных функций Win32 `SHGetFileInfo` и `ExtractIconEx`.
* **Телеметрия памяти и PID**: Потребление памяти процессами в реальном времени, идентификатор процесса, сведения об издателе и путь к файлу.
* **Безопасное завершение**: Защищённые процедуры завершения предотвращают случайную остановку критических системных процессов Windows.

---

### 8. 🎨 Профессиональные темы для комфорта глаз и поддержка RTL на нескольких языках

Создано с дотошным вниманием к пользовательскому опыту:

* **Светлый режим для комфорта глаз**: Спокойная тема в стиле фарфора и сланца (`#F1F5F9`) в духе Fluent/macOS, полностью устраняющая нагрузку на глаза, заменяющая ярко-белые экраны и использующая контрастные акценты Ocean Azure (`#0284C7`).
* **Тёмный режим Obsidian**: Сдержанная тёмная тема «глубокого космоса» с яркими электрическими бирюзовыми акцентами, умеренным эффектом стекла (glassmorphism) и карточками с двойной рамкой.
* **Полное многоязычие**: Полные родные переводы на **английский, арабский, испанский, французский и немецкий языки**.
* **Защита RTL-компоновки и чисел**: Арабский режим включает настоящее направление справа налево (RTL), строго сохраняя при этом формат слева направо для метрик, путей и индикаторов прогресса (`0.0 MB`, `32%`, `C:\...`), чтобы числа и статистика хранилища никогда не переставлялись и не искажались.

---

### 9. 🔔 Идеальный страж системного трея

* **Настоящая иконка Win32 для высокого DPI (`LoadCrispTrayIcon`)**: Использует прямое создание иконок Win32 GDI (`CreateIconIndirect`) с 32-битной ARGB-прозрачностью, обеспечивая кристально чёткую отрисовку на экранах с масштабированием 100%, 125%, 150%, 175% и 200%+ без размытия.
* **Живая телеметрия при наведении**: Показывает использование памяти в реальном времени во всплывающей подсказке трея: `RAM: 42% (13.4 GB / 31.9 GB)`.
* **Быстрые контекстные действия**: Щелчок правой кнопкой запускает **ускорение памяти в один клик** или **быструю умную очистку** без открытия главного окна.
* **Устойчивость к сбоям Explorer**: Слушает широковещательное сообщение Windows `TaskbarCreated` и автоматически восстанавливает иконку после перезапуска `explorer.exe`.

---

## 💻 Справочник CLI и автоматизация без интерфейса

Deltempo включает высокопроизводительный, скриптуемый CLI (`deltempo_cli.exe` или команда `deltempo`), предназначенный для планировщика задач, системных администраторов и сред без интерфейса.

```powershell
# Показать цели очистки без удаления файлов (пробный прогон)
deltempo clean --dry-run

# Запустить безопасную очистку и отправить удалённые файлы в корзину Windows
deltempo clean --safe --recycle-bin

# Очистить ОЗУ списка ожидания и обрезать рабочие наборы процессов
deltempo boost

# Глубокая деинсталляция приложения без создания точки восстановления
deltempo uninstall "Google Chrome" --silent --force

# Необязательное создание точки восстановления при деинсталляции
deltempo uninstall "Epic Games Launcher" --restore-point

# Вывести телеметрию системы и состояние памяти в структурированном JSON
deltempo status --json
```

### Краткая справка по командам CLI

| Команда | Назначение | Основные ключи и параметры |
| :--- | :--- | :--- |
| `deltempo scan [filter]` | Сканировать цели в поисках одноразовых данных | `--json`, `--silent` |
| `deltempo clean [filter]` | Очистить одноразовые кэши | `--dry-run`, `--recycle-bin`, `--safe`, `--all`, `--json`, `--yes` |
| `deltempo smart-clean` | Быстрая очистка проверенных безопасных кэшей | `--dry-run`, `--json`, `--yes` |
| `deltempo deep-clean` | Автономная полная очистка (ОЗУ, DISM, области) | `--dry-run`, `--json`, `--yes` |
| `deltempo boost` | Оптимизировать системную память через ядро NT | `--all`, `--standby`, `--cache`, `--workingsets`, `--json` |
| `deltempo uninstall <app>` | Глубокая деинсталляция и очистка остатков | `--dry-run`, `--force`, `--silent`, `--restore-point`, `--json` |
| `deltempo large [path]` | Сканировать диски в поисках файлов, занимающих место | `--min <size>`, `--type <cat>`, `--safe`, `--top <n>`, `--sort <size\|date>` |
| `deltempo large inspect <file>` | Проверить уровень риска файла и вердикт безопасности | `--json` |
| `deltempo large clean` | Отправить одноразовые большие файлы в корзину | `--dry-run`, `--yes`, `--safe-only` |
| `deltempo startup [list]` | Просмотреть приложения автозагрузки и их влияние на загрузку | `--high`, `--json` |
| `deltempo startup disable <app>` | Обратимо отключить программу автозагрузки | N/A |
| `deltempo startup enable <app>` | Восстановить отключённую программу автозагрузки | N/A |
| `deltempo repair [subcommand]` | Проверка целостности Windows и восстановление обслуживания | `sfc`, `dism`, `winsxs`, `chkdsk`, `update`, `network` |
| `deltempo status` | Показать телеметрию системы и сведения о памяти | `--json` |
| `deltempo test` | Самопроверка движка (API памяти, обнаруженные области) | N/A |
| `deltempo help` | Вывести полный справочник команд | N/A |
| `deltempo update [check]` | Проверить официальные релизы или применить обновление | `check`, `--dry-run` |
| `deltempo register` | Установить / проверить саму команду `deltempo` | `--status`, `--remove` |
| `deltempo unregister` | Удалить все артефакты, созданные `register` | N/A |

---

  <a id="quick-start"></a>

## 🚀 Быстрый старт

> ### ⚡ Запуск одной строкой
>
> ```powershell
> irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
> ```
>
> Скачивает последний релиз, проверяет его SHA-256, кэширует в `%LOCALAPPDATA%\Deltempo\bin` и запускает его.
> Предпочитаете консольный бинарник без интерфейса? Замените `win` на [`win-cli`](https://beso1227.github.io/Deltempo/).
>
> ### 💻 …и CLI тоже готов
>
> При первом запуске команда `deltempo` устанавливается за вас — работают `cmd.exe`, PowerShell и <kbd>Win</kbd>+<kbd>R</kbd>.
> После этого откройте **новое** окно терминала и выполните:
>
> ```powershell
> deltempo test        # самопроверка движка
> deltempo status      # живая телеметрия диска и ОЗУ
> deltempo register --status
> ```

### Вариант 1: Переносимый отдельный исполняемый файл (рекомендуется)

1. Скачайте **`Deltempo.exe`** со страницы [последнего релиза](https://github.com/Beso1227/Deltempo/releases/latest).
2. Запустите `Deltempo.exe` напрямую (установщик не требуется, автономный однофайловый файл).
3. Нажмите **Сканировать сейчас** или **Глубокая очистка в один клик**, чтобы освободить место.

### Вариант 2: Одна строка в терминале (PowerShell)

Запустите последний релиз прямо из терминала — без браузера и установщика:

```powershell
irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
```

Загрузочный скрипт скачивает последний `Deltempo.exe`, проверяет его SHA-256 по опубликованному `checksums.sha256`, кэширует в `%LOCALAPPDATA%\Deltempo\bin` и запускает его. Для консольного CLI-бинарника используйте точку входа `win-cli`:

```powershell
irm https://beso1227.github.io/Deltempo/win-cli | Invoke-Expression
```

Если манифест не удаётся прочитать или хеш не совпадает, установщик прерывается и не запускает ничего из скачанного — проверка никогда не пропускается. Обратите внимание: `Invoke-Expression` не принимает аргументов, поэтому цель выбирается точкой входа, а не ключом `-Cli`.

### Вариант 3: Команда `deltempo`

Вам никогда не придётся настраивать это вручную. При первом запуске Deltempo создаёт
CLI-бинарник для консольной подсистемы, добавляет его в ваш `PATH`, регистрирует
псевдоним <kbd>Win</kbd>+<kbd>R</kbd> и устанавливает функцию `deltempo` в ваши профили
PowerShell — при обнаружении исправляя устаревшую регистрацию, оставшуюся от старой установки.

Две вещи, которые стоит знать:

- **Откройте новое окно терминала.** Уже открытые оболочки сохраняют `PATH`, с которым они были запущены.
- **Первый запуск офлайн?** Если консольный бинарник не удалось создать, ничего не
  регистрируется и наполовину установленная команда не остаётся. Настольное приложение
  при этом не затрагивается — при следующем успешном запуске попытка повторится.

Предпочитаете управлять этим самостоятельно? `deltempo register` выполняет то же самое по
запросу, а `deltempo unregister` удаляет все созданные артефакты. Используйте
`deltempo register --status`, чтобы увидеть, что именно установлено и на какой бинарник указывает ссылка.

> Команды, затрагивающие защищённое системное состояние (`restore-points`, этап DISM в `deep-clean`),
> возвращают понятную ошибку и ненулевой код выхода при запуске из обычного терминала без повышения прав.
> Запускайте их из оболочки администратора или используйте настольное приложение.

---

<a id="privacy"></a>

## 🔒 Гарантия конфиденциальности и безопасности

Архитектура Deltempo основана на безопасности и конфиденциальности пользователя как на непреложных принципах:

1. **Гарантия нулевой телеметрии**: Deltempo содержит **нулевую телеметрию**, аналитические библиотеки, рекламные SDK или фоновые ping-трекеры. Регулярное сканирование, очистка, оптимизация памяти и деинсталляция выполняются **на 100% офлайн**.
2. **Детерминированный конвейер безопасности**:

   ```text
   ┌────────┐     ┌────────┐     ┌─────────┐     ┌────────────┐     ┌─────────┐
   │  SCAN  │ ──► │  PLAN  │ ──► │ PROTECT │ ──► │ REVALIDATE │ ──► │  CLEAN  │
   └────────┘     └────────┘     └─────────┘     └────────────┘     └─────────┘
   ```

   Файлы-кандидаты планируются, проверяются на соответствие границам защищённых каталогов («Документы», «Рабочий стол», репозитории кода, ключи SSH, учётные данные) и повторно валидируются непосредственно перед удалением.
3. **Защита от точек повторного разбора и обхода путей**: Точки junction каталогов NTFS, символические ссылки и точки монтирования томов автоматически отклоняются, чтобы предотвратить атаки обхода за пределы целевых границ.
4. **Граница локальных дисков**: Работа ограничена исключительно локальными фиксированными дисками; удалённые сетевые общие ресурсы и UNC-пути блокируются.
5. **Криптографическая проверка релизов**: Автоматическая проверка обновлений требует HTTPS и сверяет SHA-256-дайджесты бинарников с подписанными манифестами релизов GitHub.

---

## 🛠️ Сборка из исходного кода

### Предварительные требования

* Windows 10 или 11 (64-битная / x64)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* PowerShell 7+ или Windows PowerShell 5.1

### Компиляция и тестирование

```powershell
# Клонировать репозиторий
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# Скомпилировать всё решение в конфигурации Release
dotnet build deltempo.sln -c Release

# Запустить набор автоматических тестов (726 пройденных модульных и интеграционных тестов)
dotnet test deltempo.sln -c Release

# Собрать автономные однофайловые бинарники релиза (GUI и CLI)
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

Результирующие однофайловые исполняемые файлы будут помещены в:

* `publish\Deltempo.exe` (GUI)
* `publish\deltempo_cli.exe` (CLI)

---

## 📜 Лицензия

Deltempo — свободное программное обеспечение с открытым исходным кодом, распространяемое по лицензии **[MIT](LICENSE)**.

```text
Copyright (c) 2026 Beso1227 / Deltempo Project
```

---

## ❓ Часто задаваемые вопросы

**Deltempo действительно бесплатен?**
Да. Deltempo полностью бесплатен и распространяется по MIT с открытым исходным кодом — без платных тарифов, допродаж, аккаунтов и рекламы.

**Хорошая ли Deltempo альтернатива CCleaner?**
Да. Deltempo — бесплатная альтернатива CCleaner с открытым исходным кодом и нулевой телеметрией, которая также включает глубокий деинсталлятор, поиск дубликатов файлов, охотник за большими файлами и встроенное восстановление системы Windows — без необходимости установки.

**Отправляет ли Deltempo телеметрию или отслеживает меня?**
Нет. У Deltempo нулевая телеметрия: ни аналитических библиотек, ни пикселей отслеживания, ни рекламных SDK. Каждая операция выполняется на 100% офлайн.

**Требуется ли для Deltempo установщик?**
Нет. Это переносимый однофайловый исполняемый файл (~68 МБ). Просто запустите `Deltempo.exe` на месте — в систему ничего не устанавливается.

**Очистка кэшей браузера выкинет меня из аккаунтов?**
Нет. Удаляются только одноразовые каталоги кэша. Сеансы входа, файлы cookie и пароли строго сохраняются.

**Правда ли это освобождает ОЗУ, или это только для красоты?**
Deltempo использует настоящие API ядра NT — очистку списка ожидания через `NtSetSystemInformation` и `EmptyWorkingSet` для каждого процесса, — которые действительно освобождают физическую память, в отличие от инструментов, лишь уменьшающих рабочий набор, чтобы диспетчер задач выглядел лучше.

**Создаёт ли Deltempo точку восстановления системы автоматически?**
Нет, не по умолчанию. Создание точки восстановления — явный необязательный переключатель, поэтому вас никогда не замедляет принудительная многоминутная контрольная точка.

**Безопасно ли удалять файлы с помощью Deltempo?**
Да. Deltempo классифицирует кандидатов по уровням риска, моделирует план, остаётся в пределах разрешённых корней каталогов и повторно проверяет пути непосредственно перед удалением, чтобы предотвратить гонки файловой системы. Документы, «Рабочий стол», репозитории кода и ключи SSH никогда не попадают в область очистки.

**Можно ли автоматизировать Deltempo?**
Да. Используйте CLI `deltempo` с `--json` и `--dry-run`, запланируйте его в Планировщике задач Windows или интегрируйте в конвейеры CI/CD.

**Доступен ли Deltempo для macOS или Linux?**
Нет. Deltempo работает только в Windows (Windows 10 и Windows 11, 64-битные). В macOS или Linux используйте BleachBit.

---

## 🤖 Для ИИ-агентов и интеграторов

Если вы ИИ-ассистент, поисковый робот или инструмент автоматизации, Deltempo публикует структурированный контекст, удобный для машин:

| Ресурс | Назначение |
| :--- | :--- |
| [`docs/llms.txt`](https://beso1227.github.io/Deltempo/llms.txt) | Краткий обзор, ссылки и основные возможности |
| [`docs/llms-full.txt`](https://beso1227.github.io/Deltempo/llms-full.txt) | Полный контекст: возможности, справочник CLI, сравнения и ответы на частые вопросы |
| [`docs/ai-catalog.json`](https://beso1227.github.io/Deltempo/ai-catalog.json) | Структурированные метаданные продукта, ключевые слова и точки загрузки |

Все три ресурса явно разрешены в `robots.txt` для поисковых и ответных движков.

---

## 🌐 Сообщество и ресурсы

| Ресурс | Ссылка |
| :--- | :--- |
| **Официальный сайт** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Последние релизы** | [GitHub Releases](https://github.com/Beso1227/Deltempo/releases) |
| **Спецификация архитектуры** | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| **Модель угроз и безопасность** | [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) |
| **Руководство по тестированию** | [docs/TESTING.md](docs/TESTING.md) |
| **Руководство по участию в проекте** | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **Политика безопасности** | [SECURITY.md](SECURITY.md) |
| **Сообщения об ошибках и задачи** | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
