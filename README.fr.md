<div align="center">

  <a href="https://beso1227.github.io/Deltempo/">
    <img src="docs/social-preview.png" alt="Deltempo - Nettoyeur Windows de haute précision et optimiseur de mémoire" width="100%" />
  </a>

  <br />

  # Deltempo : nettoyeur Windows open-source, désinstalleur d'applications et optimiseur de mémoire

  <p align="center">
    <strong>README:</strong>
    <a href="README.md">🇬🇧 English</a> ·
    <a href="README.es.md">🇪🇸 Español</a> ·
    <a href="README.zh-CN.md">🇨🇳 简体中文</a> ·
    <a href="README.hi.md">🇮🇳 हिन्दी</a> ·
    <b>🇫🇷 Français</b> ·
    <a href="README.pt-BR.md">🇧🇷 Português</a> ·
    <a href="README.ar.md">🇸🇦 العربية</a> ·
    <a href="README.ru.md">🇷🇺 Русский</a> ·
    <a href="README.ja.md">🇯🇵 日本語</a>
  </p>

  <p><strong>Nettoyeur de disque et optimiseur de RAM gratuit et open-source pour Windows 10 &amp; 11. Récupérez de 10 à 40 Go et plus de fichiers temporaires, de fichiers inutiles d'AppData et de caches de shaders GPU — avec en plus un désinstalleur approfondi et un chercheur de fichiers en double. Zéro télémétrie, sans publicité, sans installateur.</strong></p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48"><img alt="Télécharger la dernière version de Deltempo pour Windows" src="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://beso1227.github.io/Deltempo/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48"><img alt="Visiter le site officiel de Deltempo" src="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/deltempo_cli.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48"><img alt="Télécharger la CLI sans interface de Deltempo" src="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48"><img alt="Mettre Deltempo en favori sur GitHub" src="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48" height="48" /></picture></a>
  </p>

  <p>
    <sub>Vous préférez le terminal ? Une ligne installe et vérifie la dernière version &mdash;
      <code>irm https://beso1227.github.io/Deltempo/win | Invoke-Expression</code>
      &middot; Un bug ou une idée de fonctionnalité ? <a href="https://github.com/Beso1227/Deltempo/issues/new/choose"><strong>Ouvrez une issue</strong></a> &mdash; chaque demande est lue.
      Si Deltempo vous a rendu de l'espace disque, une <a href="https://github.com/Beso1227/Deltempo"><strong>étoile</strong></a> aide réellement d'autres personnes à le découvrir.</sub>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0&labelColor=16212B"><img alt="Dernière version" src="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/actions/workflows/ci.yml"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github&labelColor=16212B"><img alt="État du build CI" src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github" height="28" /></picture></a>
    <a href="docs/TESTING.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/tests-1483_passed-00F2B0?style=for-the-badge&label=QUALITY&labelColor=16212B"><img alt="Tests : 1471 réussis, 0 échec" src="https://img.shields.io/badge/tests-1483_passed-00F2B0?style=for-the-badge&label=QUALITY" height="28" /></picture></a>
    <a href="LICENSE"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github&labelColor=16212B"><img alt="Licence : MIT" src="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github" height="28" /></picture></a>
  </p>

  <p>
    <a href="https://dotnet.microsoft.com/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white&labelColor=16212B"><img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white" height="28" /></picture></a>
    <a href="https://learn.microsoft.com/dotnet/csharp/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white&labelColor=16212B"><img alt="C# 14" src="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white" height="28" /></picture></a>
    <a href="#at-a-glance"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows&labelColor=16212B"><img alt="Prise en charge de la plateforme" src="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY&labelColor=16212B"><img alt="Portable en fichier unique" src="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#privacy"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY&labelColor=16212B"><img alt="Zéro télémétrie, 100 % hors ligne" src="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY" height="28" /></picture></a>
    <a href="docs/THREAT_MODEL.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY&labelColor=16212B"><img alt="Durcissement STRIDE" src="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md#safety-pipeline"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY&labelColor=16212B"><img alt="Sécurité vérifiée en deux phases" src="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY&labelColor=16212B"><img alt="Moteur de mémoire natif du noyau NT" src="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#quick-start"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP&labelColor=16212B"><img alt="Installation en une ligne" src="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github&labelColor=16212B"><img alt="Téléchargements GitHub" src="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github&labelColor=16212B"><img alt="Étoiles GitHub" src="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/pulls"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING&labelColor=16212B"><img alt="Contributions bienvenues" src="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING" height="28" /></picture></a>
  </p>

  <p>
    <sub>
      <a href="docs/ARCHITECTURE.md">Architecture</a> &middot;
      <a href="docs/THREAT_MODEL.md">Modèle de menaces</a> &middot;
      <a href="docs/TESTING.md">Guide de tests</a> &middot;
      <a href="docs/BENCHMARKS.md">Benchmarks</a> &middot;
      <a href="docs/RELEASES.md">Ingénierie des versions</a> &middot;
      <a href="SECURITY.md">Politique de sécurité</a> &middot;
      <a href="#quick-start">Démarrage rapide</a>
    </sub>
  </p>

  <br />

  <video src="docs/deltempo.mp4" poster="docs/deltempo.webp" controls preload="metadata" width="100%"></video>

</div>

---

  <a id="at-a-glance"></a>

## Vue d'ensemble

