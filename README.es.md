<div align="center">

  <a href="https://beso1227.github.io/Deltempo/">
    <img src="docs/social-preview.png" alt="Deltempo: limpiador de precisión y optimizador de memoria para Windows" width="100%" />
  </a>

  <br />

  # Deltempo: limpiador de Windows, desinstalador de aplicaciones y optimizador de memoria de código abierto

  <p align="center">
    <strong>README:</strong>
    <a href="README.md">English</a> ·
    <b>Español</b> ·
    <a href="README.zh-CN.md">简体中文</a> ·
    <a href="README.hi.md">हिन्दी</a> ·
    <a href="README.fr.md">Français</a> ·
    <a href="README.pt-BR.md">Português</a> ·
    <a href="README.ar.md">العربية</a> ·
    <a href="README.ru.md">Русский</a> ·
    <a href="README.ja.md">日本語</a>
  </p>

  <p><strong>Limpiador de disco y optimizador de RAM gratuito y de código abierto para Windows 10 y 11. Recupera de 10 a más de 40 GB de archivos temporales, residuos de AppData y cachés de shaders de GPU — además de un desinstalador profundo y un buscador de archivos duplicados. Cero telemetría, sin anuncios, sin instalador.</strong></p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48"><img alt="Descarga la última versión de Deltempo para Windows" src="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://beso1227.github.io/Deltempo/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48"><img alt="Visita el sitio web oficial de Deltempo" src="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/deltempo_cli.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48"><img alt="Descarga la CLI de Deltempo sin interfaz" src="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48"><img alt="Marca Deltempo con una estrella en GitHub" src="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48" height="48" /></picture></a>
  </p>

  <p>
    <sub>¿Prefieres la terminal? Una sola línea instala y verifica la última versión &mdash;
      <code>irm https://beso1227.github.io/Deltempo/win | Invoke-Expression</code>
      &middot; ¿Has encontrado un error o tienes una idea de mejora? <a href="https://github.com/Beso1227/Deltempo/issues/new/choose"><strong>Abre una incidencia</strong></a> &mdash; cada petición se lee.
      Si Deltempo te ha devuelto espacio en disco, una <a href="https://github.com/Beso1227/Deltempo"><strong>estrella</strong></a> ayuda de verdad a que otros lo encuentren.</sub>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0&labelColor=16212B"><img alt="Última versión" src="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/actions/workflows/ci.yml"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github&labelColor=16212B"><img alt="Estado de compilación CI" src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github" height="28" /></picture></a>
    <a href="docs/TESTING.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/tests-726_passed-00F2B0?style=for-the-badge&label=QUALITY&labelColor=16212B"><img alt="Pruebas: 726 aprobadas, 0 fallidas" src="https://img.shields.io/badge/tests-726_passed-00F2B0?style=for-the-badge&label=QUALITY" height="28" /></picture></a>
    <a href="LICENSE"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github&labelColor=16212B"><img alt="Licencia: MIT" src="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github" height="28" /></picture></a>
  </p>

  <p>
    <a href="https://dotnet.microsoft.com/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white&labelColor=16212B"><img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white" height="28" /></picture></a>
    <a href="https://learn.microsoft.com/dotnet/csharp/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white&labelColor=16212B"><img alt="C# 14" src="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white" height="28" /></picture></a>
    <a href="#at-a-glance"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows&labelColor=16212B"><img alt="Compatibilidad de plataforma" src="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY&labelColor=16212B"><img alt="Archivo único portátil" src="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#privacy"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY&labelColor=16212B"><img alt="Cero telemetría, 100% sin conexión" src="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY" height="28" /></picture></a>
    <a href="docs/THREAT_MODEL.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY&labelColor=16212B"><img alt="Endurecido con STRIDE" src="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md#safety-pipeline"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY&labelColor=16212B"><img alt="Seguridad verificada en dos fases" src="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY&labelColor=16212B"><img alt="Motor de memoria nativo del kernel NT" src="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#quick-start"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP&labelColor=16212B"><img alt="Instalación en una línea" src="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github&labelColor=16212B"><img alt="Descargas de GitHub" src="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github&labelColor=16212B"><img alt="Estrellas de GitHub" src="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/pulls"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING&labelColor=16212B"><img alt="Se aceptan PR" src="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING" height="28" /></picture></a>
  </p>

  <p>
    <sub>
      <a href="docs/ARCHITECTURE.md">Arquitectura</a> &middot;
      <a href="docs/THREAT_MODEL.md">Modelo de amenazas</a> &middot;
      <a href="docs/TESTING.md">Guía de pruebas</a> &middot;
      <a href="docs/BENCHMARKS.md">Benchmarks</a> &middot;
      <a href="docs/RELEASES.md">Ingeniería de versiones</a> &middot;
      <a href="SECURITY.md">Política de seguridad</a> &middot;
      <a href="#quick-start">Inicio rápido</a>
    </sub>
  </p>

  <br />

  <video src="docs/deltempo.mp4" poster="docs/deltempo.webp" controls preload="metadata" width="100%"></video>

