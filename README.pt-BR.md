<div align="center">

  <a href="https://beso1227.github.io/Deltempo/">
    <img src="docs/social-preview.png" alt="Deltempo — Limpador de Windows de precisão pura e otimizador de memória" width="100%" />
  </a>

  <br />

  # Deltempo: Limpador de Windows de código aberto, desinstalador de aplicativos e otimizador de memória

  <p align="center">
    <strong>README:</strong>
    <a href="README.md">🇬🇧 English</a> ·
    <a href="README.es.md">🇪🇸 Español</a> ·
    <a href="README.zh-CN.md">🇨🇳 简体中文</a> ·
    <a href="README.hi.md">🇮🇳 हिन्दी</a> ·
    <a href="README.fr.md">🇫🇷 Français</a> ·
    <b>🇧🇷 Português</b> ·
    <a href="README.ar.md">🇸🇦 العربية</a> ·
    <a href="README.ru.md">🇷🇺 Русский</a> ·
    <a href="README.ja.md">🇯🇵 日本語</a>
  </p>

  <p><strong>Limpeza de disco e otimizador de RAM gratuitos e de código aberto para Windows 10 &amp; 11. Recupere de 10 a mais de 40 GB de arquivos temporários, lixo do AppData e caches de shaders de GPU — além de um desinstalador profundo de aplicativos e um localizador de arquivos duplicados. Zero telemetria, sem anúncios, sem instalador.</strong></p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/Deltempo.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48"><img alt="Baixe o Deltempo mais recente para Windows" src="https://img.shields.io/badge/DOWNLOAD%20FOR%20WINDOWS-00F2B0?style=for-the-badge&label=DELTEMPO&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://beso1227.github.io/Deltempo/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48"><img alt="Visite o site oficial do Deltempo" src="https://img.shields.io/badge/OFFICIAL%20WEBSITE-8B5CF6?style=for-the-badge&label=VISIT&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest/download/deltempo_cli.exe"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48"><img alt="Baixe a CLI headless do Deltempo" src="https://img.shields.io/badge/CLI%20INSTALLER-0DD3BA?style=for-the-badge&label=HEADLESS&labelColor=16212B&height=48" height="48" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48"><img alt="Dê uma estrela ao Deltempo no GitHub" src="https://img.shields.io/badge/STAR%20THE%20REPO-F59E0B?style=for-the-badge&label=GITHUB&labelColor=16212B&height=48" height="48" /></picture></a>
  </p>

  <p>
    <sub>Prefere o terminal? Uma única linha instala e verifica a versão mais recente &mdash;
      <code>irm https://beso1227.github.io/Deltempo/win | Invoke-Expression</code>
      &middot; Encontrou um bug ou tem uma ideia de funcionalidade? <a href="https://github.com/Beso1227/Deltempo/issues/new/choose"><strong>Abra uma issue</strong></a> &mdash; todo pedido é lido.
      Se o Deltempo devolveu espaço em disco para você, uma <a href="https://github.com/Beso1227/Deltempo"><strong>estrela</strong></a> realmente ajuda outras pessoas a encontrá-lo.</sub>
  </p>

  <p>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0&labelColor=16212B"><img alt="Versão mais recente" src="https://img.shields.io/github/v/release/Beso1227/Deltempo?style=for-the-badge&label=RELEASE&color=00F2B0" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/actions/workflows/ci.yml"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github&labelColor=16212B"><img alt="Status da compilação no CI" src="https://img.shields.io/github/actions/workflow/status/Beso1227/Deltempo/ci.yml?style=for-the-badge&label=CI&logo=github" height="28" /></picture></a>
    <a href="docs/TESTING.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/tests-1505_passed-00F2B0?style=for-the-badge&label=QUALITY&labelColor=16212B"><img alt="Testes: 1505 aprovados, 0 falhas" src="https://img.shields.io/badge/tests-1505_passed-00F2B0?style=for-the-badge&label=QUALITY" height="28" /></picture></a>
    <a href="LICENSE"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github&labelColor=16212B"><img alt="Licença: MIT" src="https://img.shields.io/github/license/Beso1227/Deltempo?style=for-the-badge&label=LICENSE&color=00F2B0&logo=github" height="28" /></picture></a>
  </p>

  <p>
    <a href="https://dotnet.microsoft.com/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white&labelColor=16212B"><img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&label=RUNTIME&logo=dotnet&logoColor=white" height="28" /></picture></a>
    <a href="https://learn.microsoft.com/dotnet/csharp/"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white&labelColor=16212B"><img alt="C# 14" src="https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&label=LANGUAGE&logo=csharp&logoColor=white" height="28" /></picture></a>
    <a href="#at-a-glance"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows&labelColor=16212B"><img alt="Plataformas suportadas" src="https://img.shields.io/badge/windows_10_%2F_11_x64-0078D4?style=for-the-badge&label=PLATFORM&logo=windows" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases/latest"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY&labelColor=16212B"><img alt="Arquivo único portátil" src="https://img.shields.io/badge/portable_single--file-F59E0B?style=for-the-badge&label=BINARY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#privacy"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY&labelColor=16212B"><img alt="Zero telemetria, 100% offline" src="https://img.shields.io/badge/zero_telemetry-00F2B0?style=for-the-badge&label=PRIVACY" height="28" /></picture></a>
    <a href="docs/THREAT_MODEL.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY&labelColor=16212B"><img alt="Reforçado com STRIDE" src="https://img.shields.io/badge/STRIDE_hardened-8B5CF6?style=for-the-badge&label=SECURITY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md#safety-pipeline"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY&labelColor=16212B"><img alt="Segurança verificada em duas fases" src="https://img.shields.io/badge/two--phase_verified-8B5CF6?style=for-the-badge&label=SAFETY" height="28" /></picture></a>
    <a href="docs/ARCHITECTURE.md"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY&labelColor=16212B"><img alt="Motor de memória nativo do kernel NT" src="https://img.shields.io/badge/NT_kernel_native-0DD3BA?style=for-the-badge&label=MEMORY" height="28" /></picture></a>
  </p>

  <p>
    <a href="#quick-start"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP&labelColor=16212B"><img alt="Instalação em uma linha" src="https://img.shields.io/badge/one--line_install-00F2B0?style=for-the-badge&label=SETUP" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/releases"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github&labelColor=16212B"><img alt="Downloads do GitHub" src="https://img.shields.io/github/downloads/Beso1227/Deltempo/total?style=for-the-badge&label=DOWNLOADS&color=00F2B0&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github&labelColor=16212B"><img alt="Estrelas do GitHub" src="https://img.shields.io/github/stars/Beso1227/Deltempo?style=for-the-badge&label=STARS&color=F59E0B&logo=github" height="28" /></picture></a>
    <a href="https://github.com/Beso1227/Deltempo/pulls"><picture><source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING&labelColor=16212B"><img alt="PRs bem-vindos" src="https://img.shields.io/badge/PRs_welcome-00F2B0?style=for-the-badge&label=CONTRIBUTING" height="28" /></picture></a>
  </p>

  <p>
    <sub>
      <a href="docs/ARCHITECTURE.md">Arquitetura</a> &middot;
      <a href="docs/THREAT_MODEL.md">Modelo de Ameaças</a> &middot;
      <a href="docs/TESTING.md">Guia de Testes</a> &middot;
      <a href="docs/BENCHMARKS.md">Benchmarks</a> &middot;
      <a href="docs/RELEASES.md">Engenharia de Releases</a> &middot;
      <a href="SECURITY.md">Política de Segurança</a> &middot;
      <a href="#quick-start">Início Rápido</a>
    </sub>
  </p>

  <br />

  <video src="docs/deltempo.mp4" poster="docs/deltempo.webp" controls preload="metadata" width="100%"></video>

