<div align="center">

  <a href="https://beso1227.github.io/Deltempo/">
    <img src="docs/social-preview.png" alt="Deltempo - Nettoyeur Windows de précision pure et optimiseur de mémoire" width="100%" />
  </a>

  <br />

  # Deltempo : nettoyeur Windows open-source, désinstalleur d'applications et optimiseur de mémoire

  <p align="center">
    <strong>README:</strong>
    <a href="README.md">English</a> ·
    <a href="README.es.md">Español</a> ·
    <a href="README.zh-CN.md">简体中文</a> ·
    <a href="README.hi.md">हिन्दी</a> ·
    <b>Français</b> ·
    <a href="README.pt-BR.md">Português</a> ·
    <a href="README.ar.md">العربية</a> ·
    <a href="README.ru.md">Русский</a> ·
    <a href="README.ja.md">日本語</a>
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
    <a href="docs/TESTING.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/tests-726_passed-00F2B0?style=for-the-badge&label=QUALITY&labelColor=16212B"><img alt="Tests : 726 réussis, 0 échec" src="https://img.shields.io/badge/tests-726_passed-00F2B0?style=for-the-badge&label=QUALITY" height="28" /></picture></a>
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
    <a href="docs/THREAT_MODEL.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY&labelColor=16212B"><img alt="Durci STRIDE" src="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md#safety-pipeline"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY&labelColor=16212B"><img alt="Sécurité vérifiée en deux phases" src="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY&labelColor=16212B"><img alt="Moteur de mémoire natif du noyau NT" src="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#quick-start"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP&labelColor=16212B"><img alt="Installation en une ligne" src="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github&labelColor=16212B"><img alt="Téléchargements GitHub" src="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github&labelColor=16212B"><img alt="Étoiles GitHub" src="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/pulls"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING&labelColor=16212B"><img alt="PR bienvenues" src="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING" height="28" /></picture></a>
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
| **Version** | v3.0.0 (version de production) |
| **Licence** | Open source ([MIT](LICENSE)) |
| **Interfaces** | Interface graphique de bureau moderne (WPF Fluent) et CLI en terminal sans interface |
| **Distribution** | Exécutable portable en fichier unique (autonome, sans installateur) |
| **Couverture des tests** | 726 tests automatisés (100 % de réussite, 0 échec, 0 ignoré), fuzzing adversarial du système de fichiers |
| **Disponibilité de la CLI** | La commande `deltempo` s'auto-installe au premier lancement de l'interface — aucune configuration manuelle |
| **Télémétrie** | Zéro télémétrie. Les opérations de scan, de nettoyage, de mémoire et de désinstallation s'exécutent 100 % hors ligne |
| **Moteur de sécurité** | Planification en deux phases (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) avec 5 niveaux de risque et journalisation des transactions |
| **Désinstalleur d'applications** | Désinstallation groupée silencieuse, moteur BCU, nettoyage des traces résiduelles AppData/Registre, purge forcée des applications défectueuses |
| **Points de restauration** | Points de restauration système Windows facultatifs avant désinstallation (contrôlés par l'utilisateur, désactivés par défaut) |
| **Intelligence des services** | Explique : *« Quelque chose risque-t-il de casser si je désactive ceci ? »* via 3 verdicts de sécurité, heuristiques hors ligne et IA multi-modèles |
| **Gestionnaire de processus** | Liste des processus en temps réel avec extraction en direct des icônes haute densité (high-DPI) des applications et analyse de l'empreinte mémoire |
| **Intégration WinUtil** | Lanceur en 1 clic de l'utilitaire Chris Titus Tech WinUtil (CTT) directement depuis la barre d'outils |
| **Moteur de mémoire** | Appels natifs au noyau NT Windows (`NtSetSystemInformation`, `EmptyWorkingSet`) |
| **Centre de préférences** | Centre de contrôle en 4 onglets catégorisés (*Mises à jour*, *Général*, *Mémoire*, *Stockage et sécurité*) |
| **Gardien de la zone de notification** | Icône Win32 native haute densité (`LoadCrispTrayIcon`) avec télémétrie RAM en direct et Boost en 1 clic |
| **Thèmes et accessibilité** | Mode clair Porcelain Slate au confort oculaire pro, mode sombre Obsidian et pleine sécurité de la disposition RTL arabe |
| **Mises à jour** | Canaux de version Stable et Beta vérifiés cryptographiquement (SHA-256) avec mise en préparation atomique |

---

## Qu'est-ce que Deltempo ?

**Deltempo** est une suite de maintenance Windows moderne, performante et open-source, conçue pour récupérer de l'espace de stockage en toute sécurité, désinstaller en profondeur les logiciels têtus, surveiller l'impact du démarrage au démarrage du système et optimiser la mémoire du système. Elle purge les caches d'applications jetables, les restes d'installateurs orphelins, les artefacts de compilation et les journaux système obsolètes sans toucher aux documents personnels, aux identifiants de navigateur ni aux composants critiques du système d'exploitation.

Les utilitaires de nettoyage traditionnels fonctionnent souvent comme des boîtes noires opaques, installent des logiciels publicitaires groupés ou laissent d'importantes résidus dans le registre. Deltempo est construite sur une **architecture où la sécurité prime** : les chemins candidats sont classés dans des niveaux de risque explicites, simulés avant suppression, bornés aux racines de répertoires autorisés et revalidés juste avant la suppression afin de se prémunir contre les conditions de concurrence du système de fichiers.

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
| **Points de restauration facultatifs** | **Optionnel (choix de l'utilisateur, désactivé par défaut)** | ⚠️ Automatisé / fonctionnalité payante | ❌ Aucun | Optionnel | Bascule système manuelle |
| **Intelligence des services de démarrage** | **Oui (« Quelque chose va-t-il casser ? » — verdicts à 3 niveaux)** | ❌ Simple liste d'activation/désactivation | ❌ Aucun | Liste détaillée du registre | Métriques de base du Gestionnaire des tâches |
| **Balayage des traces résiduelles** | **AppData, ProgramData, registre et raccourcis** | ⚠️ Pro payant uniquement | ❌ Aucun | ✅ Recherche manuelle dans le registre | ❌ Aucun |
| **Caches de développement et de shaders** | **NuGet, npm, pip, Cargo, Gradle, shaders GPU** | ❌ Navigateur et Windows uniquement | ⚠️ Partiel | ❌ Aucun | ❌ Aucun |
| **Détection des fichiers volumineux** | **Catégorisés par IA (>50 Mo, classés par risque)** | ❌ Recherche de fichiers basique | ❌ Aucun | ❌ Aucun | Répartition de base des disques |
| **Réparation des fichiers système** | **SFC, DISM et CHKDSK intégrés** | ❌ Utilitaire payant séparé | ❌ Aucun | ❌ Aucun | Invite de commandes manuelle |
| **Intégration WinUtil (Chris Titus)** | **Lanceur intégré élevé en 1 clic** | ❌ Aucun | ❌ Aucun | ❌ Aucun | ❌ Aucun |
| **Interface moderne et confort oculaire** | **Obsidian Dark et Porcelain Slate Light (WPF)** | Obsolète / surchargée | Interface GTK2/3 héritée | Interface WinForms héritée | Paramètres Windows intégrés |
| **Parité complète de la CLI** | **Oui (CLI `deltempo` avec `--json` et dry-run)** | ⚠️ Options de commande limitées | CLI basique | CLI basique | ❌ Aucun |
| **Distribution** | **Portable en fichier unique (68 Mo, sans installateur)** | Nécessite un installateur et des services | Installateur ou zip portable | Nécessite un installateur et un runtime | Intégré au système d'exploitation |

---

## 🌟 Tour d'horizon des fonctionnalités principales

### 1. 🧹 Nettoyeur de stockage de précision sur 26+ périmètres

Deltempo cible les données jetables dans les environnements système, de développement et de jeu sans toucher aux documents de l'utilisateur, aux jetons d'authentification actifs ni aux paramètres personnels :

* **Périmètres du système d'exploitation** : Temp utilisateur (`%TEMP%`), Temp Windows (`C:\Windows\Temp`), Prefetch, caches de téléchargement de Windows Update (`SoftwareDistribution\Download`), résidus de mise à niveau Windows (`$WINDOWS.~BT`), caches d'Optimisation de distribution, rapports d'erreur Windows (`WER`), vidages mémoire et caches de polices/vignettes.
* **Shaders GPU et jeux** : cache OTA NVIDIA App / GeForce Experience, cache AMD Radeon Software, caches de shaders DirectX (`D3DSCache`), pipelines Vulkan (`GLCache`), pré-mise en cache des shaders Steam et webcaches du lanceur Epic Games.
* **Écosystème des développeurs** : cache local NuGet v3, cache npm, cache pip, cache cible de Rust Cargo, caches Gradle, instantanés temporaires de l'émulateur Android Studio et caches des extensions VS Code.
* **Navigateurs modernes et communication** : répertoires de cache jetables des profils Chromium (Chrome, Edge, Brave, Opera, Vivaldi, Arc) et Gecko (Firefox) avec **sessions de connexion strictement préservées** ; caches multimédias de Discord, Slack et Spotify.
* **Bouclier de sécurité (>24 h)** : garde-fou optionnel qui exempte tout fichier créé ou modifié au cours des 24 dernières heures afin d'éviter les conflits avec des installateurs d'arrière-plan actifs ou des éditeurs en cours d'exécution.
* **Garde TOCTOU** : vérifie les limites, les attributs et les chemins canoniques des fichiers juste avant la suppression pour éliminer les conditions de concurrence.

---

### 2. 📦 Désinstalleur d'applications en profondeur et balayage des résidus

Dites adieu aux logiciels têtus, aux programmes à moitié désinstallés et aux assistants d'désinstallation en désordre :

* **Inventaire unifié des applications** : analyse les ruches de registre 64 bits et 32 bits (`HKLM`, `HKCU`) ainsi que les paquets modernes du Windows Store (AppX/UWP). Affiche la taille d'installation réelle, la vérification de l'éditeur, la version et les dates d'installation.
* **Point de restauration système facultatif** : contrairement à d'autres utilitaires qui imposent un point de restauration système lent de 2 minutes ou le sautent entièrement, Deltempo laisse le contrôle total à l'utilisateur. Un interrupteur dédié vous permet de décider de créer un point de contrôle avant désinstallation (**désactivé par défaut**).
* **Désinstallation groupée silencieuse de plusieurs applications** : sélectionnez plusieurs applications et lancez une désinstallation sans surveillance sans cliquer à travers des dizaines de boîtes de dialogue d'installateur répétitives.
* **Balayage approfondi des résidus** : une fois le désinstalleur d'une application terminé, l'analyseur de Deltempo traque les restes orphelins :
  * Branches du registre : `HKCU\Software\<Vendor>`, `HKLM\Software\<Vendor>`, `HKLM\Software\WOW6432Node\<Vendor>`.
  * Répertoires du système de fichiers : `%LocalAppData%\<App>`, `%AppData%\<App>`, `%ProgramData%\<App>`, `%ProgramFiles%\<App>`.
  * Entrées de démarrage et raccourcis orphelins du menu Démarrer.
* **Purge forcée des logiciels corrompus** : si un désinstalleur est cassé, introuvable ou génère des erreurs, Deltempo nettoie de force tous les répertoires associés et désenregistre proprement ses clés de registre.

---

### 3. ⚡ Optimiseur de mémoire natif du noyau NT Windows

Contrairement aux « nettoyeurs de RAM » grand public qui forcent simplement la mémoire dans le fichier d'échange et ralentissent votre PC, Deltempo utilise des appels système natifs et documentés du noyau NT Windows :

* **Invalidation de la liste d'attente** : appelle `NtSetSystemInformation` avec `SystemMemoryListInformation` (classe `80`) pour renvoyer les pages mémoire en attente inutilisées dans le pool disponible, au profit des tâches gourmandes (jeux, compilation, rendu).
* **Réduction des jeux de travail inactifs** : exploite `EmptyWorkingSet` avec des jetons de processus élevés (`SeProfileSingleProcessPrivilege` et `SeDebugPrivilege`) pour libérer les jeux de travail abandonnés des processus d'arrière-plan inactifs.
* **Bouclier des processus critiques** : les composants fondamentaux de Windows (`csrss.exe`, `dwm.exe`, `explorer.exe`, `lsass.exe`, `services.exe`, `smss.exe`, `svchost.exe` et Windows Defender) sont automatiquement protégés et jamais réduits.
* **Boost automatique en arrière-plan** : peut surveiller la pression mémoire en arrière-plan et déclencher automatiquement un nettoyage lorsque l'utilisation de la RAM physique dépasse un seuil défini par l'utilisateur (p. ex. 85 %).

---

### 4. 🧠 Gestionnaire de démarrage et intelligence des services

Ne vous demandez plus quels programmes ralentissent le démarrage de votre PC :

* **« Quelque chose risque-t-il de casser si je désactive ceci ? »** : chaque application de démarrage et service d'arrière-plan est analysé avec une badge de verdict intelligente à 3 niveaux :
  * 🟢 **SafeToDisable** : lanceurs de commodité, mises à jour de jeux et applications de communication qui n'ont pas besoin de démarrer avec Windows.
  * 🟡 **CautionNeeded** : panneaux de contrôle audio, utilitaires de pavé tactile ou logiciels de périphériques dont les raccourcis clavier ou les menus de la zone de notification peuvent devenir inactifs jusqu'à une ouverture manuelle.
  * 🔴 **EssentialKeep** : suites de sécurité, agents de sauvegarde et de synchronisation cloud ou pilotes matériels essentiels.
* **Pipeline d'intelligence double** :
  * **Heuristiques hors ligne** : classification déterministe instantanée basée sur les signatures numériques, les identités d'éditeurs vérifiées, les chemins binaires et les bases de données de processus connues.
  * **IA multi-fournisseurs facultative** : résumés opérationnels détaillés à la demande, prenant en charge OpenAI, Anthropic Claude, Google Gemini, Groq, OpenRouter et les modèles locaux hors ligne (Ollama, LM Studio).
* **Bascules de registre 100 % réversibles** : les éléments désactivés sont stockés en toute sécurité dans les clés de registre `Run_Deltempo_Disabled`. Tout élément peut être réactivé en un seul clic.

---

### 5. 🔍 Inspecteur de fichiers volumineux catégorisés par IA

Découvrez ce qui consomme réellement votre espace disque :

* **Analyse multi-disques** : analysez rapidement `C:\` ou tout autre disque fixe secondaire à la recherche de fichiers dépassant des seuils de taille personnalisables (>50 Mo, >100 Mo, >500 Mo, >1 Go).
* **Étiquetage automatique des catégories** : regroupe intelligemment les découvertes en archives (`.zip`, `.rar`, `.7z`), images disque (`.iso`, `.vhd`), disques de machine virtuelle (`.vmdk`, `.vhdx`), installateurs (`.msi`, `.exe`), médias vidéo/audio et fichiers journaux obsolètes.
* **Classement des risques de sécurité** : chaque fichier volumineux est évalué en sécurité avant que vous n'y touchiez, évitant ainsi la suppression accidentelle de disques d'hyperviseur ou d'installations de jeux importantes.

---

### 6. 🛠️ Réparation du système Windows et intégration CTT WinUtil

Diagnostiquez et réparez la corruption du système d'exploitation Windows directement depuis l'interface :

* **SFC (System File Checker)** : exécute `sfc /scannow` dans un contexte élevé pour réparer les fichiers système corrompus.
* **Maintenance DISM** : vérifie, analyse et restaure la santé du magasin de composants Windows (`/Cleanup-Image /RestoreHealth`).
* **Réinitialisation de base WinSxS** : nettoie les versions du magasin de composants remplacées pour récupérer des gigaoctets après les mises à jour majeures de Windows.
* **CHKDSK et réinitialisation réseau** : planifiez la vérification des volumes de disque au prochain démarrage ou videz le cache DNS et réinitialisez les piles Winsock en un clic.
* **Chris Titus Tech WinUtil (CTT)** : le lanceur intégré en 1 clic exécute la célèbre suite PowerShell WinUtil élevée pour le débloatage, la suppression de la télémétrie et la configuration automatisée des logiciels via winget.

---

### 7. 📊 Gestionnaire de processus en temps réel

* **Extraction native d'icônes haute densité** : extraction en direct d'icônes nettes 32 bits des exécutables à l'aide des routines Win32 natives `SHGetFileInfo` et `ExtractIconEx`.
* **Télémétrie mémoire et PID** : empreinte mémoire des processus en temps réel, identifiant de processus, informations de l'éditeur et chemin du fichier.
* **Terminaison sûre** : des routines de terminaison protégées empêchent l'arrêt accidentel des processus système critiques de Windows.

---

### 8. 🎨 Thèmes confort oculaire pro et support multilingue RTL

Conçu avec une attention obsessionnelle portée à l'expérience utilisateur :

* **Mode clair confort oculaire pro** : un thème apaisant porcelaine/ardoise Fluent/macOS (`#F1F5F9`) qui élimine complètement la fatigue oculaire, remplace les écrans blancs éblouissants et utilise des accents Ocean Azure (`#0284C7`) à fort contraste.
* **Mode sombre Obsidian** : thème sombre élégant et spatial avec des accents cyan électrique éclatants, une glassmorphism subtile et des cartes à double cadre.
* **Couverture multilingue complète** : traductions natives complètes en **anglais, arabe, espagnol, français et allemand**.
* **Disposition RTL et protection numérique** : le mode arabe active un véritable flux de fenêtres de droite à gauche (RTL) tout en imposant strictement un formatage de gauche à droite sur les métriques, les chemins et les indicateurs de progression (`0.0 MB`, `32%`, `C:\...`) afin que les chiffres et les statistiques de stockage ne soient jamais inversés ni altérés.

---

### 9. 🔔 Gardien parfaitement net de la zone de notification

* **Icône Win32 haute densité authentique (`LoadCrispTrayIcon`)** : utilise la création directe d'icônes GDI Win32 (`CreateIconIndirect`) avec transparence alpha ARGB 32 bits, offrant un rendu ultra-net sur les écrans à mise à l'échelle 100 %, 125 %, 150 %, 175 % et 200 %+ sans flou.
* **Télémétrie en direct au survol** : affiche l'utilisation mémoire en temps réel dans l'infobulle de la zone de notification : `RAM: 42% (13.4 GB / 31.9 GB)`.
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
```

### Résumé des commandes de la CLI

| Commande | Objectif | Drapeaux et options clés |
| :--- | :--- | :--- |
| `deltempo scan [filter]` | Analyser les cibles à la recherche de données jetables | `--json`, `--silent` |
| `deltempo clean [filter]` | Nettoyer les caches jetables | `--dry-run`, `--recycle-bin`, `--safe`, `--all`, `--json`, `--yes` |
| `deltempo smart-clean` | Purge rapide des caches vérifiés comme sûrs | `--dry-run`, `--json`, `--yes` |
| `deltempo deep-clean` | Nettoyage complet autonome (RAM, DISM, périmètres) | `--dry-run`, `--json`, `--yes` |
| `deltempo boost` | Optimiser la mémoire système via le noyau NT | `--all`, `--standby`, `--cache`, `--workingsets`, `--json` |
| `deltempo uninstall <app>` | Désinstallation approfondie et purge des résidus | `--dry-run`, `--force`, `--silent`, `--restore-point`, `--json` |
| `deltempo large [path]` | Analyser les disques à la recherche de fichiers gourmands en espace | `--min <size>`, `--type <cat>`, `--safe`, `--top <n>`, `--sort <size\|date>` |
| `deltempo large inspect <file>` | Inspecter le niveau de risque d'un fichier et son verdict de sécurité | `--json` |
| `deltempo large clean` | Envoyer les fichiers volumineux jetables à la corbeille | `--dry-run`, `--yes`, `--safe-only` |
| `deltempo startup [list]` | Inspecter les applications de démarrage et leur impact au boot | `--high`, `--json` |
| `deltempo startup disable <app>` | Désactiver réversiblement un programme de démarrage | N/A |
| `deltempo startup enable <app>` | Réactiver un programme de démarrage désactivé | N/A |
| `deltempo repair [subcommand]` | Vérification de l'intégrité Windows et réparation de la maintenance | `sfc`, `dism`, `winsxs`, `chkdsk`, `update`, `network` |
| `deltempo status` | Afficher la télémétrie système et les informations mémoire | `--json` |
| `deltempo test` | Auto-vérifier le moteur (API mémoire, périmètres détectés) | N/A |
| `deltempo help` | Afficher la référence complète des commandes | N/A |
| `deltempo update [check]` | Vérifier les versions officielles ou appliquer une mise à jour | `check`, `--dry-run` |
| `deltempo register` | Installer / inspecter la commande `deltempo` elle-même | `--status`, `--remove` |
| `deltempo unregister` | Supprimer tous les artefacts créés par `register` | N/A |

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

### Option 2 : Ligne unique en terminal (PowerShell)

Lancez la dernière version directement depuis votre terminal — sans navigateur, sans installateur :

```powershell
irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
```

Le script d'amorçage télécharge le dernier `Deltempo.exe`, vérifie son SHA-256 par rapport au `checksums.sha256` publié, le met en cache dans `%LOCALAPPDATA%\Deltempo\bin` et le lance. Pour le binaire CLI sans interface, utilisez le point d'entrée `win-cli` :

```powershell
irm https://beso1227.github.io/Deltempo/win-cli | Invoke-Expression
```

Si le manifeste ne peut pas être lu, ou si l'empreinte ne correspond pas, l'installateur abandonne et n'exécute rien de ce qu'il a téléchargé — la vérification n'est jamais sautée. Notez que `Invoke-Expression` n'accepte aucune argument : la cible est donc choisie par le point d'entrée plutôt que par une option `-Cli`.

### Option 3 : La commande `deltempo`

Vous n'avez jamais à configurer cela à la main. La première fois que vous lancez Deltempo, il provisionne
un binaire CLI de sous-système console, l'ajoute à votre `PATH` utilisateur, enregistre l'alias
<kbd>Win</kbd>+<kbd>R</kbd> et installe une fonction `deltempo` dans vos profils PowerShell — en réparant
au passage un enregistrement obsolète laissé par une ancienne installation s'il en trouve un.

Deux choses à savoir :

- **Ouvrez une nouvelle fenêtre de terminal.** Les shells déjà ouverts conservent le `PATH` avec lequel ils ont démarré.
- **Premier lancement hors ligne ?** Si le binaire console ne peut pas être provisionné, rien n'est enregistré
  et aucune commande à moitié installée ne subsiste. L'application de bureau n'est pas affectée — le prochain
  lancement réussi réessaie.

Vous préférez le gérer vous-même ? `deltempo register` fait la même chose à la demande, et
`deltempo unregister` supprime tous les artefacts qu'il a créés. Utilisez `deltempo register --status`
pour voir exactement ce qui est installé et quel binaire il pointe.

> Les commandes qui touchent à l'état système protégé (`restore-points`, l'étape DISM de `deep-clean`)
> renvoient une erreur claire et un code de sortie non nul lorsqu'elles sont exécutées depuis un terminal
> standard, non élevé. Exécutez-les depuis une invite d'administration, ou utilisez l'application de bureau.

---

<a id="privacy"></a>

## 🔒 Garantie de confidentialité et de sécurité

Deltempo est architecturée avec la sécurité et la confidentialité de l'utilisateur comme fondamentaux non négociables :

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
5. **Vérification cryptographique des versions** : les vérifications automatiques de mise à jour imposent HTTPS et vérifient les empreintes SHA-256 des binaires contre les manifestes de version GitHub signés.

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

# Exécuter la suite de tests automatisés (726 tests unitaires et d'intégration réussis)
dotnet test deltempo.sln -c Release

# Packager les binaires de release autonomes en fichier unique (GUI et CLI)
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

Les exécutables en fichier unique résultants seront publiés dans :

* `publish\Deltempo.exe` (GUI)
* `publish\deltempo_cli.exe` (CLI)

---

## 📜 Licence

Deltempo est un logiciel gratuit et open-source sous licence **[Licence MIT](LICENSE)**.

```text
Copyright (c) 2026 Beso1227 / Deltempo Project
```

---

## ❓ Foire aux questions

**Deltempo est-il vraiment gratuit ?**
Oui. Deltempo est entièrement gratuit et open source sous licence MIT — sans palier payant, sans ventes additionnelles, sans compte et sans publicité.

**Deltempo est-il une bonne alternative à CCleaner ?**
Oui. Deltempo est une alternative gratuite, open source et sans télémétrie à CCleaner qui inclut en plus un désinstalleur approfondi, un chercheur de fichiers en double, un détecteur de fichiers volumineux et la réparation du système Windows intégrée — sans aucune installation.

**Deltempo envoie-t-il des données de télémétrie ou me traque-t-il ?**
Non. Deltempo n'a aucune télémétrie : ni bibliothèques d'analyse, ni pixels de suivi, ni SDK publicitaires. Chaque opération s'exécute 100 % hors ligne.

**Deltempo nécessite-t-il un installateur ?**
Non. C'est un exécutable portable en fichier unique (~68 Mo). Exécutez `Deltempo.exe` directement sur place — rien n'est installé sur votre système.

**Le nettoyage des caches de navigateur va-t-il me déconnecter de mes comptes ?**
Non. Seuls les répertoires de cache jetables sont supprimés. Les sessions de connexion, les cookies et les mots de passe sont strictement préservés.

**Libère-t-il vraiment de la RAM, ou est-ce seulement cosmétique ?**
Deltempo utilise de vraies API du noyau NT — purge de la liste d'attente via `NtSetSystemInformation` et `EmptyWorkingSet` par processus — qui libèrent réellement de la mémoire physique, contrairement aux outils qui ne font que réduire le jeu de travail pour embellir les gestionnaires de tâches.

**Deltempo crée-t-il un point de restauration système automatiquement ?**
Non, pas par défaut. La création de point de restauration est une option explicite, si bien que vous n'êtes jamais ralenti par une sauvegarde forcée de plusieurs minutes.

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

Les trois sont explicitement autorisés dans `robots.txt` pour les moteurs de recherche et de réponse.

---

## 🌐 Communauté et ressources

| Ressource | Lien |
| :--- | :--- |
| **Site officiel** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Dernières versions** | [GitHub Releases](https://github.com/Beso1227/Deltempo/releases) |
| **Spécification de l'architecture** | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| **Modèle de menaces et sécurité** | [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) |
| **Guide de tests** | [docs/TESTING.md](docs/TESTING.md) |
| **Guide de contribution** | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **Politique de sécurité** | [SECURITY.md](SECURITY.md) |
| **Signalement de bogues et issues** | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