</div>

---

<a id="at-a-glance"></a>

## De un vistazo

| Propiedad | Detalle |
| :--- | :--- |
| **Sitio web oficial** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Plataforma** | Windows 10 y 11 (64 bits / x64) |
| **Versión** | v3.0.0 (Versión de producción) |
| **Licencia** | Código abierto ([MIT](LICENSE)) |
| **Interfaces** | GUI moderna de escritorio (WPF Fluent) y CLI de terminal sin interfaz |
| **Distribución** | Ejecutable portátil de archivo único (autocontenido, sin instalador) |
| **Cobertura de pruebas** | 726 pruebas automatizadas (100% de aprobación, 0 fallidas, 0 omitidas), fuzzing adversarial del sistema de archivos |
| **Disponibilidad de la CLI** | El comando `deltempo` se autoinstala en el primer inicio de la GUI — sin configuración manual |
| **Telemetría** | Cero telemetría. Las operaciones de escaneo, limpieza, memoria y desinstalación se ejecutan 100% sin conexión |
| **Motor de seguridad** | Planificación en dos fases (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) con 5 niveles de riesgo y registro transaccional |
| **Desinstalador de aplicaciones** | Desinstalador masivo silencioso, motor BCU, limpieza de residuos en AppData y Registro, y borrado forzado de aplicaciones dañadas |
| **Puntos de restauración** | Puntos de restauración del Sistema de Windows opcionales antes de desinstalar (controlados por el usuario, desactivados de forma predeterminada) |
| **Inteligencia de servicios** | Responde: *«¿Algo saldrá mal si lo desactivo?»* mediante 3 veredictos de seguridad, heurísticas sin conexión e IA multimodelo |
| **Gestor de procesos** | Listado de procesos en tiempo real con extracción de iconos de aplicaciones en alta densidad (High-DPI) y análisis del uso de memoria |
| **Integración con WinUtil** | Lanzador en 1 clic de la utilidad WinUtil (CTT) de Chris Titus Tech directamente desde la barra de herramientas |
| **Motor de memoria** | Llamadas nativas al kernel NT de Windows (`NtSetSystemInformation`, `EmptyWorkingSet`) |
| **Centro de preferencias** | Centro de control de 4 pestañas categorizado (*Actualizaciones*, *General*, *Memoria*, *Almacenamiento y seguridad*) |
| **Guardián de la bandeja** | Icono Win32 nativo de alta densidad (`LoadCrispTrayIcon`) con telemetría de RAM en vivo y Boost en 1 clic |
| **Temas y accesibilidad** | Modo claro profesional Porcelain Slate confortable para la vista, modo oscuro Obsidian y compatibilidad completa con el diseño RTL del árabe |
| **Actualizaciones** | Canales de versiones Estable y Beta verificados criptográficamente (SHA-256) con preparación atómica |

---

## ¿Qué es Deltempo?

**Deltempo** es una suite de mantenimiento de Windows moderna, de alto rendimiento y de código abierto, diseñada para recuperar espacio de almacenamiento de forma segura, desinstalar a fondo software persistente, supervisar el impacto del arranque en el inicio y optimizar la memoria del sistema. Elimina cachés de aplicaciones desechables, restos de instaladores huérfanos, artefactos de compilación y registros del sistema obsoletos sin tocar documentos personales, credenciales del navegador ni componentes críticos del sistema operativo.

Las utilidades de limpieza tradicionales suelen funcionar como cajas negras opacas, instalar adware incluido o dejar profundos residuos en el registro. Deltempo se basa en una **arquitectura con la seguridad primero**: las rutas candidatas se clasifican en niveles de riesgo explícitos, se simulan antes de eliminarlas, se confinan a las raíces de directorios autorizadas y se vuelven a validar justo antes de la eliminación para protegerse contra condiciones de carrera en el sistema de archivos.

Además de la limpieza de disco, Deltempo incluye herramientas de gestión de memoria del kernel NT de Windows de bajo nivel para vaciar las listas de páginas en espera (standby) y recortar conjuntos de trabajo inactivos mediante las llamadas oficiales de sistema Win32 y NT.

---

## ⚔️ Cómo se compara Deltempo con la competencia

La mayoría de las utilidades de limpieza y optimización de Windows incluyen adware comercial, requieren servicios de fondo invasivos, bloquean funciones esenciales tras suscripciones de pago o dependen de bases de código obsoletas.

Deltempo es completamente de código abierto, no contiene telemetría, no requiere instalación y ofrece una suite de extremo a extremo que combina limpieza precisa, desinstalación profunda de software, gestión de memoria a nivel de kernel e inteligencia de servicios de inicio.