| Propriété | Détail |
| :--- | :--- |
| **Site officiel** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Plateforme** | Windows 10 & 11 (64 bits / x64) |
| **Version** | v3.2.0 (version de production) |
| **Licence** | Open source ([MIT](LICENSE)) |
| **Interfaces** | Interface graphique de bureau moderne (WPF Fluent) et CLI en terminal sans interface |
| **Distribution** | Exécutable portable en fichier unique (autonome, sans installateur) |
| **Couverture des tests** | 1471 tests automatisés (100 % de réussite, 0 échec, 0 ignoré), fuzzing adversarial du système de fichiers |
| **Disponibilité de la CLI** | La commande `deltempo` s'auto-installe au premier lancement de l'interface — aucune configuration manuelle |
| **Télémétrie** | Zéro télémétrie. Les opérations de scan, de nettoyage, de mémoire et de désinstallation s'exécutent 100 % hors ligne |
| **Moteur de sécurité** | Planification en deux phases (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) avec 5 niveaux de risque et journalisation des transactions |
| **Refus délibérés** | Les chemins qui ressemblent à des caches mais sont indispensables ne sont jamais proposés comme cibles — caches sources MSI, installations du SDK .NET par utilisateur, racines nues de fournisseurs de pilotes, `registry\src` de Cargo, récupération de sessions Audacity. Voir [§2.1](#21-what-deltempo-refuses-to-delete). |
| **Rapport d'empreinte AppData** | Rapport en lecture seule de l'espace occupé dans AppData, du plus volumineux au plus petit, dans l'application et via `deltempo appdata-report`. Il ne fait que rapporter : aucun chemin de suppression n'existe sur l'une ou l'autre surface. |
| **Désinstalleur d'applications** | Désinstallation groupée silencieuse, moteur BCU, nettoyage des traces résiduelles AppData/registre, purge forcée des applications défectueuses |
| **Points de restauration** | Points de restauration système Windows facultatifs avant désinstallation (contrôlés par l'utilisateur, désactivés par défaut) |
| **Intelligence des services** | Explique : *« Quelque chose risque-t-il de casser si je désactive ceci ? »* via 3 verdicts de sécurité, heuristiques hors ligne et IA multi-modèles |
| **Gestionnaire de processus** | Liste des processus en temps réel avec extraction en direct des icônes haute densité (high-DPI) des applications et analyse de l'empreinte mémoire |
| **Intégration WinUtil** | Lanceur en 1 clic pour [Chris Titus Tech WinUtil](https://christitus.com/win) (CTT), par Chris Titus |
| **Moteur de mémoire** | Appels natifs au noyau NT Windows (`NtSetSystemInformation`, `EmptyWorkingSet`) |
| **Centre de préférences** | Centre de contrôle en 4 onglets catégorisés (*Mises à jour*, *Général*, *Mémoire*, *Stockage et sécurité*) |
| **Gardien de la zone de notification** | Icône Win32 native haute densité (`LoadCrispTrayIcon`) avec télémétrie RAM en direct et Boost en 1 clic |
| **Thèmes et accessibilité** | Mode clair Porcelain Slate au confort oculaire pro, mode sombre Obsidian et pleine sécurité de la disposition RTL arabe |
| **Mises à jour** | Canaux de version Stable et Beta vérifiés cryptographiquement (SHA-256) avec une préparation atomique |

---

## Qu'est-ce que Deltempo ?

**Deltempo** est une suite de maintenance Windows moderne, performante et open-source, conçue pour récupérer de l'espace de stockage en toute sécurité, désinstaller en profondeur les logiciels têtus, surveiller l'impact des entrées de démarrage sur le temps de démarrage du système et optimiser la mémoire système. Elle purge les caches applicatifs jetables, les résidus d'installateurs orphelins, les artefacts de compilation et les journaux système obsolètes sans toucher aux documents personnels, aux identifiants de navigateur ni aux composants critiques du système d'exploitation.

Les utilitaires de nettoyage traditionnels fonctionnent souvent comme des boîtes noires opaques, installent des logiciels publicitaires groupés ou laissent d'importants résidus dans le registre. Deltempo est construite sur une **architecture où la sécurité prime** : les chemins candidats sont classés dans des niveaux de risque explicites, simulés avant suppression, bornés aux racines de répertoires autorisés et revalidés juste avant la suppression afin de contrer les conditions de concurrence du système de fichiers.

En plus du nettoyage du disque, Deltempo inclut des outils de gestion de la mémoire du noyau Windows NT de bas niveau pour vider les listes de pages en attente et réduire les jeux de travail inactifs via les appels système Win32 et NT officiels.

---

## ⚔️ Comment Deltempo se compare à la concurrence

La plupart des utilitaires de nettoyage et d'optimisation Windows embarquent du logiciel publicitaire commercial, imposent des services d'arrière-plan intrusifs, verrouillent les fonctionnalités essentielles derrière des abonnements payants ou reposent sur des bases de code obsolètes.

Deltempo est entièrement open source, ne contient aucune télémétrie, ne nécessite aucune installation et fournit une suite de bout en bout combinant nettoyage de précision, désinstallation approfondie des logiciels, gestion de la mémoire au niveau du noyau et intelligence des services de démarrage.

| Capacité / Fonctionnalité | **Deltempo** | **CCleaner** | **BleachBit** | **Bulk Crap Uninstaller** | **Windows Storage Sense** |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Licence et code source** | **MIT open source (C# 14 / .NET 10)** | Propriétaire / Commercial | GPLv3 open source (Python/GTK) | Apache 2.0 open source (.NET) | Propriétaire (Microsoft) |
| **Télémétrie et confidentialité** | **Zéro télémétrie (100 % hors ligne)** | ⚠️ Traqueurs et collecte de données | ✅ Zéro télémétrie | Télémétrie minimale | Télémétrie de diagnostic Windows |
| **Adware groupé / ventes additionnelles** | **Aucun / Jamais** | ⚠️ Lots d'adware historiques et ventes additionnelles | Aucun | Aucun | Aucun |
| **Moteur de mémoire** | **Noyau NT natif (`NtSetSystemInformation`)** | ⚠️ Basique (Pro payant uniquement) | ❌ Aucun | ❌ Aucun | ❌ Aucun |
| **Désinstalleur d'applications en profondeur** | **Groupé silencieux + éradication des résidus** | ⚠️ Basique (Pro payant pour le mode approfondi) | ❌ Aucun | ✅ Complet | ❌ Ajout/suppression de base uniquement |
| **Points de restauration facultatifs** | **Optionnel (choix de l'utilisateur, désactivé par défaut)** | ⚠️ Automatisé / fonctionnalité payante | ❌ Aucun | Optionnel | Activation/désactivation manuelle du système |
| **Intelligence des services de démarrage** | **Oui (« Quelque chose va-t-il casser ? » — verdicts à 3 niveaux)** | ❌ Simple liste d'activation/désactivation | ❌ Aucun | Liste détaillée du registre | Métriques de base du Gestionnaire des tâches |
| **Balayage des traces résiduelles** | **AppData, ProgramData, registre et raccourcis** | ⚠️ Pro payant uniquement | ❌ Aucun | ✅ Recherche manuelle dans le registre | ❌ Aucun |
| **Caches de développement et de shaders** | **NuGet, npm, pip, Cargo, Gradle, shaders GPU** | ❌ Navigateur et Windows uniquement | ⚠️ Partiel | ❌ Aucun | ❌ Aucun |
| **Détection des fichiers volumineux** | **Catégorisés par IA (>50 Mo, classés par risque)** | ❌ Recherche de fichiers basique | ❌ Aucun | ❌ Aucun | Répartition de base des disques |
| **Réparation des fichiers système** | **SFC, DISM et CHKDSK intégrés** | ❌ Utilitaire payant séparé | ❌ Aucun | ❌ Aucun | Invite de commandes manuelle |
| **Intégration WinUtil (Chris Titus)** | **Lanceur en 1 clic avec élévation** (projet tiers de Chris Titus) | ❌ Aucun | ❌ Aucun | ❌ Aucun | ❌ Aucun |
| **Interface moderne et confort oculaire** | **Obsidian Dark et Porcelain Slate Light (WPF)** | Obsolète / surchargée | Interface GTK2/3 héritée | Interface WinForms héritée | Paramètres Windows intégrés |
| **Parité complète de la CLI** | **Oui (CLI `deltempo` avec `--json` et dry-run)** | ⚠️ Options de commande limitées | CLI basique | CLI basique | ❌ Aucun |
| **Distribution** | **Portable en fichier unique (68 Mo, sans installateur)** | Nécessite un installateur et des services | Installateur ou zip portable | Nécessite un installateur et un runtime | Intégré au système d'exploitation |

---

## 🌟 Tour d'horizon des fonctionnalités principales

### 1. 🧹 Nettoyeur de stockage de précision sur 26+ périmètres

Deltempo cible les données jetables dans les environnements système, de développement et de jeu sans toucher aux documents de l'utilisateur, aux jetons d'authentification actifs ni aux paramètres personnels :

* **Périmètres du système d'exploitation** : Temp utilisateur (`%TEMP%`), Temp Windows (`C:\Windows\Temp`), Prefetch, caches de téléchargement de Windows Update (`SoftwareDistribution\Download`), résidus de mise à niveau Windows (`$WINDOWS.~BT`), caches d'Optimisation de distribution, rapports d'erreur Windows (`WER`), vidages mémoire et caches de polices/vignettes.
* **Shaders GPU et jeux** : cache OTA NVIDIA App / GeForce Experience, cache AMD Radeon Software, caches de shaders DirectX (`D3DSCache`), pipelines Vulkan (`GLCache`), pré-mise en cache des shaders Steam et caches web du lanceur Epic Games.
* **Écosystème des développeurs** : cache local NuGet v3, cache npm, cache pip, cache cible de Rust Cargo, caches Gradle, instantanés temporaires de l'émulateur Android Studio et caches des extensions VS Code.
* **Navigateurs modernes et communication** : répertoires de cache jetables des profils Chromium (Chrome, Edge, Brave, Opera, Vivaldi, Arc) et Gecko (Firefox) avec **sessions de connexion strictement préservées** ; caches multimédias de Discord, Slack et Spotify.
* **Bouclier de sécurité (>24 h)** : garde-fou optionnel qui exempte tout fichier créé ou modifié au cours des 24 dernières heures afin d'éviter les conflits avec des installateurs d'arrière-plan actifs ou des éditeurs en cours d'exécution.
* **Protection TOCTOU** : vérifie les limites, les attributs et les chemins canoniques des fichiers juste avant la suppression pour éliminer les conditions de concurrence.
* **Détection des applications Store orphelines** : les répertoires AppData dont le package MSIX propriétaire n'est plus installé sont identifiés à partir de preuves binaires du référentiel `AppModel` — nom de famille du package analysé, apparié sur l'ID d'éditeur — plutôt qu'à partir d'heuristiques fondées sur le nom du répertoire. Les résultats sont signalés comme `LowRisk` et ne sont jamais présélectionnés pour la suppression.

<a id="21-what-deltempo-refuses-to-delete"></a>

### 2.1 Ce que Deltempo refuse de supprimer

Un nettoyeur qui ne connaît que ce qui peut être supprimé en toute sécurité est dangereux, car « temporaire » et « jetable » sont souvent la même illusion. Plusieurs chemins qui ressemblent en surface exactement à des répertoires de compilation ou de téléchargement sont en réalité indispensables, et Deltempo les refuse par construction — ils sont absents des résolveurs de périmètre et refusés indépendamment par `ProtectionPolicy`, si bien qu'aucun réglage, drapeau ou option de CLI ne peut les proposer :

| Chemin refusé | Pourquoi sa suppression est destructrice |
| :--- | :--- |
| `%ProgramData%\Package Cache`<br>`%WinDir%\Installer\$PatchCache$` | Les caches sources MSI dont `msiexec` a besoin pour **réparer** et **désinstaller** les produits installés. Les supprimer casse définitivement Modifier/Réparer et le chemin de désinstallation Programmes et Fonctionnalités sur toute la machine. |
| `%LOCALAPPDATA%\Microsoft\dotnet` | La racine d'**installation** du SDK et du runtime .NET par utilisateur (exécutables d'hébergement `dotnet.exe`, `sdk\`, `packs\`, `shared\`), pas un cache. Seul le répertoire de travail transitoire `%TEMP%\.net` est jetable. |
| `C:\NVIDIA`, `C:\AMD`, `C:\Intel`, `C:\Realtek` | Les racines nues de fournisseurs dans lesquelles un installateur de pilote décompresse ses fichiers. Une passe de nettoyage lancée en pleine installation supprimerait une charge utile que l'installateur n'a pas encore consommée et ne peut pas récupérer. Seuls des sous-répertoires de préparation spécifiques sont ciblés. |
| `~\.cargo\registry\src` | Les sources de crates **extraites** que `cargo build --offline` compile. Son homologue `registry\cache` est nettoyé ; `src` ne l'est pas, car vider l'un sans l'autre est une opération « tout ou rien » que ce modèle par cible ne peut pas exprimer en toute sécurité. |
| `%LOCALAPPDATA%\Audacity\SessionData` | Données de **récupération** de sessions non enregistrées, pas des fichiers de travail temporaires. Les supprimer détruit des enregistrements en cours qui n'ont jamais été sauvegardés. |
| `%ProgramData%\Microsoft\Windows Defender\Definition Updates\Backup` | Copies de **retour arrière** des définitions de Defender. Les supprimer retire la possibilité de revenir en arrière sur une mise à jour de définitions défectueuse. |
| `%LOCALAPPDATA%\Electronic Arts\EA Desktop\UserConfig` | Les paramètres d'application d'EA Desktop, pas un cache. |

Ces chemins sont refusés deux fois : ils sont retirés des résolveurs de périmètre, *et* `ProtectionPolicy` les refuse indépendamment. Une régression d'un résolveur, un jeu de règles fourni par l'utilisateur ou une entrée de paramètres obsolète ne peuvent donc pas les réintroduire.

Cette liste est appliquée dans le code et couverte par `CleanerServiceTests` — chaque exclusion est accompagnée d'un test vérifiant que le chemin est absent *et* d'une assertion jumelle prouvant que le répertoire de travail temporaire correspondant est toujours présent, afin que la correction ne puisse pas se dégrader en suppression trop large.

---

### 2. 📦 Désinstalleur d'applications en profondeur et balayage des résidus

Dites adieu aux logiciels têtus, aux programmes à moitié désinstallés et aux assistants de désinstallation en désordre :

* **Inventaire unifié des applications** : analyse les hives de registre 64 bits et 32 bits (`HKLM`, `HKCU`) ainsi que les paquets modernes du Windows Store (AppX/UWP). Affiche la taille d'installation réelle, la version, les dates d'installation et la vérification de l'éditeur.
* **Point de restauration système facultatif** : contrairement à d'autres utilitaires qui imposent un point de restauration système lent de 2 minutes ou le sautent entièrement, Deltempo laisse le contrôle total à l'utilisateur. Un commutateur dédié vous permet de décider de créer un point de restauration avant désinstallation (**désactivé par défaut**).
* **Désinstallation groupée silencieuse de plusieurs applications** : sélectionnez plusieurs applications et lancez une désinstallation sans surveillance sans devoir valider des dizaines de boîtes de dialogue d'installateur répétitives.
* **Balayage approfondi des résidus** : une fois le désinstalleur d'une application terminé, l'analyseur de Deltempo traque les résidus orphelins :
  * Branches du registre : `HKCU\Software\<Vendor>`, `HKLM\Software\<Vendor>`, `HKLM\Software\WOW6432Node\<Vendor>`.
  * Répertoires du système de fichiers : `%LocalAppData%\<App>`, `%AppData%\<App>`, `%ProgramData%\<App>`, `%ProgramFiles%\<App>`.
  * Entrées de démarrage et raccourcis orphelins du menu Démarrer.
* **Purge forcée des logiciels corrompus** : si un désinstalleur est cassé, introuvable ou génère des erreurs, Deltempo nettoie de force tous les répertoires associés et désenregistre proprement ses clés de registre.

---

### 3. ⚡ Optimiseur de mémoire natif du noyau NT Windows

Contrairement aux « nettoyeurs de RAM » grand public qui forcent simplement la mémoire dans le fichier d'échange et ralentissent votre PC, Deltempo utilise des appels système natifs et documentés du noyau NT Windows :

* **Invalidation de la liste en attente** : appelle `NtSetSystemInformation` avec `SystemMemoryListInformation` (classe `80`) pour renvoyer les pages mémoire en attente inutilisées dans le pool disponible, au profit des tâches gourmandes (jeux, compilation, rendu).
* **Réduction des jeux de travail inactifs** : exploite `EmptyWorkingSet` avec un jeton de processus disposant des privilèges `SeProfileSingleProcessPrivilege` et `SeDebugPrivilege` pour libérer les jeux de travail abandonnés des processus d'arrière-plan inactifs.
* **Bouclier des processus critiques** : les composants fondamentaux de Windows (`csrss.exe`, `dwm.exe`, `explorer.exe`, `lsass.exe`, `services.exe`, `smss.exe`, `svchost.exe` et Windows Defender) sont automatiquement protégés et jamais réduits.
* **Boost automatique en arrière-plan** : peut surveiller la pression mémoire en arrière-plan et déclencher automatiquement un nettoyage lorsque l'utilisation de la RAM physique dépasse un seuil défini par l'utilisateur (p. ex. 85 %).

---

### 4. 🧠 Gestionnaire de démarrage et intelligence des services

Ne vous demandez plus quels programmes ralentissent le démarrage de votre PC :

* **« Quelque chose risque-t-il de casser si je désactive ceci ? »** : chaque application de démarrage et chaque service d'arrière-plan est analysé avec un badge de verdict intelligent à 3 niveaux :
  * 🟢 **SafeToDisable** : lanceurs de confort, mises à jour de jeux et applications de communication qui n'ont pas besoin de démarrer avec Windows.
  * 🟡 **CautionNeeded** : panneaux de contrôle audio, utilitaires de pavé tactile ou logiciels de périphériques dont les raccourcis clavier ou les menus de la zone de notification peuvent devenir inactifs jusqu'à une ouverture manuelle.
  * 🔴 **EssentialKeep** : suites de sécurité, agents de sauvegarde et de synchronisation dans le cloud ou pilotes matériels essentiels.
* **Pipeline d'intelligence double** :
  * **Heuristiques hors ligne** : classification déterministe instantanée basée sur les signatures numériques, l'identité vérifiée des éditeurs, les chemins des exécutables et les bases de données de processus connues.
  * **IA multi-fournisseurs facultative** : résumés opérationnels détaillés à la demande, prenant en charge OpenAI, Anthropic Claude, Google Gemini, Groq, OpenRouter et les modèles locaux hors ligne (Ollama, LM Studio).
* **Activation/désactivation dans le registre, 100 % réversible** : les éléments désactivés sont stockés en toute sécurité dans les clés de registre `Run_Deltempo_Disabled`. Tout élément peut être réactivé en un seul clic.

---

### 5. 🔍 Inspecteur de fichiers volumineux catégorisés par IA

Découvrez ce qui consomme réellement votre espace disque :

* **Analyse multidisques** : analysez rapidement `C:\` ou tout autre disque fixe secondaire à la recherche de fichiers dépassant des seuils de taille personnalisables (>50 Mo, >100 Mo, >500 Mo, >1 Go).
* **Étiquetage automatique des catégories** : regroupe intelligemment les découvertes en archives (`.zip`, `.rar`, `.7z`), images disque (`.iso`, `.vhd`), disques de machine virtuelle (`.vmdk`, `.vhdx`), installateurs (`.msi`, `.exe`), médias vidéo/audio et fichiers de log périmés.
* **Classement des risques de sécurité** : chaque fichier volumineux fait l'objet d'une évaluation de sécurité avant que vous n'y touchiez, ce qui évite la suppression accidentelle de disques d'hyperviseur ou d'installations de jeux importantes.

---

### 6. 🛠️ Réparation du système Windows et intégration CTT WinUtil

Diagnostiquez et réparez la corruption du système d'exploitation Windows directement depuis l'interface :

* **SFC (System File Checker)** : exécute `sfc /scannow` avec les privilèges d'administrateur pour réparer les fichiers système corrompus.
* **Maintenance DISM** : vérifie, analyse et restaure la santé du magasin de composants Windows (`/Cleanup-Image /RestoreHealth`).
* **Réinitialisation de base WinSxS** : nettoie les versions du magasin de composants remplacées pour récupérer des gigaoctets après les mises à jour majeures de Windows.
* **CHKDSK et réinitialisation réseau** : planifier la vérification des volumes de disque au prochain démarrage ou vider le cache DNS et réinitialiser les piles Winsock en un clic.
 * **Chris Titus Tech WinUtil (CTT)** : le lanceur en 1 clic ouvre WinUtil dans une session PowerShell élevée afin de supprimer les applications préinstallées, de retirer la télémétrie et d'installer automatiquement winget. WinUtil est un **utilitaire tiers de [Chris Titus](https://christitus.com)** : il ne fait pas partie de Deltempo et n'est pas couvert par l'audit de Deltempo. Voir [Avis de tiers](THIRD-PARTY-NOTICES.md).

---

### 7. 📊 Gestionnaire de processus en temps réel

* **Extraction native d'icônes haute densité** : extraction en direct d'icônes nettes 32 bits des exécutables à l'aide des routines Win32 natives `SHGetFileInfo` et `ExtractIconEx`.
* **Télémétrie mémoire et PID** : empreinte mémoire des processus en temps réel, identifiant de processus, informations de l'éditeur et chemin du fichier.
* **Terminaison sûre** : des routines de terminaison protégées empêchent l'arrêt accidentel des processus système critiques de Windows.

---

### 8. 📊 Rapport d'empreinte AppData et détection des applications Store orphelines

`%LOCALAPPDATA%` et `%APPDATA%` sont les plus gros et les moins bien compris consommateurs d'espace disque sur une machine Windows typique, et c'est aussi là que se logent les erreurs les plus destructrices. Deltempo les traite en lecture seule par défaut.

* **Rapport d'empreinte en lecture seule** : une ventilation du plus gros au plus petit de ce qui occupe réellement AppData, accessible depuis le bouton **AppData Report** dans l'application et via `deltempo appdata-report` dans la CLI. Il mesure, trie et affiche. Il n'y a ni `--clean`, ni `--yes`, ni aucun chemin de suppression sur l'une ou l'autre surface — délibérément, et non par hasard.
* **Sécurité des points de réanalyse** : les jonctions de compatibilité héritées d'AppData (`Application Data`, `Local Settings`) pointent à nouveau vers `Local`. Les parcourir relit le même arbre et gonfle le total ; elles sont donc ignorées. Mesuré sur un profil réel : la correction a ramené une mesure de 59.5 GB à la valeur réelle de 32.6 GB sur 123 dossiers.
* **Applications Microsoft Store orphelines** : `StoreAppOrphanResolver` lit le référentiel de packages AppModel (les hives `HKCU` et `HKLM`) comme preuve binaire et apparie chaque dossier de `LOCALAPPDATA\Packages` à son package MSIX propriétaire. Le nom du dossier (`Name_PublisherId`) est rapproché de la clé du référentiel (`Name_Version_Arch_ResourceId_PublisherId`) en le scindant au dernier tiret bas et en appariant sur l'ID d'éditeur plus le préfixe du nom. Si aucune hive ne s'ouvre, le résolveur ne renvoie rien plutôt que de deviner. Les orphelins apparaissent comme `LowRisk`, non sélectionnés, et `ProtectionPolicy` reste l'unique source de vérité pour les refus.

> **Une réponse correcte peut être « rien ».** Sur une machine où aucune application Store n'a été supprimée, le rapport renvoie zéro orphelin. C'est le comportement voulu : la fonctionnalité est jugée sur l'absence de faux positifs, pas sur son rendement.

### 9. 🎨 Thèmes confort oculaire pro et support multilingue RTL

Conçu avec une attention obsessionnelle portée à l'expérience utilisateur :

* **Mode clair confort oculaire pro** : un thème apaisant porcelaine/ardoise Fluent/macOS (`#F1F5F9`) qui élimine complètement la fatigue oculaire, remplace les écrans blancs éblouissants et utilise des accents Ocean Azure (`#0284C7`) à fort contraste.
* **Mode sombre Obsidian** : thème sombre profond et élégant avec des accents cyan électrique éclatants, un effet glassmorphism subtil et des cartes à double cadre.
* **Couverture multilingue (en cours)** : sélecteur de langue pour **anglais, arabe, espagnol, français et allemand**. L'espace de travail Cleaner est traduit et l'arabe active le flux RTL natif ; les boîtes de dialogue modales et les autres espaces de travail ne sont pas encore traduits.
* **Disposition RTL et protection numérique** : le mode arabe active un véritable flux de fenêtres de droite à gauche (RTL) tout en imposant strictement un formatage de gauche à droite sur les métriques, les chemins et les indicateurs de progression (`0.0 MB`, `32%`, `C:\...`) afin que les chiffres et les statistiques de stockage ne soient jamais inversés ni altérés.

---

### 10. 🔔 Gardien parfaitement net de la zone de notification

* **Icône Win32 haute densité authentique (`LoadCrispTrayIcon`)** : utilise la création directe d'icônes GDI Win32 (`CreateIconIndirect`) avec transparence alpha ARGB 32 bits, offrant un rendu ultra-net sur les écrans à mise à l'échelle 100 %, 125 %, 150 %, 175 % et 200 %+ sans flou.
* **Télémétrie en direct au survol** : affiche l'utilisation de la mémoire en temps réel dans l'infobulle de la zone de notification : `RAM: 42% (13.4 GB / 31.9 GB)`.
* **Actions contextuelles rapides** : cliquez avec le bouton droit pour déclencher **Boost mémoire en 1 clic** ou **Nettoyage intelligent rapide** sans ouvrir la fenêtre principale.
* **Résilience vis-à-vis d'Explorer** : écoute le message de diffusion `TaskbarCreated` de Windows pour restaurer automatiquement l'icône si `explorer.exe` redémarre.

---

## 💻 Référence de la CLI et automatisation sans interface

Deltempo inclut une CLI performante et scriptable (`deltempo_cli.exe` ou la commande `deltempo`) conçue pour les tâches planifiées, les administrateurs système et les environnements sans interface.

```powershell
# Aperçu des cibles nettoyables sans supprimer de fichiers (simulation / dry-run)
deltempo clean --dry-run

# Exécuter un nettoyage sûr et envoyer les fichiers supprimés dans la Corbeille Windows
deltempo clean --safe --recycle-bin

# Vider la RAM en attente (standby) et réduire les jeux de travail des processus
deltempo boost

# Désinstallation approfondie d'une application sans créer de point de restauration
deltempo uninstall "Google Chrome" --silent --force

# Création facultative d'un point de restauration pendant la désinstallation
deltempo uninstall "Epic Games Launcher" --restore-point

# Afficher la télémétrie système et l'état de la mémoire en JSON structuré
deltempo status --json

# Voir ce qui occupe réellement AppData, du plus volumineux au plus petit (lecture seule ; ne peut rien supprimer)
deltempo appdata-report --top 15
```

### Résumé des commandes de la CLI

| Commande | Objectif | Drapeaux et options clés |
| :--- | :--- | :--- |
| `deltempo scan [filter]` | Analyser les cibles à la recherche de données jetables | `--json`, `--silent` |
| `deltempo clean [filter]` | Nettoyer les caches jetables | `--dry-run`, `--recycle-bin`, `--safe`, `--all`, `--json`, `--yes` |
| `deltempo smart-clean` | Purge rapide des caches vérifiés comme sûrs | `--dry-run`, `--json`, `--yes` |
| `deltempo deep-clean` | Nettoyage complet autonome (RAM, DISM, périmètres) | `--dry-run`, `--json`, `--yes` |
| `deltempo restore-points` | Inspecter et purger les points de restauration système | `--clean`, `--all` (nécessite une élévation) |
| `deltempo appdata-report` | Rapport d'empreinte AppData en lecture seule, du plus volumineux au plus petit | `--json`, `--top <n>` — **aucun drapeau de suppression n'existe** |
| `deltempo duplicates [path]` | Recherche de doublons à l'octet près (hachage en 3 étapes) | `--delete`, `--strategy` |
| `deltempo large [path]` | Analyser les disques à la recherche de fichiers consommant beaucoup d'espace | `--min <size>`, `--type <cat>`, `--safe`, `--top <n>`, `--sort <size\|date>` |
| `deltempo large inspect <file>` | Inspecter le niveau de risque d'un fichier et son verdict de sécurité | `--json` |
| `deltempo large clean` | Envoyer les fichiers volumineux jetables à la corbeille | `--dry-run`, `--yes`, `--safe-only` |
| `deltempo large delete <file>` | Envoyer un fichier spécifique à la corbeille | `--yes` |
| `deltempo force-delete <path>` | Purger un fichier/dossier verrouillé ou récalcitrant | `--terminate-lockers`, `--recycle`, `--dry-run`, `--retry <n>`, `--force-override` |
| `deltempo force-delete scan <path>` | Inspecter la résistance au verrouillage, les détenteurs de verrous et le niveau de risque | `--json` |
| `deltempo force-delete pending` | Lister les fichiers en attente de suppression au prochain redémarrage | `--json` |
| `deltempo boost` | Optimiser la mémoire système via le noyau NT | `--all`, `--standby`, `--cache`, `--workingsets`, `--json` |
| `deltempo procs` | Lister les processus d'arrière-plan consommant plus de 20 Mo | `--json` |
| `deltempo procs trim <pid\|name>` | Réduire le jeu de travail d'un processus | `--json` |
| `deltempo procs kill <pid\|name>` | Terminer un processus d'arrière-plan hors contrôle | `--json` |
| `deltempo uninstall <app>` | Désinstallation approfondie et purge des résidus | `--dry-run`, `--force`, `--silent`, `--restore-point`, `--json` |
| `deltempo startup [list]` | Inspecter les applications de démarrage et leur impact sur le temps de démarrage | `--high`, `--json` |
| `deltempo startup disable <app>` | Désactiver de manière réversible un programme de démarrage | N/A |
| `deltempo startup enable <app>` | Réactiver un programme de démarrage désactivé | N/A |
| `deltempo repair [subcommand]` | Vérification de l'intégrité de Windows et réparation via le mode maintenance | `all`, `sfc`, `dism`, `winsxs`, `chkdsk`, `scanhealth`, `update`, `network` |
| `deltempo schedule` | Gérer la maintenance hebdomadaire de l'ordonnanceur de tâches | install / remove / status |
| `deltempo shell` | Gérer l'intégration du menu contextuel d'Explorer | install / remove / status |
| `deltempo status` | Afficher la télémétrie système et les informations mémoire | `--json` |
| `deltempo test` | Auto-vérifier le moteur (API mémoire, périmètres détectés) | N/A |
| `deltempo help` | Afficher la référence complète des commandes | N/A |
| `deltempo update [check]` | Vérifier les versions officielles ou appliquer une mise à jour | `check`, `--dry-run` |
| `deltempo register` | Installer / inspecter la commande `deltempo` elle-même | `--status`, `--remove` |
| `deltempo unregister` | Supprimer tous les artefacts créés par `register` | N/A |
| `deltempo kill` | Fermer les instances Deltempo en arrière-plan en cours d'exécution | N/A |

**Options globales** s'appliquent à l'ensemble des commandes de nettoyage : `--smart` / `--safe-only`, `--recycle-bin` / `-r`, `--unsafe` (désactive le bouclier de 24 heures), `--dry-run` / `-d`, `--yes` / `-y`, `--json` / `-j`, `--silent` / `-s`, et `--export <file>` pour un journal d'audit horodaté.

---

  <a id="quick-start"></a>

## 🚀 Démarrage rapide

> ### ⚡ Exécutez-le en une ligne
>
> ```powershell
> irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
> ```
>
> Télécharge la dernière version, vérifie son SHA-256, la met en cache dans `%LOCALAPPDATA%\Deltempo\bin` et la lance.
> Vous préférez le binaire console sans interface ? Remplacez `win` par [`win-cli`](https://beso1227.github.io/Deltempo/).
>
> ### 💻 …et la CLI est aussi prête
>
> Au premier lancement, la commande `deltempo` est installée pour vous — `cmd.exe`, PowerShell et <kbd>Win</kbd>+<kbd>R</kbd> fonctionnent tous.
> Ouvrez ensuite une **nouvelle** fenêtre de terminal, puis :
>
> ```powershell
> deltempo test        # auto-vérification du moteur
> deltempo status      # télémétrie disque + RAM en direct
> deltempo register --status
> ```

### Option 1 : Exécutable portable autonome (recommandé)

1. Téléchargez **`Deltempo.exe`** depuis la page de la [dernière version](https://github.com/Beso1227/Deltempo/releases/latest).
2. Exécutez `Deltempo.exe` directement (sans installateur, fichier unique autonome).
3. Cliquez sur **Analyser maintenant** ou **Nettoyage approfondi en 1 clic** pour récupérer de l'espace.

### Option 2 : Commande en une ligne dans le terminal (PowerShell)

Lancez la dernière version directement depuis votre terminal — sans navigateur, sans installateur :

```powershell
irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
```

Le script d'amorçage télécharge la dernière version de `Deltempo.exe`, vérifie son SHA-256 par rapport au `checksums.sha256` publié, le met en cache dans `%LOCALAPPDATA%\Deltempo\bin` et le lance. Pour le binaire CLI sans interface, utilisez le point d'entrée `win-cli` :

```powershell
irm https://beso1227.github.io/Deltempo/win-cli | Invoke-Expression
```

Si le manifeste ne peut pas être lu, ou si l'empreinte ne correspond pas, l'installateur abandonne et n'exécute rien de ce qu'il a téléchargé — la vérification n'est jamais sautée. Notez que `Invoke-Expression` n'accepte aucun argument : la cible est donc choisie par le point d'entrée plutôt que par une option `-Cli`.

### Option 3 : La commande `deltempo`

Vous n'avez jamais à configurer cela à la main. La première fois que vous lancez Deltempo, il installe
un binaire CLI de sous-système console, l'ajoute à votre `PATH` utilisateur, enregistre l'alias
<kbd>Win</kbd>+<kbd>R</kbd> et installe une fonction `deltempo` dans vos profils PowerShell — en réparant
au passage un enregistrement obsolète laissé par une ancienne installation s'il en trouve un.

Deux choses à savoir :

- **Ouvrez une nouvelle fenêtre de terminal.** Les shells déjà ouverts conservent le `PATH` avec lequel ils ont démarré.
- **Premier lancement hors ligne ?** Si le binaire console ne peut pas être installé, rien n'est enregistré
  et aucune commande à moitié installée ne subsiste. L'application de bureau n'est pas touchée — le prochain
  lancement réussi réessaiera.

Vous préférez le gérer vous-même ? `deltempo register` fait la même chose à la demande, et
`deltempo unregister` supprime tous les artefacts qu'il a créés. Utilisez `deltempo register --status`
pour voir exactement ce qui est installé et vers quel binaire il pointe.

> Les commandes qui touchent à l'état système protégé (`restore-points`, l'étape DISM de `deep-clean`)
> renvoient une erreur claire et un code de sortie non nul lorsqu'elles sont exécutées depuis un terminal
> standard, sans élévation de privilèges. Exécutez-les depuis une invite d'administration, ou utilisez l'application de bureau.

---

<a id="privacy"></a>

## 🔒 Garantie de confidentialité et de sécurité

Deltempo est architecturée autour de la sécurité et de la confidentialité de l'utilisateur, deux principes non négociables :

1. **Garantie de zéro télémétrie** : Deltempo ne contient **aucune télémétrie**, aucune bibliothèque d'analyse, aucun SDK publicitaire et aucun traqueur d'arrière-plan. Les analyses, nettoyages, optimisations mémoire et désinstallations courants s'exécutent **100 % hors ligne**.
2. **Pipeline de sécurité déterministe** :

   ```text
   ┌────────┐     ┌────────┐     ┌─────────┐     ┌────────────┐     ┌─────────┐
   │  SCAN  │ ──► │  PLAN  │ ──► │ PROTECT │ ──► │ REVALIDATE │ ──► │  CLEAN  │
   └────────┘     └────────┘     └─────────┘     └────────────┘     └─────────┘
   ```

   Les fichiers candidats sont planifiés, vérifiés par rapport aux limites des répertoires protégés (Documents, Bureau, dépôts de code, clés SSH, identifiants), puis revalidés juste avant la suppression.
3. **Défense contre les points de reanalyse et la traversée** : les jonctions de répertoires NTFS, les liens symboliques et les points de montage de volume sont automatiquement rejetés pour empêcher les attaques de traversée hors des limites cibles.
4. **Limite aux disques locaux** : confinement exclusif aux disques fixes locaux ; les partages réseau distants et les chemins UNC sont bloqués.
5. **Protection de l'état durable AppData** : Deltempo refuse de supprimer l'*état* des applications qui se trouve simplement sous `AppData`. Les bases de synchronisation OneDrive, les identifiants Office et Outlook, le coffre du Gestionnaire d'identifiants Windows, les listes de fichiers récents, les clés SSH et GnuPG, ainsi que les hives du shell `NTUSER.DAT` / `UsrClass.dat` sont tous refusés. Cette règle est appliquée **par répertoire, et pas seulement par fichier** : un `Directory.Delete` récursif supprime une sous-arborescence entière en un seul appel système et ne consulte jamais une protection par fichier ; les opérations de purge de résidus parcourrent donc d'abord l'arborescence à la recherche d'un descendant protégé, bornent ce parcours à 5 000 répertoires, ignorent les points de reanalyse et **refusent de poursuivre** si la borne est dépassée.
6. **Vérification cryptographique des versions** : les vérifications automatiques de mise à jour exigent HTTPS et vérifient les empreintes SHA-256 des binaires contre les manifestes de version GitHub signés.

> Le vérificateur de signature des manifestes utilise ECDSA P-256 avec une clé de remplacement intégrée, et ni lui ni la vérification Authenticode ne sont encore reliés au processus de mise à jour. La vérification de l'empreinte SHA-256 par rapport à l'artefact de version *est* en revanche bien appliquée. `docs/THREAT_MODEL.md` présente ces éléments comme des lacunes connues et non comme des mesures d'atténuation.

---

## 🛠️ Compilation à partir des sources

### Prérequis

* Windows 10 ou 11 (64 bits / x64)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* PowerShell 7+ ou Windows PowerShell 5.1

### Compilation et tests

```powershell
# Cloner le dépôt
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# Compiler toute la solution en configuration Release
dotnet build deltempo.sln -c Release

# Vérifier le style de code comme le fait la CI. Exécuter la vérification sur
# deltempo.sln, jamais sur un seul projet : la solution contient l'app WPF, Core, la CLI,
# le shell WinUI et le projet de tests, si bien qu'une vérification par projet ne couvre qu'une partie de la solution.
dotnet format deltempo.sln --verify-no-changes --no-restore

# Exécuter la suite de tests automatisés (1471 tests unitaires et d'intégration réussis)
# xunit.v3 s'exécute sur Microsoft.Testing.Platform, que le `dotnet test` du SDK
# .NET 10 ne peut pas prendre en charge ("Testing with VSTest target is no longer
# supported"). Construire une fois, puis invoquer directement l'exécutable de tests.
dotnet build Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release
./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category!=Benchmark"

# Packager les binaires de release autonomes en fichier unique (GUI et CLI)
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

Les exécutables en fichier unique résultants seront publiés dans :

* `publish\Deltempo.exe` (GUI)
* `publish\deltempo_cli.exe` (CLI)

---

## 🙏 Crédits et outils tiers

Deltempo est conçu et maintenu par les contributeurs de Deltempo. Une fonctionnalité
repose sur le travail de quelqu'un d'autre, et ce projet est reconnu ici dans les moindres détails :

| Projet | Auteur | Ce que Deltempo en fait |
|---|---|---|
| **[Chris Titus Tech Windows Utility (WinUtil)](https://christitus.com/win)** | **[Chris Titus](https://christitus.com)** | Le bouton **CTT WinUtil** de la section Réparation du système télécharge ce script et l'exécute dans une nouvelle fenêtre PowerShell **élevée**, exactement comme sa propre documentation l'indique |

### À noter

- WinUtil **n'est pas sous licence MIT** et **ne fait pas partie** de Deltempo. Deltempo ne le redistribue pas, ne le réplique pas, ne le fige pas et ne le vérifie pas, et ne peut pas le faire.
- Comme le script est téléchargé et exécuté en une seule étape, **tout ce que cette URL sert s'exécute avec les droits d'administrateur sur votre machine.** Lisez-le d'abord si vous le souhaitez : <https://christitus.com/win>
- Le moteur de sécurité de Deltempo, la revalidation avant suppression et la vérification des signatures des versions publiées ne couvrent **que le code de Deltempo**, jamais du code qu'il n'a pas vu.
- Si WinUtil vous est utile, soutenez directement [Chris Titus](https://christitus.com).

Les détails complets, et notamment la commande exacte exécutée par Deltempo, figurent dans
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## 📜 Licence

Deltempo est un logiciel gratuit et open-source sous licence **[Licence MIT](LICENSE)**.

```text
Copyright (c) 2026 Deltempo Contributors
```

---

## ❓ Foire aux questions

**Deltempo est-il vraiment gratuit ?**
Oui. Deltempo est entièrement gratuit et open source sous licence MIT — sans palier payant, sans ventes additionnelles, sans compte et sans publicité.

**Deltempo est-il une bonne alternative à CCleaner ?**
Oui. Deltempo est une alternative gratuite, open source et sans télémétrie à CCleaner qui inclut en plus un désinstalleur approfondi, un chercheur de fichiers en double, un détecteur de fichiers volumineux et une réparation intégrée du système Windows — sans aucune installation.

**Deltempo envoie-t-il des données de télémétrie ou me traque-t-il ?**
Non. Deltempo n'a aucune télémétrie : ni bibliothèques d'analyse, ni pixels de suivi, ni SDK publicitaires. Chaque opération s'exécute 100 % hors ligne.

**Deltempo nécessite-t-il un installateur ?**
Non. C'est un exécutable portable en fichier unique (~68 Mo). Exécutez `Deltempo.exe` directement sur place — rien n'est installé sur votre système.

**Le nettoyage des caches de navigateur va-t-il me déconnecter de mes comptes ?**
Non. Seuls les répertoires de cache jetables sont supprimés. Les sessions de connexion, les cookies et les mots de passe sont strictement préservés.

**Réduire la mémoire fait-il vraiment quelque chose, ou est-ce seulement cosmétique ?**
Cela déplace réellement la mémoire, mais ce n'est **pas** une optimisation des performances, et nous ne le prétendrons pas. Deltempo appelle de véritables API du noyau NT — vidage de la liste en attente via `NtSetSystemInformation` et `EmptyWorkingSet` par processus. Écarter le cache en attente relève bien le chiffre de mémoire *disponible*, car Windows compte les pages en attente comme disponibles ; en revanche, cela ne rend pas la machine plus rapide. La liste en attente est précisément le cache que Windows utilise pour satisfaire les nouvelles allocations sans toucher au disque : la jeter signifie généralement que la prochaine allocation provoque un défaut de page. La réduction des jeux de travail présente le même compromis en miniature : les pages sont repoussées sur le disque puis relues au prochain accès, ce qui peut brièvement *ralentir* une application. Le résumé honnête : **la mémoire libérée est une mémoire qui sera relue.** Le bénéfice réel est plus modeste que « plus de RAM libre » : cela peut aider une application qui a réellement enflé et libérer de l'espace d'adressage dans une situation contraint. Ce n'est pas un bouton « turbo », et Deltempo ne se présente pas comme tel. Chaque cible de l'interface indique sa conséquence réelle plutôt qu'un score de sécurité.

**Deltempo crée-t-il un point de restauration système automatiquement ?**
Non, pas par défaut. La création d'un point de restauration est une option explicite, si bien que vous n'êtes jamais ralenti par un point de restauration forcé de plusieurs minutes.

**Est-il sûr de supprimer des fichiers avec Deltempo ?**
Oui. Deltempo classe les candidats par niveaux de risque, simule le plan, reste dans les racines de répertoires autorisées et revalide les chemins juste avant la suppression pour prévenir les conditions de concurrence du système de fichiers. Les documents, le bureau, les dépôts de code et les clés SSH ne font jamais partie des cibles.

**Puis-je automatiser Deltempo ?**
Oui. Utilisez la CLI `deltempo` avec `--json` et `--dry-run`, planifiez-la avec l'ordonnanceur de tâches Windows ou intégrez-la dans des pipelines CI/CD.

**Deltempo est-il disponible pour macOS ou Linux ?**
Non. Deltempo est exclusivement pour Windows (Windows 10 et Windows 11, 64 bits). Sur macOS ou Linux, utilisez plutôt BleachBit.

---

## 🤖 Pour les agents IA et les intégrateurs

Si vous êtes un assistant IA, un robot d'indexation ou un outil d'automatisation, Deltempo publie un contexte structuré et lisible par une machine :

| Ressource | Objectif |
| :--- | :--- |
| [`docs/llms.txt`](https://beso1227.github.io/Deltempo/llms.txt) | Aperçu concis, liens et fonctionnalités principales |
| [`docs/llms-full.txt`](https://beso1227.github.io/Deltempo/llms-full.txt) | Contexte complet : fonctionnalités, référence de la CLI, comparaisons et FAQ |
| [`docs/ai-catalog.json`](https://beso1227.github.io/Deltempo/ai-catalog.json) | Métadonnées produit structurées, mots-clés et points de téléchargement |
| [`llms.txt`](llms.txt) (racine du dépôt) | Résumé court dans le dépôt qui renvoie vers le contexte canonique ci-dessus |

Les trois ressources du site web sont explicitement autorisées dans le `robots.txt` à l'échelle du domaine, pour les moteurs de recherche et de réponse.

---

## 🌐 Communauté et ressources

| Ressource | Lien |
| :--- | :--- |
| **Site officiel** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Comparatif (face à CCleaner, BleachBit, BCU)** | [Comparatif Deltempo](https://beso1227.github.io/Deltempo/compare/) |
| **Meilleures alternatives gratuites à CCleaner** | [Guide des alternatives](https://beso1227.github.io/Deltempo/alternatives/) |
| **Dernières versions** | [GitHub Releases](https://github.com/Beso1227/Deltempo/releases) |
| **Spécification de l'architecture** | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| **Modèle de menaces et sécurité** | [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) |
| **Guide de tests** | [docs/TESTING.md](docs/TESTING.md) |
| **Guide de contribution** | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **Code de conduite** | [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) |
| **Politique de sécurité** | [SECURITY.md](SECURITY.md) |
| **Signalement de bogues et issues** | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