</div>

---

  <a id="at-a-glance"></a>

## Visão Geral

| Propriedade | Detalhe |
| :--- | :--- |
| **Site oficial** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Plataforma** | Windows 10 e 11 (64 bits / x64) |
| **Versão** | v3.2.0 (release de produção) |
| **Licença** | Código aberto ([MIT](LICENSE)) |
| **Interfaces** | GUI moderna para desktop (WPF Fluent) e CLI de terminal headless |
| **Distribuição** | Executável portátil de arquivo único (autossuficiente, sem necessidade de instalador) |
| **Cobertura de testes** | 1505 testes automatizados (100% de aprovação, 0 falhas, 0 ignorados), fuzzing adversarial de sistema de arquivos |
| **Disponibilidade da CLI** | O comando `deltempo` se autoinstala na primeira inicialização da GUI — nenhuma configuração manual |
| **Telemetria** | Zero telemetria. As operações de varredura, limpeza, memória e desinstalação são executadas 100% offline |
| **Motor de segurança** | Planejamento em duas fases (`SCAN → PLAN → PROTECT → REVALIDATE → CLEAN`) com 5 níveis de risco e log transacional das operações |
| **Recusas deliberadas** | O Deltempo nunca oferece como alvo caminhos que parecem caches, mas são indispensáveis: caches de origem MSI, instalações do SDK do .NET por usuário, raízes de fabricante de drivers, `registry\src` do Cargo e os dados de recuperação de sessões do Audacity. Veja [§2.1](#21-what-deltempo-refuses-to-delete). |
| **Relatório de pegada do AppData** | Relatório somente leitura do espaço ocupado no AppData, do maior para o menor, tanto no aplicativo quanto via `deltempo appdata-report`. Ele apenas relata: não existe caminho de exclusão em nenhuma das duas superfícies. |
| **Desinstalador de aplicativos** | Desinstalação silenciosa em massa, motor BCU, limpeza de resíduos de AppData/registro, limpeza forçada de aplicativos corrompidos |
| **Pontos de restauração** | Pontos de Restauração do Sistema do Windows opcionais antes da desinstalação (controlados pelo usuário, desativados por padrão) |
| **Inteligência de serviços** | Explica: *"Algo pode dar errado se eu desativar isso?"* por meio de 3 vereditos de segurança, heurísticas offline e IA multimodelo |
| **Gerenciador de processos** | Listagem de processos em tempo real com extração ao vivo de ícones de aplicativos em alta DPI e análise do uso de memória |
| **Integração com WinUtil** | Lançador de 1 clique para o [Chris Titus Tech WinUtil](https://christitus.com/win) (CTT), de Chris Titus |
| **Motor de memória** | Chamadas nativas do kernel NT do Windows (`NtSetSystemInformation`, `EmptyWorkingSet`) |
| **Central de preferências** | Central de controle com 4 abas categorizadas (*Atualizações*, *Geral*, *Memória*, *Armazenamento e Segurança*) |
| **Guardião da bandeja** | Ícone Win32 nativo de alta DPI (`LoadCrispTrayIcon`) com telemetria de RAM ao vivo e Boost com 1 clique |
| **Temas e acessibilidade** | Modo claro Porcelain Slate confortável para os olhos, modo escuro Obsidian e segurança total no layout RTL do idioma árabe |
| **Atualizações** | Canais de release Stable e Beta verificados criptografamente (SHA-256) com preparação atômica |

---

## O que é o Deltempo?

**Deltempo** é uma suíte de manutenção do Windows moderna, de alto desempenho e de código aberto, projetada para recuperar espaço de armazenamento com segurança, desinstalar por completo software teimoso, monitorar o impacto dos programas na inicialização e otimizar a memória do sistema. Ela remove caches descartáveis de aplicativos, restos órfãos de instaladores, artefatos de build e logs de sistema obsoletos sem tocar em documentos pessoais, credenciais do navegador nem em componentes críticos do sistema operacional.

As utilidades de limpeza tradicionais frequentemente funcionam como caixas-pretas opacas, instalam adware empacotado ou deixam resíduos profundos no registro. O Deltempo foi projetado com uma **arquitetura que prioriza a segurança**: os caminhos candidatos são classificados em níveis de risco explícitos, simulados antes da exclusão, limitados às raízes de diretórios autorizados e revalidados imediatamente antes da remoção para proteger contra condições de corrida no sistema de arquivos.

Além da limpeza de disco, o Deltempo inclui ferramentas de baixo nível de gerenciamento de memória do kernel NT do Windows para esvaziar listas de páginas em espera e aparar conjuntos de trabalho inativos por meio de chamadas de sistema Win32 e NT oficiais.

---

## ⚔️ Como o Deltempo se compara aos concorrentes

A maioria das utilidades de limpeza e otimização do Windows empacota adware comercial, exige serviços em segundo plano invasivos, restringe recursos essenciais a assinaturas pagas ou depende de bases de código desatualizadas.

O Deltempo é totalmente de código aberto, contém zero telemetria, não requer instalação e oferece uma suíte ponta a ponta que combina limpeza de precisão, desinstalação profunda de software, gerenciamento de memória em nível de kernel e inteligência de serviços de inicialização.

| Capacidade / Recurso | **Deltempo** | **CCleaner** | **BleachBit** | **Bulk Crap Uninstaller** | **Windows Storage Sense** |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Licença e base de código** | **Código aberto MIT (C# 14 / .NET 10)** | Proprietária / Comercial | Código aberto GPLv3 (Python/GTK) | Código aberto Apache 2.0 (.NET) | Proprietária (Microsoft) |
| **Telemetria e privacidade** | **Zero telemetria (100% offline)** | ⚠️ Rastreadores e coleta de dados | ✅ Zero telemetria | Telemetria mínima | Telemetria de diagnóstico do Windows |
| **Adware empacotado / ofertas extras** | **Nenhum / Nunca** | ⚠️ Adware e ofertas extras no histórico | Nenhum | Nenhum | Nenhum |
| **Motor de memória** | **Kernel NT nativo (`NtSetSystemInformation`)** | ⚠️ Básico (somente no Pro pago) | ❌ Nenhum | ❌ Nenhum | ❌ Nenhum |
| **Desinstalador profundo de aplicativos** | **Remoção silenciosa em massa + erradicação de resíduos** | ⚠️ Básico (profundo no Pro pago) | ❌ Nenhum | ✅ Abrangente | ❌ Apenas Adicionar/Remover básico |
| **Pontos de restauração opcionais** | **Opcional (escolha do usuário, desativado por padrão)** | ⚠️ Automatizado / recurso pago | ❌ Nenhum | Opcional | Chave manual do sistema |
| **Inteligência de serviços de inicialização** | **Sim ("Algo pode quebrar?" vereditos em 3 níveis)** | ❌ Lista simples de chaves liga/desliga | ❌ Nenhum | Lista detalhada do registro | Métricas básicas do Gerenciador de Tarefas |
| **Varredor de resíduos** | **AppData, ProgramData, registro e atalhos** | ⚠️ Somente no Pro pago | ❌ Nenhum | ✅ Busca manual no registro | ❌ Nenhum |
| **Caches de desenvolvedor e shaders** | **NuGet, npm, pip, Cargo, Gradle, shaders de GPU** | ❌ Apenas navegador e Windows | ⚠️ Parcial | ❌ Nenhum | ❌ Nenhum |
| **Descoberta de arquivos grandes** | **Categorizados por IA (>50 MB, classificados por risco)** | ❌ Busca básica de arquivos | ❌ Nenhum | ❌ Nenhum | Detalhamento básico das unidades |
| **Reparo de arquivos do sistema** | **SFC, DISM e CHKDSK integrados** | ❌ Utilidade paga separada | ❌ Nenhum | ❌ Nenhum | Prompt de Comando manual |
| **Integração com o WinUtil (Chris Titus)** | **Lançador elevado em 1 clique** (de terceiros, por Chris Titus) | ❌ Nenhum | ❌ Nenhum | ❌ Nenhum | ❌ Nenhum |
| **UI moderna e conforto visual** | **Obsidian Dark e Porcelain Slate Light (WPF)** | Desatualizada / Sobrecarregada | Interface antiga em GTK2/3 | Interface antiga em WinForms | Configurações integradas do Windows |
| **Paridade total com a CLI** | **Sim (CLI `deltempo` com `--json` e dry-run)** | ⚠️ Opções de comando limitadas | CLI básica | CLI básica | ❌ Nenhuma |
| **Distribuição** | **Arquivo único portátil (68 MB, sem instalador)** | Requer instalador e serviços | Instalador ou zip portátil | Requer instalador e runtime | Integrado ao SO |

---
## 🌟 Aprofundamento nos Recursos Principais

### 1. 🧹 Limpeza de Armazenamento de Precisão com 26+ Escopos

O Deltempo tem como alvo dados descartáveis nos ambientes de sistema, desenvolvedor e jogos sem tocar em documentos do usuário, tokens de autenticação ativos nem configurações pessoais:

* **Escopos do Sistema Operacional**: Temp do Usuário (`%TEMP%`), Windows Temp (`C:\Windows\Temp`), Prefetch, caches de download do Windows Update (`SoftwareDistribution\Download`), resíduos de atualização do Windows (`$WINDOWS.~BT`), caches de Otimização de Entrega, Windows Error Reporting (`WER`), Memory Dumps e caches de Fontes/Thumbnails.
* **Shaders de GPU e Jogos**: Cache OTA do NVIDIA App / GeForce Experience, cache do AMD Radeon Software, caches de shaders DirectX (`D3DSCache`), pipelines Vulkan (`GLCache`), pré-cache de shaders do Steam e caches web do lançador da Epic Games.
* **Ecossistema de Desenvolvimento**: Cache local NuGet v3, cache npm, cache pip, cache de compilação do Cargo (Rust), caches do Gradle, snapshots temporários do emulador do Android Studio e caches de extensões do VS Code.
* **Navegadores Modernos e Comunicação**: Diretórios de cache descartáveis dos perfis Chromium (Chrome, Edge, Brave, Opera, Vivaldi, Arc) e Gecko (Firefox), com **sessões de login estritamente preservadas**; caches de mídia do Discord, Slack e Spotify.
* **Escudo de Segurança (>24h)**: Proteção opcional que isenta qualquer arquivo criado ou modificado nas últimas 24 horas para evitar conflito com instaladores ativos em segundo plano ou editores em execução.
* **Guarda TOCTOU**: Verifica limites de arquivo, atributos e caminhos canônicos imediatamente antes da remoção para eliminar condições de corrida.
* **Detecção de aplicativos órfãos da Store**: Diretórios do AppData cujo pacote MSIX correspondente não está mais instalado são identificados a partir de evidências binárias do repositório `AppModel` — nome de família do pacote analisado, comparado pelo ID do publicador — em vez de heurísticas baseadas no nome do diretório. Os achados são reportados como `LowRisk` e nunca são pré-selecionados para exclusão.

<a id="21-what-deltempo-refuses-to-delete"></a>

### 2.1 O que o Deltempo se recusa a apagar

Um limpador que só sabe o que é seguro remover é perigoso, porque "temporário" e "descartável" muitas vezes são a mesma mentira. Vários caminhos que superficialmente parecem exatamente rascunhos de build ou download são, na verdade, indispensáveis, e o Deltempo os recusa por construção — eles estão ausentes dos resolvedores de escopo e são independentemente negados pelo `ProtectionPolicy`, de modo que nenhuma configuração ou opção de CLI pode oferecê-los:

| Caminho recusado | Por que apagá-lo é destrutivo |
| :--- | :--- |
| `%ProgramData%\Package Cache`<br>`%WinDir%\Installer\$PatchCache$` | Os caches de origem MSI de que o `msiexec` precisa para **reparar** e **desinstalar** produtos instalados. Removê-los quebra permanentemente o caminho de Modificar/Reparar e de desinstalação em Programas e Recursos em toda a máquina. |
| `%LOCALAPPDATA%\Microsoft\dotnet` | A **raiz de instalação** por usuário do SDK e do runtime do .NET (hosts `dotnet.exe`, `sdk\`, `packs\`, `shared\`), não um cache. Apenas o rascunho transitório de build em `%TEMP%\.net` é descartável. |
| `C:\NVIDIA`, `C:\AMD`, `C:\Intel`, `C:\Realtek` | As raízes de fabricante em que um instalador de driver descompacta o conteúdo. Uma execução de limpeza no meio da instalação apagaria arquivos que o instalador ainda não consumiu e não consegue obter de novo. Apenas subdiretórios de staging específicos são alvos. |
| `~\.cargo\registry\src` | O código-fonte de crates **extraído** contra o qual `cargo build --offline` compila. O diretório irmão `registry\cache` é limpo; `src` não é, porque limpar um sem o outro é uma operação tudo-ou-nada que este modelo por alvo não consegue expressar com segurança. |
| `%LOCALAPPDATA%\Audacity\SessionData` | Dados de **recuperação** de sessões não salvas, não rascunhos. Apagá-los destrói gravações em andamento que nunca foram salvas. |
| `%ProgramData%\Microsoft\Windows Defender\Definition Updates\Backup` | Cópias de **reversão** das assinaturas do Defender. Removê-las elimina a possibilidade de reverter uma atualização de definições ruim. |
| `%LOCALAPPDATA%\Electronic Arts\EA Desktop\UserConfig` | Configurações do aplicativo EA Desktop, não um cache. |

Esses caminhos são recusados duas vezes: eles são removidos dos resolvedores de escopo, *e* o `ProtectionPolicy` os nega de forma independente. Portanto, uma regressão em um resolvedor, um pacote de regras fornecido pelo usuário ou uma entrada de configuração obsoleta não consegue reintroduzi-los.

Esta lista é aplicada em código e coberta por `CleanerServiceTests` — cada item recusado tem um teste que afirma que o caminho está ausente *e* uma verificação equivalente que prova que o rascunho legítimo da mesma lista ainda está presente, de modo que a correção não possa se degenerar em uma remoção ampla demais.

---

### 2. 📦 Desinstalador Profundo de Aplicativos e Varredura de Resíduos

Adeus ao bloatware teimoso, ao software meio apagado e aos assistentes bagunçados de desinstalação:

* **Inventário Unificado de Aplicações**: Analisa os hives de registro de 64 bits e 32 bits (`HKLM`, `HKCU`) além dos pacotes modernos do Windows Store (AppX/UWP). Exibe o tamanho real de instalação, verificação do publicador, versão e datas de instalação.
* **Ponto de Restauração do Sistema Opcional**: Diferente de outros utilitários que forçam um ponto de restauração lento de 2 minutos ou o ignoram por completo, o Deltempo dá controle total ao usuário. Uma opção na interface permite decidir se deseja criar um ponto de restauração pré-desinstalação (**desativada por padrão**).
* **Remoção Silenciosa em Massa de Vários Aplicativos**: Selecione vários aplicativos e dispare a desinstalação automática, sem precisar passar por dezenas de diálogos repetitivos de instalador.
* **Varredura Profunda de Resíduos**: Quando o desinstalador de um aplicativo termina, o scanner do Deltempo identifica os vestígios órfãos:
  * Ramos do registro: `HKCU\Software\<Fabricante>`, `HKLM\Software\<Fabricante>`, `HKLM\Software\WOW6432Node\<Fabricante>`.
  * Diretórios do sistema de arquivos: `%LocalAppData%\<App>`, `%AppData%\<App>`, `%ProgramData%\<App>`, `%ProgramFiles%\<App>`.
  * Entradas de inicialização e atalhos órfãos do Menu Iniciar.
* **Limpeza Forçada para Software Corrompido**: Se um desinstalador estiver quebrado, ausente ou lançar erros, o Deltempo limpa forçosamente todos os diretórios relacionados e remove suas chaves de registro de forma limpa.

---

### 3. ⚡ Otimizador de Memória Nativo do Kernel NT do Windows

Diferente dos "limpadores de RAM" voltados ao consumidor comum, que simplesmente empurram a memória para o arquivo de swap e deixam seu PC mais lento, o Deltempo utiliza chamadas de sistema nativas e documentadas do kernel NT do Windows:

* **Invalidação da Lista em Espera**: Chama `NtSetSystemInformation` com `SystemMemoryListInformation` (classe `80`) para devolver ao pool disponível as páginas de memória em espera que não estão em uso, dedicate a tarefas de alta demanda (jogos, compilação, renderização).
* **Aparar Conjuntos de Trabalho Inativos**: Utiliza `EmptyWorkingSet` com tokens de processo com privilégios de elevação (`SeProfileSingleProcessPrivilege` e `SeDebugPrivilege`) para liberar os conjuntos de trabalho abandonados de processos inativos em segundo plano.
* **Escudo de Processos Críticos**: Componentes centrais do Windows (`csrss.exe`, `dwm.exe`, `explorer.exe`, `lsass.exe`, `services.exe`, `smss.exe`, `svchost.exe` e o Windows Defender) são automaticamente protegidos e nunca aparados.
* **Auto-Boost em Segundo Plano**: Pode monitorar a pressão de memória em segundo plano e acionar automaticamente uma limpeza quando o uso de RAM física ultrapassar um limite definido pelo usuário (por exemplo, 85%).

---

### 4. 🧠 Gerenciador de Inicialização e Inteligência de Serviços

Pare de se perguntar quais programas estão desacelerando a inicialização do seu PC:

* **"Algo pode dar errado se eu desativar isso?"**: Cada aplicativo de inicialização e serviço em segundo plano é analisado com um selo de veredito em 3 níveis:
  * 🟢 **SafeToDisable**: Lançadores de conveniência, atualizadores de jogos e aplicativos de comunicação que não precisam iniciar com o Windows.
  * 🟡 **CautionNeeded**: Painéis de controle de áudio, utilitários de touchpad ou software de periféricos cujos atalhos ou menus de bandeja podem ficar inativos até serem abertos manualmente.
  * 🔴 **EssentialKeep**: Suítes de segurança, agentes de backup em nuvem ou drivers essenciais de hardware.
* **Pipeline de Inteligência Dupla**:
  * **Heurísticas Offline**: Classificação determinística instantânea baseada em assinaturas digitais, identidades de fabricantes verificadas, caminhos de binários e bancos de dados de processos conhecidos.
  * **IA Opcional com Múltiplos Provedores**: Resumos operacionais detalhados sob demanda com suporte a OpenAI, Anthropic Claude, Google Gemini, Groq, OpenRouter e modelos locais offline (Ollama, LM Studio).
* **Chaves de registro 100% Reversíveis**: Os itens desativados são armazenados com segurança nas chaves de registro `Run_Deltempo_Disabled`. Qualquer item pode ser reativado com um único clique.

---

### 5. 🔍 Inspetor de Arquivos Grandes Categorizado por IA

Descubra o que realmente está consumindo o espaço do seu disco:

* **Varredura de Múltiplas Unidades**: Analise rapidamente `C:\` ou qualquer outra unidade fixa secundária em busca de arquivos que excedam limites de tamanho personalizáveis (>50 MB, >100 MB, >500 MB, >1 GB).
* **Classificação Automática por Categoria**: Agrupa de forma inteligente os arquivos encontrados em Arquivos Comprimidos (`.zip`, `.rar`, `.7z`), Imagens de Disco (`.iso`, `.vhd`), Discos de Máquina Virtual (`.vmdk`, `.vhdx`), Instaladores (`.msi`, `.exe`), mídias de Vídeo/Áudio e arquivos de log obsoletos.
* **Classificação de Risco de Segurança**: Cada arquivo grande é avaliado quanto à segurança antes de você tocá-lo, impedindo a exclusão acidental de discos de hipervisor ou instalações importantes de jogos.

---

### 6. 🛠️ Reparo do Sistema Windows e Integração com o CTT WinUtil

Diagnostique e repare a corrupção do sistema operacional Windows diretamente pela interface:

* **SFC (System File Checker)**: Executa `sfc /scannow` em um contexto elevado para reparar arquivos do sistema corrompidos.
* **Manutenção via DISM**: Verifica, examina e restaura a integridade do Component Store do Windows (`/Cleanup-Image /RestoreHealth`).
* **Redefinição da Base do WinSxS**: Limpa as versões superadas do Component Store para recuperar gigabytes após grandes atualizações do Windows.
* **CHKDSK e Reset de Rede**: Agende a verificação do volume de disco na próxima inicialização ou limpe o cache do DNS e redefina as pilhas do Winsock com um clique.
 * **Chris Titus Tech WinUtil (CTT)**: O lançador de 1 clique abre o WinUtil em uma sessão elevada do PowerShell para a remoção de bloatware, a remoção de telemetria e a configuração automatizada do winget. O WinUtil é uma **utilidade de terceiros de [Chris Titus](https://christitus.com)**: não faz parte do Deltempo e não é coberto pela auditoria do Deltempo. Veja [Avisos de terceiros](THIRD-PARTY-NOTICES.md).

---

### 7. 📊 Gerenciador de Processos em Tempo Real

* **Extração Nativa de Ícones em Alta DPI**: Extração ao vivo de ícones executáveis nítidos de 32 bits usando as rotinas nativas Win32 `SHGetFileInfo` e `ExtractIconEx`.
* **Telemetria de Memória e PID**: Uso de memória dos processos em tempo real, ID do processo, informações do publicador e caminho do arquivo.
* **Terminação Segura**: Rotinas de encerramento protegidas impedem a terminação acidental de processos críticos do sistema Windows.

---

### 8. 📊 Relatório de Pegada do AppData e Detecção de Aplicativos Órfãos da Store

`%LOCALAPPDATA%` e `%APPDATA%` são os maiores e menos compreendidos consumidores de espaço em disco em uma máquina Windows típica, e é também onde moram os erros mais destrutivos. O Deltempo os trata como somente leitura por padrão.

* **Relatório de pegada somente leitura**: Um detalhamento do maior para o menor do que realmente ocupa o AppData, acessível pelo botão **AppData Report** no aplicativo e por `deltempo appdata-report` na CLI. Ele mede, ordena e exibe. Não existe `--clean`, nem `--yes`, nem nenhum caminho de exclusão em nenhuma das duas superfícies — deliberadamente, e não por acaso.
* **Proteção contra pontos de reanálise**: As junções de compatibilidade antigas do AppData (`Application Data`, `Local Settings`) apontam de volta para `Local`. Atravessá-las relê a mesma árvore e infla o total, por isso são ignoradas. Em um perfil real, a correção levou uma leitura de 59.5 GB para o valor real de 32.6 GB em 123 pastas.
* **Aplicativos Microsoft Store órfãos**: O `StoreAppOrphanResolver` lê o repositório de pacotes do AppModel (os hives `HKCU` e `HKLM`) como evidência binária e faz a correspondência entre cada pasta de `LOCALAPPDATA\Packages` e o pacote MSIX que a possuía. O nome da pasta (`Name_PublisherId`) é conferido com a chave do repositório (`Name_Version_Arch_ResourceId_PublisherId`), dividindo o nome no último sublinhado e comparando pelo ID do publicador e pelo prefixo do nome. Se nenhum dos hives puder ser aberto, o resolvedor não retorna nada em vez de adivinhar. Os órfãos aparecem como `LowRisk`, não selecionados, e o `ProtectionPolicy` continua sendo a única fonte de verdade para as recusas.

> **Uma resposta correta pode ser "nada".** Em uma máquina sem aplicativos da Store removidos, o relatório retorna zero órfãos. Esse é o comportamento pretendido: o recurso é julgado pela ausência de falsos positivos, não pela quantidade de resultados.

### 9. 🎨 Temas Profissionais de Conforto Visual e Suporte Multilíngue RTL

Projetado com atenção obsessiva à experiência do usuário:

* **Modo Claro Profissional de Conforto Visual**: Um tema suave de porcelana e ardósia estilo Fluent/macOS (`#F1F5F9`) que elimina completamente a fadiga visual, substitui telas brancas ofuscantes e usa destaques de alto contraste em Azul Oceano (`#0284C7`).
* **Modo Escuro Obsidian**: Tema escuro elegante de espaço profundo, com destaques vibrantes em ciano elétrico, efeito glassmorphism sutil e cartões com moldura dupla.
* **Cobertura Multilíngue (em andamento)**: Seletor de idioma para **inglês, árabe, espanhol, francês e alemão**. A área de limpeza já está traduzida e o árabe tem fluxo RTL nativo; os modais e as demais áreas ainda não foram traduzidos.
* **Layout RTL e Proteção Numérica**: O modo árabe ativa um fluxo verdadeiro da direita para a esquerda (RTL) na janela, aplicando rigorosamente a formatação da esquerda para a direita em métricas, caminhos e indicadores de progresso (`0.0 MB`, `32%`, `C:\...`) para que números e estatísticas de armazenamento nunca sejam invertidos ou corrompidos.

---

### 10. 🔔 Guardião da Bandeja do Sistema Perfeito ao Píxel

* **Ícone Win32 Verdadeiramente de Alta DPI (`LoadCrispTrayIcon`)**: Emprega criação direta de ícones Win32 GDI (`CreateIconIndirect`) com transparência alfa ARGB de 32 bits, entregando renderização nitidíssima em telas com escala de 100%, 125%, 150%, 175% e 200%+ sem desfoque.
* **Telemetria ao Passar o Mouse**: Exibe o uso de memória em tempo real na dica da bandeja: `RAM: 42% (13.4 GB / 31.9 GB)`.
* **Ações Rápidas de Contexto**: Clique com o botão direito para acionar **Boost de Memória com 1 Clique** ou **Limpeza Rápida Inteligente** sem abrir a janela principal.
* **Resiliência ao Explorer**: Escuta a mensagem de difusão `TaskbarCreated` do Windows para restaurar automaticamente o ícone caso o `explorer.exe` seja reiniciado.

---

## 💻 Referência da CLI e Automação Headless

O Deltempo inclui uma CLI de alto desempenho e fácil de automatizar (`deltempo_cli.exe` ou o comando `deltempo`), projetada para tarefas agendadas, administradores de sistemas e ambientes headless.

```powershell
# Pré-visualizar alvos limpáveis sem excluir arquivos (dry-run)
deltempo clean --dry-run

# Executar limpeza segura e enviar os arquivos excluídos para a Lixeira do Windows
deltempo clean --safe --recycle-bin

# Liberar a RAM da lista em espera e aparar os conjuntos de trabalho dos processos
deltempo boost

# Desinstalação profunda de um aplicativo sem criar ponto de restauração
deltempo uninstall "Google Chrome" --silent --force

# Criação opcional de ponto de restauração durante a desinstalação
deltempo uninstall "Epic Games Launcher" --restore-point

# Emitir telemetria do sistema e saúde da memória em JSON estruturado
deltempo status --json

# Ver o que realmente está ocupando o AppData, do maior para o menor (somente leitura; não pode excluir)
deltempo appdata-report --top 15
```

### Resumo dos Comandos da CLI

| Comando | Objetivo | Opções principais |
| :--- | :--- | :--- |
| `deltempo scan [filter]` | Analisar alvos em busca de dados descartáveis | `--json`, `--silent` |
| `deltempo clean [filter]` | Limpar caches descartáveis | `--dry-run`, `--recycle-bin`, `--safe`, `--all`, `--json`, `--yes` |
| `deltempo smart-clean` | Expurgo rápido de caches verificados como seguros | `--dry-run`, `--json`, `--yes` |
| `deltempo deep-clean` | Limpeza completa autônoma (RAM, DISM, escopos) | `--dry-run`, `--json`, `--yes` |
| `deltempo restore-points` | Inspecionar e expurgar Pontos de Restauração do Sistema | `--clean`, `--all` (requer elevação) |
| `deltempo appdata-report` | Detalhamento somente leitura da pegada do AppData, do maior para o menor | `--json`, `--top <n>` — **não existe nenhuma opção de exclusão** |
| `deltempo duplicates [path]` | Localizador de duplicados em nível de bytes (hash em 3 estágios) | `--delete`, `--strategy` |
| `deltempo large [path]` | Analisar unidades em busca de arquivos que consomem espaço | `--min <size>`, `--type <cat>`, `--safe`, `--top <n>`, `--sort <size\|date>` |
| `deltempo large inspect <file>` | Inspecionar o nível de risco e o veredito de segurança do arquivo | `--json` |
| `deltempo large clean` | Enviar para a Lixeira arquivos grandes descartáveis | `--dry-run`, `--yes`, `--safe-only` |
| `deltempo large delete <file>` | Enviar para a Lixeira um arquivo específico | `--yes` |
| `deltempo force-delete <path>` | Expurgar um arquivo/pasta travado ou teimoso | `--terminate-lockers`, `--recycle`, `--dry-run`, `--retry <n>`, `--force-override` |
| `deltempo force-delete scan <path>` | Inspecionar a resistência, os responsáveis pelo bloqueio e o nível de segurança | `--json` |
| `deltempo force-delete pending` | Listar arquivos enfileirados para exclusão na próxima reinicialização | `--json` |
| `deltempo boost` | Otimizar a memória do sistema via kernel NT | `--all`, `--standby`, `--cache`, `--workingsets`, `--json` |
| `deltempo procs` | Listar processos em segundo plano que consomem mais de 20 MB | `--json` |
| `deltempo procs trim <pid\|name>` | Aparar o conjunto de trabalho de um processo | `--json` |
| `deltempo procs kill <pid\|name>` | Encerrar um processo em segundo plano descontrolado | `--json` |
| `deltempo uninstall <app>` | Desinstalação profunda e expurgo de resíduos | `--dry-run`, `--force`, `--silent`, `--restore-point`, `--json` |
| `deltempo startup [list]` | Inspecionar aplicativos de inicialização e impacto na inicialização | `--high`, `--json` |
| `deltempo startup disable <app>` | Desativar reversivelmente um programa de inicialização | N/D |
| `deltempo startup enable <app>` | Reativar um programa de inicialização desativado | N/D |
| `deltempo repair [subcommand]` | Verificação de integridade do Windows e reparo dos serviços do sistema | `all`, `sfc`, `dism`, `winsxs`, `chkdsk`, `scanhealth`, `update`, `network` |
| `deltempo schedule` | Gerenciar a manutenção semanal do Agendador de Tarefas | install / remove / status |
| `deltempo shell` | Gerenciar a integração do menu de contexto do Explorer | install / remove / status |
| `deltempo status` | Exibir telemetria do sistema e informações de memória | `--json` |
| `deltempo test` | Verificação automática do motor (API de memória, escopos detectados) | N/D |
| `deltempo help` | Imprimir a referência completa de comandos | N/D |
| `deltempo update [check]` | Verificar releases oficiais ou aplicar atualização | `check`, `--dry-run` |
| `deltempo register` | Instalar / inspecionar o próprio comando `deltempo` | `--status`, `--remove` |
| `deltempo unregister` | Remover todos os artefatos criados por `register` | N/D |
| `deltempo kill` | Fechar as instâncias do Deltempo em execução em segundo plano | N/D |

**Opções globais** aplicam-se aos comandos de limpeza: `--smart` / `--safe-only`, `--recycle-bin` / `-r`, `--unsafe` (desativa o escudo de 24 horas), `--dry-run` / `-d`, `--yes` / `-y`, `--json` / `-j`, `--silent` / `-s` e `--export <file>` para um log de auditoria com carimbo de data e hora.

---

  <a id="quick-start"></a>

## 🚀 Início Rápido

> ### ⚡ Execute em uma linha
>
> ```powershell
> irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
> ```
>
> Baixa o release mais recente, verifica seu SHA-256, o armazena em cache em `%LOCALAPPDATA%\Deltempo\bin` e o inicia.
> Prefere o binário de console headless? Troque `win` por [`win-cli`](https://beso1227.github.io/Deltempo/).
>
> ### 💻 …e a CLI também está pronta
>
> A primeira inicialização instala o comando `deltempo` para você — `cmd.exe`, PowerShell e <kbd>Win</kbd>+<kbd>R</kbd> funcionam.
> Depois, abra uma janela de terminal **nova** e então:
>
> ```powershell
> deltempo test        # autoverificação do motor
> deltempo status      # telemetria ao vivo de disco + RAM
> deltempo register --status
> ```

### Opção 1: Executável Portátil Autônomo (Recomendado)

1. Baixe o **`Deltempo.exe`** na página do [Release mais recente](https://github.com/Beso1227/Deltempo/releases/latest).
2. Execute `Deltempo.exe` diretamente (sem instalador, arquivo único autossuficiente).
3. Clique em **Scan Now** ou **1-Click Deep Clean** para recuperar espaço.

### Opção 2: Comando de Uma Linha no Terminal (PowerShell)

Inicie o release mais recente diretamente pelo terminal — sem navegador, sem instalador:

```powershell
irm https://beso1227.github.io/Deltempo/win | Invoke-Expression
```

O script de bootstrap baixa o `Deltempo.exe` mais recente, verifica seu SHA-256 contra o `checksums.sha256` publicado, o armazena em cache em `%LOCALAPPDATA%\Deltempo\bin` e o inicia. Para o binário da CLI headless, use o ponto de entrada `win-cli`:

```powershell
irm https://beso1227.github.io/Deltempo/win-cli | Invoke-Expression
```

Se o manifesto não puder ser lido ou se o hash não corresponder, o instalador aborta e não executa nada do que baixou — a verificação nunca é ignorada. Observe que `Invoke-Expression` não aceita argumentos, então o alvo é selecionado pelo ponto de entrada em vez de uma opção `-Cli`.

### Opção 3: O Comando `deltempo`

Você nunca precisa configurar isso manualmente. Na primeira vez que iniciar o Deltempo, ele prepara um binário de CLI de subsistema de console, o adiciona ao `PATH` do seu usuário, registra o alias <kbd>Win</kbd>+<kbd>R</kbd> e instala uma função `deltempo` nos seus perfis do PowerShell — corrigindo um registro obsoleto deixado por uma instalação antiga, se encontrar algum.

Duas coisas que valem saber:

- **Abra uma janela de terminal nova.** Shells já abertos mantêm o `PATH` com o qual iniciaram.
- **Primeira execução offline?** Se o binário de console não puder ser preparado, nada é registrado e nenhum comando pela metade fica para trás. O aplicativo desktop não é afetado — a próxima inicialização bem-sucedida tenta de novo.

Prefere gerenciar você mesmo? `deltempo register` faz o mesmo sob demanda, e `deltempo unregister` remove todos os artefatos que ele criou. Use `deltempo register --status` para ver exatamente o que está instalado e para qual binário ele aponta.

> Comandos que tocam estado protegido do sistema (`restore-points`, o estágio DISM do `deep-clean`)
> retornam um erro claro e um código de saída diferente de zero quando executados em um terminal padrão, não elevado.
> Execute-os de um shell de Administrador, ou use o aplicativo desktop.

---

<a id="privacy"></a>

## 🔒 Garantia de Privacidade e Segurança

O Deltempo é arquitetado com segurança e privacidade do usuário como fundamentos não negociáveis:

1. **Garantia de Zero Telemetria**: O Deltempo contém **zero telemetria**, bibliotecas de análises, SDKs de publicidade ou rastreadores de ping em segundo plano. A varredura de rotina, a limpeza, a otimização de memória e a desinstalação rodam **100% offline**.
2. **Pipeline de Segurança Determinístico**:

   ```text
   ┌────────┐     ┌────────┐     ┌─────────┐     ┌────────────┐     ┌─────────┐
   │  SCAN  │ ──► │  PLAN  │ ──► │ PROTECT │ ──► │ REVALIDATE │ ──► │  CLEAN  │
   └────────┘     └────────┘     └─────────┘     └────────────┘     └─────────┘
   ```

   Os arquivos candidatos são planejados, verificados contra os limites de diretórios protegidos (Documentos, Área de Trabalho, Repositórios de Código, chaves SSH, credenciais) e revalidados imediatamente antes da exclusão.
3. **Defesa contra Pontos de Reanálise e Travessia**: Junções de diretório NTFS, links simbólicos e pontos de montagem de volume são rejeitados automaticamente para impedir ataques de travessia fora dos limites do alvo.
4. **Limite de Unidade Local**: Confinado exclusivamente a unidades fixas locais; compartilhamentos de rede remotos e caminhos UNC são bloqueados.
5. **Proteção de Estado Durável do AppData**: O Deltempo se recusa a excluir o *estado* de aplicativos que apenas mora sob `AppData`. Bancos de dados de sincronização do OneDrive, credenciais do Office e do Outlook, o cofre do Gerenciador de Credenciais do Windows, listas de saltos (*jump lists*) de arquivos recentes, material de chaves SSH e GnuPG e os hives do shell `NTUSER.DAT` / `UsrClass.dat` são todos negados. Isso é aplicado **por diretório, não apenas por arquivo**: um `Directory.Delete` recursivo remove uma subárvore inteira em uma única chamada de sistema e nunca verifica arquivo por arquivo, então as operações de expurgo de resíduos primeiro procuram um descendente protegido, limitam esse percurso a 5.000 diretórios, pulam pontos de reanálise e **falham de forma fechada** se o limite for excedido.
6. **Verificação Criptográfica de Releases**: As verificações automáticas de atualização exigem HTTPS e conferem os digests SHA-256 dos binários contra os manifestos de release assinados do GitHub.

> O verificador de assinatura do manifesto é ECDSA P-256 com uma chave fictícia (placeholder) embutida, e nem ele nem a verificação Authenticode ainda estão integrados ao fluxo real de atualização. Já a verificação do digest SHA-256 contra o artefato do release *é* aplicada. `docs/THREAT_MODEL.md` lista essas lacunas como brechas conhecidas, não como mitigações.

---

## 🛠️ Compilando a Partir do Código-Fonte

### Pré-requisitos

* Windows 10 ou 11 (64 bits / x64)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* PowerShell 7+ ou Windows PowerShell 5.1

### Compilação e Testes

```powershell
# Clonar o repositório
git clone https://github.com/Beso1227/Deltempo.git
cd Deltempo

# Compilar toda a solução na configuração Release
dotnet build deltempo.sln -c Release

# Verifica o estilo de código da mesma forma que a CI. Execute contra deltempo.sln, nunca contra um
# único projeto: a solução contém o app WPF, o Core, a CLI, o shell WinUI e o
# projeto de testes, então uma verificação por projeto passaria sem erro e deixaria parte da solução sem verificar.
dotnet format deltempo.sln --verify-no-changes --no-restore

# Executar a suíte de testes automatizados (1505 testes unitários e de integração aprovados)
# O xunit.v3 roda sobre o Microsoft.Testing.Platform, que o `dotnet test` do SDK
# do .NET 10 não consegue executar ("Testing with VSTest target is no longer
# supported"). Compile uma vez e depois invoque o executável de testes diretamente.
dotnet build Tests/Deltempo.Tests/Deltempo.Tests.csproj -c Release
./Tests/Deltempo.Tests/bin/Release/net10.0-windows/Deltempo.Tests.exe --filter "Category!=Benchmark"

# Empacotar os executáveis de release portáteis de arquivo único (GUI e CLI)
pwsh -ExecutionPolicy Bypass -File scripts/build_release_exe.ps1
```

Os executáveis de arquivo único resultantes serão publicados em:

* `publish\Deltempo.exe` (GUI)
* `publish\deltempo_cli.exe` (CLI)

---

## 🙏 Créditos e Ferramentas de Terceiros

O Deltempo é criado e mantido pelos colaboradores do Deltempo. Um recurso incorpora o
trabalho de outra pessoa, e esse projeto é creditado aqui por completo:

| Projeto | Autor | O que o Deltempo faz com ele |
|---|---|---|
| **[Chris Titus Tech Windows Utility (WinUtil)](https://christitus.com/win)** | **[Chris Titus](https://christitus.com)** | O botão **CTT WinUtil** em Reparo do Sistema baixa este script e o executa em uma nova janela do PowerShell **elevada**, exatamente como a própria documentação dele recomenda |

### Atenção

- O WinUtil **não tem licença MIT** e **não faz parte** do Deltempo. O Deltempo não o redistribui, não o espelha, não o fixa em uma versão específica e não o verifica — e não pode.
- Como o script é baixado e executado em uma única etapa, **tudo o que for servido por essa URL é executado com direitos de administrador na sua máquina.** Leia antes, se quiser: <https://christitus.com/win>
- O motor de segurança do Deltempo, a revalidação antes da exclusão e a verificação das assinaturas dos releases cobrem **apenas o código do Deltempo**, nunca código que ele não viu.
- Se o WinUtil ajudar você, apoie diretamente o [Chris Titus](https://christitus.com).

Os detalhes completos, incluindo o comando exato executado pelo Deltempo, estão em
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## 📜 Licença

O Deltempo é software livre e de código aberto licenciado sob a **[Licença MIT](LICENSE)**.

```text
Copyright (c) 2026 Deltempo Contributors
```

---

## ❓ Perguntas Frequentes

**O Deltempo é realmente gratuito?**
Sim. O Deltempo é completamente gratuito e de código aberto sob MIT — sem plano pago, sem ofertas extras, sem conta e sem publicidade.

**O Deltempo é uma boa alternativa ao CCleaner?**
Sim. O Deltempo é uma alternativa gratuita, de código aberto e sem telemetria ao CCleaner que também inclui um desinstalador profundo, um localizador de arquivos duplicados, um detector de arquivos grandes e reparo integrado do sistema Windows — sem necessidade de instalação.

**O Deltempo envia telemetria ou me rastreia?**
Não. O Deltempo tem zero telemetria: sem bibliotecas de análises, sem pixels de rastreamento, sem SDKs de publicidade. Toda operação roda 100% offline.

**O Deltempo exige um instalador?**
Não. É um executável portátil de arquivo único (~68 MB). Execute `Deltempo.exe` diretamente no local — nada é instalado no seu sistema.

**Limpar caches de navegador vai me desconectar das minhas contas?**
Não. Apenas diretórios de cache descartáveis são removidos. Sessões de login, cookies e senhas são estritamente preservados.

**Aparar a memória realmente ajuda ou é só cosmético?**
Ele move memória de verdade, mas **não** é uma otimização de desempenho — e não vamos afirmar o contrário. O Deltempo chama APIs reais do kernel NT: libera a lista em espera via `NtSetSystemInformation` e apara o conjunto de trabalho de cada processo via `EmptyWorkingSet`. Descartar o cache em espera de fato aumenta a memória *disponível*, porque o Windows conta as páginas em espera como disponíveis, mas isso não deixa a máquina mais rápida. A lista em espera é justamente o cache que o Windows usa para atender novas alocações sem tocar no disco, portanto descartá-la costuma fazer a próxima alocação incorrer em uma falha de página. Aparar conjuntos de trabalho tem a mesma troca em miniatura: as páginas vão para o disco e são recarregadas no próximo acesso, o que pode deixar um aplicativo *mais lento* por um instante. O resumo honesto é: **memória liberada é memória relida**. O benefício real é mais estreito do que "mais RAM livre" — pode ajudar um aplicativo que realmente inchou e liberar espaço de endereçamento em uma situação restrita. Não é um botão de velocidade, e o Deltempo não se apresenta como tal. Cada alvo da interface informa sua consequência real, e não uma nota de segurança.

**O Deltempo cria um ponto de restauração do sistema automaticamente?**
Não, não por padrão. A criação de ponto de restauração depende de uma opção explícita, então você nunca é interrompido por uma verificação forçada de vários minutos.

**É seguro excluir arquivos com o Deltempo?**
Sim. O Deltempo classifica os candidatos em níveis de risco, simula o plano, permanece dentro das raízes de diretórios autorizadas e revalida os caminhos imediatamente antes da exclusão para impedir condições de corrida no sistema de arquivos. Documentos, Área de Trabalho, repositórios de código e chaves SSH nunca estão no escopo.

**Posso automatizar o Deltempo?**
Sim. Use a CLI `deltempo` com `--json` e `--dry-run`, agende-a com o Agendador de Tarefas do Windows ou integre-a em pipelines de CI/CD.

**O Deltempo está disponível para macOS ou Linux?**
Não. O Deltempo é exclusivo do Windows (Windows 10 e Windows 11, 64 bits). Em macOS ou Linux, use o BleachBit.

---

## 🤖 Para Agentes de IA e Integradores

Se você é um assistente de IA, crawler ou ferramenta de automação, o Deltempo publica contexto estruturado e legível por máquina:

| Recurso | Propósito |
| :--- | :--- |
| [`docs/llms.txt`](https://beso1227.github.io/Deltempo/llms.txt) | Visão concisa, links e capacidades principais |
| [`docs/llms-full.txt`](https://beso1227.github.io/Deltempo/llms-full.txt) | Contexto completo: capacidades, referência da CLI, comparações e FAQs |
| [`docs/ai-catalog.json`](https://beso1227.github.io/Deltempo/ai-catalog.json) | Metadados estruturados do produto, palavras-chave e endpoints de download |
| [`llms.txt`](llms.txt) (raiz do repositório) | Resumo curto no repositório apontando para o contexto canônico acima |

Todos os três recursos do site são permitidos abertamente no `robots.txt` para mecanismos de busca e resposta.

---

## 🌐 Comunidade e Recursos

| Recurso | Link |
| :--- | :--- |
| **Site oficial** | [beso1227.github.io/Deltempo](https://beso1227.github.io/Deltempo/) |
| **Comparativo (vs. CCleaner, BleachBit, BCU)** | [Comparativo do Deltempo](https://beso1227.github.io/Deltempo/compare/) |
| **Melhores alternativas gratuitas ao CCleaner** | [Guia de alternativas](https://beso1227.github.io/Deltempo/alternatives/) |
| **Releases mais recentes** | [GitHub Releases](https://github.com/Beso1227/Deltempo/releases) |
| **Especificação de arquitetura** | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| **Modelo de ameaças e segurança** | [docs/THREAT_MODEL.md](docs/THREAT_MODEL.md) |
| **Guia de testes** | [docs/TESTING.md](docs/TESTING.md) |
| **Guia de contribuição** | [CONTRIBUTING.md](CONTRIBUTING.md) |
| **Código de conduta** | [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) |
| **Política de segurança** | [SECURITY.md](SECURITY.md) |
| **Relatórios de Bugs e Issues** | [GitHub Issues](https://github.com/Beso1227/Deltempo/issues) |