| Capacidades / Funciones | **Deltempo** | **CCleaner** | **BleachBit** | **Bulk Crap Uninstaller** | **Windows Storage Sense** |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Licencia y código fuente** | **MIT de código abierto (C# 14 / .NET 10)** | Propietario / Comercial | GPLv3 de código abierto (Python/GTK) | Apache 2.0 de código abierto (.NET) | Propietario (Microsoft) |
| **Telemetría y privacidad** | **Cero telemetría (100% sin conexión)** | ⚠️ Rastreadores y recopilación de datos | ✅ Cero telemetría | Telemetría mínima | Telemetría de diagnóstico de Windows |
| **Adware incluido / ventas adicionales** | **Ninguno / Nunca** | ⚠️ Paquetes de adware históricos y ventas adicionales | Ninguno | Ninguno | Ninguno |
| **Motor de memoria** | **Kernel NT nativo (`NtSetSystemInformation`)** | ⚠️ Básico (solo en la versión Pro de pago) | ❌ Ninguno | ❌ Ninguno | ❌ Ninguno |
| **Desinstalador profundo de aplicaciones** | **Masivo silencioso + erradicación de residuos** | ⚠️ Básico (la versión Pro de pago permite la limpieza profunda) | ❌ Ninguno | ✅ Completo | ❌ Solo «Quitar programas» básico |
| **Puntos de restauración opcionales** | **Opcional (elección del usuario, desactivado de forma predeterminada)** | ⚠️ Función automática / de pago | ❌ Ninguno | Opcional | Interruptor manual del sistema |
| **Inteligencia de servicios de inicio** | **Sí («¿Se romperá algo?» con veredictos de 3 niveles)** | ❌ Simple lista de interruptores de activado/desactivado | ❌ Ninguno | Lista detallada del registro | Métricas básicas del Administrador de tareas |
| **Eliminador de residuos** | **AppData, ProgramData, Registro y accesos directos** | ⚠️ Solo en la versión Pro de pago | ❌ Ninguno | ✅ Búsqueda manual en el registro | ❌ Ninguno |
| **Cachés de desarrollo y shaders** | **NuGet, npm, pip, Cargo, Gradle, shaders de GPU** | ❌ Solo navegador y Windows | ⚠️ Parcial | ❌ Ninguno | ❌ Ninguno |
| **Descubrimiento de archivos grandes** | **Categorizado por IA (>50 MB, clasificado por riesgo)** | ❌ Búsqueda básica de archivos | ❌ Ninguno | ❌ Ninguno | Desglose básico de unidades |
| **Reparación de archivos del sistema** | **SFC, DISM y CHKDSK integrados** | ❌ Utilidad independiente de pago | ❌ Ninguno | ❌ Ninguno | Símbolo del sistema manual |
| **Integración con WinUtil (Chris Titus)** | **Lanzador integrado con privilegios elevados en 1 clic** | ❌ Ninguno | ❌ Ninguno | ❌ Ninguno | ❌ Ninguno |
| **Interfaz moderna y confort visual** | **Obsidian oscuro y Porcelain Slate claro (WPF)** | Obsoleto / Sobrecargado | Interfaz heredada de GTK2/3 | Interfaz heredada de WinForms | Configuración integrada de Windows |
| **Paridad total con la CLI** | **Sí (CLI de `deltempo` con `--json` y simulación)** | ⚠️ Interruptores de comando limitados | CLI básica | CLI básica | ❌ Ninguno |
| **Distribución** | **Archivo único portátil (68 MB, sin instalador)** | Requiere instalador y servicios | Instalador o ZIP portátil | Requiere instalador y entorno de ejecución | Integrado en el SO |

---

## 🌟 Detalle de las funciones principales

### 1. 🧹 Limpiador de almacenamiento preciso con más de 26 ámbitos

Deltempo ataca datos desechables en entornos de sistema, desarrollo y juegos sin tocar documentos del usuario, tokens de autenticación activos ni configuraciones personales:

* **Ámbitos del sistema operativo**: Temporal de usuario (`%TEMP%`), Temporal de Windows (`C:\Windows\Temp`), Prefetch, cachés de descarga de Windows Update (`SoftwareDistribution\Download`), restos de la actualización de Windows (`$WINDOWS.~BT`), cachés de Optimización de entrega, Informe de errores de Windows (`WER`), volcados de memoria y cachés de fuentes/miniaturas.
* **Shaders de GPU y juegos**: Caché OTA de NVIDIA App / GeForce Experience, caché de AMD Radeon Software, cachés de shaders de DirectX (`D3DSCache`), pipelines de Vulkan (`GLCache`), pre-caché de shaders de Steam y webcaches del lanzador de Epic Games.
* **Ecosistema de desarrollo**: Caché local de NuGet v3, caché de npm, caché de pip, caché de destino de Cargo (Rust), cachés de Gradle, instantáneas temporales del emulador de Android Studio y cachés de extensiones de VS Code.
* **Navegadores modernos y comunicación**: Directorios de caché desechables de perfiles Chromium (Chrome, Edge, Brave, Opera, Vivaldi, Arc) y de perfiles Gecko (Firefox), con **las sesiones de inicio de sesión estrictamente preservadas**; cachés de medios de Discord, Slack y Spotify.
* **Escudo de seguridad (>24 h)**: Guarda de seguridad opcional que exime cualquier archivo creado o modificado en las últimas 24 horas para evitar interferir con instaladores de fondo activos o editores en ejecución.
* **Protección TOCTOU**: Verifica los límites, los atributos y las rutas canónicas de los archivos justo antes de eliminarlos para eliminar las condiciones de carrera.

---

### 2. 📦 Desinstalador profundo de aplicaciones y eliminador de residuos

Olvídate del bloatware persistente, del software a medio desinstalar y de los asistentes de desinstalación desordenados:

* **Inventario unificado de aplicaciones**: Analiza los hives de registro de 64 y 32 bits (`HKLM`, `HKCU`) junto con los paquetes modernos de Windows Store (AppX/UWP). Muestra el tamaño real de instalación, la verificación del editor, la versión y las fechas de instalación.
* **Punto de restauración del sistema opcional**: A diferencia de otras utilidades que imponen un lento punto de restauración de 2 minutos o lo omiten por completo, Deltempo entrega el control total al usuario. Un interruptor dedicado te permite decidir si crear un punto de control de restauración antes de desinstalar (**desactivado de forma predeterminada**).
* **Eliminación masiva silenciosa de varias aplicaciones**: Selecciona varias aplicaciones e inicia la desinstalación sin supervisión sin tener que pasar por decenas de diálogos repetitivos del instalador.
* **Barrido profundo de residuos**: Cuando el desinstalador de una aplicación termina, el explorador de Deltempo busca restos huérfanos:
  * Ramas del registro: `HKCU\Software\<Vendor>`, `HKLM\Software\<Vendor>`, `HKLM\Software\WOW6432Node\<Vendor>`.
  * Directorios del sistema de archivos: `%LocalAppData%\<App>`, `%AppData%\<App>`, `%ProgramData%\<App>`, `%ProgramFiles%\<App>`.
  * Entradas de inicio y accesos directos huérfanos del menú Inicio.
* **Borrado forzado de software dañado**: Si un desinstalador está dañado, ausente o devuelve errores, Deltempo limpia a la fuerza todos los directorios relacionados y da de baja sus claves de registro de forma limpia.

---

### 3. ⚡ Optimizador de memoria nativo del kernel NT de Windows

A diferencia de los «limpiadores de RAM» para consumidores que simplemente fuerzan la memoria al archivo de intercambio y ralentizan tu PC, Deltempo utiliza llamadas nativas y documentadas al kernel NT de Windows:

* **Invalidación de la lista en espera**: Invoca `NtSetSystemInformation` con `SystemMemoryListInformation` (clase `80`) para devolver al grupo disponible las páginas de memoria en caché sin uso, de modo que puedan atender tareas de alta demanda (juegos, compilaciones, renderizado).
* **Recorte del conjunto de trabajo inactivo**: Aprovecha `EmptyWorkingSet` con tokens de proceso elevados (`SeProfileSingleProcessPrivilege` y `SeDebugPrivilege`) para liberar conjuntos de trabajo abandonados de procesos de fondo inactivos.
* **Escudo de procesos críticos**: Los componentes centrales de Windows (`csrss.exe`, `dwm.exe`, `explorer.exe`, `lsass.exe`, `services.exe`, `smss.exe`, `svchost.exe` y Windows Defender) se protegen automáticamente y nunca se recortan.
* **Auto-boost en segundo plano**: Puede supervisar la presión de memoria en segundo plano y activar automáticamente una limpieza cuando el uso de la RAM física supere un umbral definido por el usuario (p. ej., 85%).

---

### 4. 🧠 Gestor de inicio e inteligencia de servicios

Deja de preguntarte qué programas ralentizan el arranque de tu PC:

* **«¿Algo saldrá mal si lo desactivo?»**: Cada aplicación de inicio y servicio de fondo se analiza con una insignia de veredicto inteligente de 3 niveles:
  * 🟢 **SafeToDisable**: Lanzadores de conveniencia, actualizadores de juegos y aplicaciones de comunicación que no necesitan arrancar con Windows.
  * 🟡 **CautionNeeded**: Paneles de control de audio, utilidades de panel táctil o software de periféricos donde los atajos de teclado o los menús de bandeja podrían quedar inactivos hasta abrirlos manualmente.
  * 🔴 **EssentialKeep**: Suites de seguridad, agentes de sincronización y copia de seguridad en la nube o controladores de hardware esenciales.
* **Doble canal de inteligencia**:
  * **Heurísticas sin conexión**: Clasificación determinista instantánea basada en firmas digitales, identidades verificadas del editor, rutas de binarios y bases de datos de procesos conocidos.
  * **IA multiproveedor opcional**: Resúmenes operativos detallados bajo demanda con soporte de OpenAI, Anthropic Claude, Google Gemini, Groq, OpenRouter y modelos locales sin conexión (Ollama, LM Studio).
* **Interruptores del registro 100% reversibles**: Los elementos desactivados se guardan de forma segura en las claves de registro `Run_Deltempo_Disabled`. Cualquier elemento puede reactivarse con un solo clic.

---

### 5. 🔍 Inspector de archivos grandes categorizado por IA

Descubre qué está consumiendo realmente tu espacio en disco:

* **Escaneo de múltiples unidades**: Escanea rápidamente `C:\` o cualquier otra unidad fija secundaria en busca de archivos que superen umbrales de tamaño personalizables (>50 MB, >100 MB, >500 MB, >1 GB).
* **Etiquetado automático de categorías**: Agrupa inteligentemente los hallazgos en Archivos comprimidos (`.zip`, `.rar`, `.7z`), Imágenes de disco (`.iso`, `.vhd`), Discos de máquinas virtuales (`.vmdk`, `.vhdx`), Instaladores (`.msi`, `.exe`), medios de vídeo/audio y archivos de registro obsoletos.
* **Clasificación de riesgo por seguridad**: Cada archivo grande se evalúa por seguridad antes de que lo toques, evitando la eliminación accidental de discos de hipervisor o instalaciones de juegos importantes.

---

### 6. 🛠️ Reparación del sistema Windows e integración con CTT WinUtil

Diagnostica y repara la corrupción del sistema operativo Windows directamente desde la interfaz:

* **SFC (comprobador de archivos del sistema)**: Ejecuta `sfc /scannow` en un contexto elevado para reparar archivos del sistema dañados.
* **Mantenimiento de DISM**: Comprueba, analiza y restaura la salud del almacén de componentes de Windows (`/Cleanup-Image /RestoreHealth`).
* **Restablecimiento base de WinSxS**: Limpia las versiones obsoletas del almacén de componentes para recuperar gigabytes tras las actualizaciones importantes de Windows.
* **CHKDSK y restablecimiento de red**: Programa la verificación del volumen de disco en el siguiente arranque o vacía el DNS y restablece las pilas de Winsock con un clic.
* **Chris Titus Tech WinUtil (CTT)**: El lanzador integrado en 1 clic ejecuta la conocida suite WinUtil de PowerShell con privilegios elevados de Chris Titus Tech para eliminar bloatware, quitar telemetría y configurar software con winget de forma automática.

---

### 7. 📊 Gestor de procesos en tiempo real

* **Extracción nativa de iconos en alta densidad**: Extracción en vivo de iconos nítidos de 32 bits de los ejecutables mediante las rutinas nativas de Win32 `SHGetFileInfo` y `ExtractIconEx`.
* **Telemetría de memoria y PID**: Uso de memoria de los procesos en tiempo real, ID de proceso, información del editor y ruta del archivo.
* **Terminación segura**: Las rutinas de finalización protegidas evitan la terminación accidental de procesos críticos del sistema Windows.

---

### 8. 🎨 Temas profesionales de confort visual y compatibilidad multilingüe RTL

Diseñado con una atención obsesiva a la experiencia de usuario:

* **Modo claro profesional confortable para la vista**: Un tema reconfortante de porcelana y pizarra estilo Fluent/macOS (`#F1F5F9`) que elimina por completo la fatiga visual, sustituye las pantallas blancas deslumbrantes y usa acentos de alto contraste Ocean Azure (`#0284C7`).
* **Modo oscuro Obsidian**: Tema oscuro elegante de espacio profundo con vibrantes acentos cian eléctrico, un sutil efecto glassmorphism y tarjetas de doble bisel.
* **Cobertura multilingüe completa**: Traducciones nativas completas en **inglés, árabe, español, francés y alemán**.
* **Diseño RTL y protección numérica**: El modo árabe activa un flujo de ventana real de derecha a izquierda (RTL) mientras aplica estrictamente el formato de izquierda a derecha en métricas, rutas e indicadores de progreso (`0.0 MB`, `32%`, `C:\...`) para que las cifras y las estadísticas de almacenamiento nunca se inviertan ni se corrompan.

---

### 9. 🔔 Guardián pixel perfecto de la bandeja del sistema

* **Icono Win32 nativo de alta densidad (`LoadCrispTrayIcon`)**: Emplea la creación directa de iconos GDI de Win32 (`CreateIconIndirect`) con transparencia alfa ARGB de 32 bits, ofreciendo un renderizado nítido en pantallas con escalado del 100%, 125%, 150%, 175% y más del 200% sin desenfoque.
* **Telemetría en vivo al pasar el cursor**: Muestra el uso de memoria en tiempo real en la descripción de la bandeja: `RAM: 42% (13.4 GB / 31.9 GB)`.
* **Acciones rápidas de contexto**: Clic derecho para activar **Boost de memoria en 1 clic** o **Limpieza inteligente rápida** sin abrir la ventana principal.
* **Resiliencia ante Explorer**: Escucha el mensaje de difusión `TaskbarCreated` de Windows para restaurar automáticamente el icono si `explorer.exe` se reinicia.

---

## 💻 Referencia de la CLI y automatización sin interfaz

Deltempo incluye una CLI de alto rendimiento y scriptable (`deltempo_cli.exe` o el comando `deltempo`) diseñada para tareas programadas, administradores de sistemas y entornos sin interfaz.

```powershell
# Previsualiza los objetivos limpiables sin borrar archivos (simulación)
deltempo clean --dry-run

# Ejecuta una limpieza segura y envía los archivos eliminados a la Papelera de reciclaje de Windows
deltempo clean --safe --recycle-bin

# Vuelca la RAM en espera y recorta los conjuntos de trabajo de los procesos
deltempo boost

# Desinstalación profunda de una aplicación sin crear un punto de restauración
deltempo uninstall "Google Chrome" --silent --force

# Creación opcional de un punto de restauración durante la desinstalación
deltempo uninstall "Epic Games Launcher" --restore-point

# Muestra la telemetría del sistema y el estado de la memoria en JSON estructurado
deltempo status --json
```

### Resumen de comandos de la CLI

| Comando | Propósito | Indicadores y opciones clave |
| :--- | :--- | :--- |
| `deltempo scan [filter]` | Analiza objetivos en busca de datos desechables | `--json`, `--silent` |
| `deltempo clean [filter]` | Limpia cachés desechables | `--dry-run`, `--recycle-bin`, `--safe`, `--all`, `--json`, `--yes` |
| `deltempo smart-clean` | Purga rápida de cachés verificados como seguros | `--dry-run`, `--json`, `--yes` |
| `deltempo deep-clean` | Limpieza completa autónoma (RAM, DISM, ámbitos) | `--dry-run`, `--json`, `--yes` |
| `deltempo boost` | Optimiza la memoria del sistema mediante el kernel NT | `--all`, `--standby`, `--cache`, `--workingsets`, `--json` |
| `deltempo uninstall <app>` | Desinstalación profunda y purga de residuos | `--dry-run`, `--force`, `--silent`, `--restore-point`, `--json` |
| `deltempo large [path]` | Busca en las unidades archivos que consumen espacio | `--min <size>`, `--type <cat>`, `--safe`, `--top <n>`, `--sort <size\|date>` |
| `deltempo large inspect <file>` | Inspecciona el nivel de riesgo y el veredicto de seguridad de un archivo | `--json` |
| `deltempo large clean` | Envía a la papelera los archivos grandes desechables | `--dry-run`, `--yes`, `--safe-only` |
| `deltempo startup [list]` | Analiza las aplicaciones de inicio y su impacto en el arranque | `--high`, `--json` |
| `deltempo startup disable <app>` | Desactiva de forma reversible un programa de inicio | N/A |
| `deltempo startup enable <app>` | Reactiva un programa de inicio desactivado | N/A |
| `deltempo repair [subcommand]` | Comprobación de integridad de Windows y reparación de mantenimiento | `sfc`, `dism`, `winsxs`, `chkdsk`, `update`, `network` |
| `deltempo status` | Muestra la telemetría del sistema y la información de memoria | `--json` |
| `deltempo test` | Autocomprobación del motor (API de memoria, ámbitos detectados) | N/A |
| `deltempo help` | Muestra la referencia completa de comandos | N/A |
| `deltempo update [check]` | Comprueba versiones oficiales o aplica la actualización | `check`, `--dry-run` |
| `deltempo register` | Instala / inspecciona el propio comando `deltempo` | `--status`, `--remove` |
| `deltempo unregister` | Elimina todos los artefactos creados por `register` | N/A |

---

<a id="quick-start"></a>

## 🚀 Inicio rápido

> ### ⚡ Ejecútalo en una sola línea
>
> ```powershell
> irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
> ```
>
> Descarga la última versión, verifica su SHA-256, la almacena en caché en `%LOCALAPPDATA%\Deltempo\bin` y la lanza.
> ¿Prefieres la binaria de consola sin interfaz? Sustituye `win` por [`win-cli`](https://beso1227.github.io/Deltempo/).
>
> ### 💻 …y la CLI también queda lista
>
> El primer inicio instala por ti el comando `deltempo` — funcionan `cmd.exe`, PowerShell y <kbd>Win</kbd>+<kbd>R</kbd>.
> Abre después una ventana de terminal **nueva** y, a continuación:
>
> ```powershell
> deltempo test        # autocomprobación del motor
> deltempo status      # telemetría en vivo de disco y RAM
> deltempo register --status
> ```

### Opción 1: ejecutable portátil independiente (recomendado)

1. Descarga **`Deltempo.exe`** desde la página de la [Última versión](https://github.com/Beso1227/Deltempo/releases/latest).
2. Ejecuta `Deltempo.exe` directamente (sin instalador, archivo único autocontenido).
3. Haz clic en **Escanear ahora** o **Limpieza profunda en 1 clic** para recuperar espacio.

### Opción 2: una línea en la terminal (PowerShell)

Lanza la última versión directamente desde tu terminal — sin navegador, sin instalador:

```powershell
irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
```

El script de arranque descarga el último `Deltempo.exe`, verifica su SHA-256 frente al `checksums.sha256` publicado, lo almacena en caché en `%LOCALAPPDATA%\Deltempo\bin` y lo lanza. Para la binaria de la CLI sin interfaz, usa el punto de entrada `win-cli`:

```powershell
irm https://beso1227.github.io/Deltempo/win-cli | Invoke-Expression
```

Si el manifiesto no se puede leer o el hash no coincide, el instalador se aborta y no ejecuta nada de lo descargado: la verificación nunca se omite. Ten en cuenta que `Invoke-Expression` no acepta argumentos, por lo que el destino se elige mediante el punto de entrada y no mediante un interruptor `-Cli`.

### Opción 3: el comando `deltempo`

Nunca tienes que configurarlo a mano. La primera vez que inicias Deltempo, este aprovisiona una
binaria de CLI del subsistema de consola, la añade a tu `PATH` de usuario, registra el alias <kbd>Win</kbd>+<kbd>R</kbd>
e instala una función `deltempo` en tus perfiles de PowerShell — reparando un registro obsoleto
dejado por una instalación anterior si encuentra uno.

Dos cosas que conviene saber:

- **Abre una ventana de terminal nueva.** Las sesiones ya abiertas conservan el `PATH` con el que arrancaron.
- **¿Primer inicio sin conexión?** Si la binaria de consola no se puede aprovisionar, no se registra nada y no
  queda ningún comando a medio instalar. La aplicación de escritorio no se ve afectada: el siguiente
  inicio correcto lo reintenta.

¿Prefieres gestionarlo tú mismo? `deltempo register` hace lo mismo bajo demanda, y
`deltempo unregister` elimina todos los artefactos que creó. Usa `deltempo register --status` para ver
exactamente qué está instalado y a qué binario apunta.

> Los comandos que tocan estado protegido del sistema (`restore-points`, la fase de DISM de `deep-clean`)
> devuelven un error claro y un código de salida distinto de cero cuando se ejecutan desde una terminal estándar sin privilegios elevados.
> Ejecútalos desde una sesión de Administrador o usa la aplicación de escritorio.

---

<a id="privacy"></a>

## 🔒 Garantía de privacidad y seguridad

Deltempo está arquitectado con la seguridad y la privacidad del usuario como fundamentos innegociables:

1. **Garantía de cero telemetría**: Deltempo contiene **cero telemetría**, bibliotecas analíticas, SDK de publicidad ni rastreadores de sondeo en segundo plano. El escaneo, la limpieza, la optimización de memoria y la desinstalación rutinarios se ejecutan **100% sin conexión**.
2. **Canal de seguridad determinista**:

   ```text
   ┌────────┐     ┌────────┐     ┌─────────┐     ┌────────────┐     ┌─────────┐
   │  SCAN  │ ──► │  PLAN  │ ──► │ PROTECT │ ──► │ REVALIDATE │ ──► │  CLEAN  │
   └────────┘     └────────┘     └─────────┘     └────────────┘     └─────────┘
   ```

   Los archivos candidatos se planifican, se verifican frente a los límites de los directorios protegidos (Documentos, Escritorio, repositorios de código, claves SSH, credenciales) y se vuelven a validar justo antes de la eliminación.
3. **Defensa contra puntos de reanálisis y traversiones**: Las uniones de directorios NTFS, los enlaces simbólicos y los puntos de montaje de volumen se rechazan automáticamente para evitar ataques de traversiones fuera de los límites definidos.
4. **Límite del disco local**: Confined exclusivamente a discos fijos locales; los recursos de red remotos y las rutas UNC están bloqueados.
5. **Verificación criptográfica de las versiones**: Las comprobaciones automáticas de actualización exigen HTTPS y verifican los resúmenes SHA-256 de las binarias frente a los manifiestos firmados de las versiones de GitHub.

---

## 🛠️ Compilación desde el código fuente

### Requisitos previos

* Windows 10 o 11 (64 bits / x64)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* PowerShell 7+ o Windows PowerShell 5.1

### Compilación y pruebas

```powershell
# Clona el repositorio
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# Compila toda la solución en la configuración Release
dotnet build deltempo.sln -c Release

# Ejecuta la suite de pruebas automatizadas (726 pruebas unitarias y de integración aprobadas)
dotnet test deltempo.sln -c Release

# Empaqueta las binarias independientes de archivo único de la versión (GUI y CLI)
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

Los ejecutables de archivo único resultantes se publicarán en:

* `publish\Deltempo.exe` (GUI)
* `publish\deltempo_cli.exe` (CLI)

---

## 📜 Licencia

Deltempo es software libre y de código abierto bajo la **[Licencia MIT](LICENSE)**.

```text
Copyright (c) 2026 Beso1227 / Deltempo Project
```

---

## ❓ Preguntas frecuentes

**¿Es Deltempo realmente gratuito?**
Sí. Deltempo es completamente gratuito y de código abierto bajo MIT — sin nivel de pago, sin ventas adicionales, sin cuenta y sin publicidad.

**¿Es Deltempo una buena alternativa a CCleaner?**
Sí. Deltempo es una alternativa gratuita, de código abierto y sin telemetría a CCleaner que además incluye un desinstalador profundo, un buscador de archivos duplicados, un localizador de archivos grandes y reparación integrada del sistema Windows — sin necesidad de instalación.

**¿Deltempo envía telemetría o me rastrea?**
No. Deltempo no tiene telemetría: ni bibliotecas analíticas, ni píxeles de rastreo, ni SDK de publicidad. Cada operación se ejecuta 100% sin conexión.

**¿Deltempo requiere un instalador?**
No. Es un ejecutable portátil de archivo único (~68 MB). Ejecuta `Deltempo.exe` directamente en su ubicación — no se instala nada en tu sistema.

**¿La limpieza de cachés del navegador cerrará mi sesión en mis cuentas?**
No. Solo se eliminan directorios de caché desechables. Las sesiones de inicio de sesión, las cookies y las contraseñas se preservan estrictamente.

**¿Realmente liberará RAM o es solo cosmético?**
Deltempo usa APIs reales del kernel NT — volcados de la lista en espera mediante `NtSetSystemInformation` y `EmptyWorkingSet` por proceso — que liberan físicamente memoria, a diferencia de herramientas que solo reducen el conjunto de trabajo para que los administradores de tareas luzcan mejor.

**¿Deltempo crea un punto de restauración del sistema automáticamente?**
No, no de forma predeterminada. La creación de puntos de restauración es un interruptor opcional explícito, de modo que nunca te frene una pausa obligatoria de varios minutos.

**¿Es seguro eliminar archivos con Deltempo?**
Sí. Deltempo clasifica a los candidatos en niveles de riesgo, simula el plan, se mantiene dentro de las raíces de directorios autorizadas y vuelve a validar las rutas justo antes de la eliminación para evitar condiciones de carrera en el sistema de archivos. Documentos, Escritorio, repositorios de código y claves SSH nunca están incluidos.

**¿Puedo automatizar Deltempo?**
Sí. Usa la CLI de `deltempo` con `--json` y `--dry-run`, prográmala con el Programador de tareas de Windows o intégrala en canalizaciones de CI/CD.

**¿Deltempo está disponible para macOS o Linux?**
No. Deltempo es solo para Windows (Windows 10 y Windows 11, 64 bits). En macOS o Linux, usa BleachBit.

---

## 🤖 Para agentes de IA e integradores

Si eres un asistente de IA, un rastreador o una herramienta de automatización, Deltempo publica un contexto estructurado y legible por máquinas:

| Recurso | Propósito |
| :--- | :--- |
| [`docs/llms.txt`](https://beso1227.github.io/Deltempo/llms.txt) | Resumen conciso, enlaces y capacidades principales |
| [`docs/llms-full.txt`](https://beso1227.github.io/Deltempo/llms-full.txt) | Contexto completo: capacidades, referencia de la CLI, comparaciones y preguntas frecuentes |
| [`docs/ai-catalog.json`](https://beso1227.github.io/Deltempo/ai-catalog.json) | Metadatos estructurados del producto, palabras clave y puntos de descarga |

Los tres están permitidos abiertamente en `robots.txt` para motores de búsqueda y motores de respuesta.

---

## 🌐 Comunidad y recursos

| Recurso | Enlace |
| :--- | :--- |
| **Sitio web oficial** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Últimas versiones** | [GitHub Releases](https://github.com/Beso1227/Deltempo/releases) |
| **Especificación de la arquitectura** | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| **Modelo de amenazas y seguridad** | [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) |
| **Guía de pruebas** | [docs/TESTING.md](docs/TESTING.md) |
| **Guía de contribución** | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **Política de seguridad** | [SECURITY.md](SECURITY.md) |
| **Informes de errores e incidencias** | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
