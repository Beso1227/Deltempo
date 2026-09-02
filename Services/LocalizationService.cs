using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class LocalizationService
{
    public static string CurrentLanguage { get; set; } = "en";

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["en"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Windows & User Profile Guardian",
            ["AdminLabel"] = "Administrator",
            ["ReclaimableSpace"] = "RECLAIMABLE JUNK & APPDATA",
            ["HeroScanSubtext"] = "Deep scan of User Profile, GPU Shaders, AppData & System Temp",
            ["DriveOsLabel"] = "OS Drive (C:)",
            ["DriveFree"] = "Free",
            ["DriveOf"] = "free of",
            ["SelectSafe"] = "Select 100% Safe",
            ["SelectAll"] = "Select All",
            ["Clear"] = "Clear",
            ["Rescan"] = "Rescan (F5)",
            ["SafetyShield"] = "Safety Shield (>24h Old)",
            ["ReadyStatus"] = "Ready for precision cleanup",
            ["ActivityLog"] = "Activity Log",
            ["HideLog"] = "Hide Log",
            ["ExportReport"] = "Export Report",
            ["CleanSelected"] = "Clean Selected",
            ["Cancel"] = "Cancel",
            ["Inspect"] = "Inspect",
            ["ConfirmTitle"] = "Confirm Precision Cleanup",
            ["ConfirmSubtitle"] = "Ready to purge selected disposable cache files",
            ["ConfirmReclaimableLabel"] = "ESTIMATED RECLAIMABLE SPACE",
            ["ConfirmShieldOn"] = "🟢 Safety Shield: ON",
            ["ConfirmShieldOff"] = "⚠️ Safety Shield: OFF",
            ["ConfirmSummary"] = "Cleaning selected categories. User configs and personal files remain 100% protected.",
            ["StartCleanup"] = "Start Cleanup",
            ["CompletedTitle"] = "Cleanup Completed!",
            ["SuccessfullyReclaimed"] = "Successfully Reclaimed",
            ["Awesome"] = "Awesome!",
            ["FilesDeleted"] = "FILES DELETED",
            ["FoldersPurged"] = "FOLDERS PURGED",
            ["TimeElapsed"] = "TIME ELAPSED",
            ["InspectorTitle"] = "Largest Junk Files Inspector",
            ["InspectorSubtitle"] = "Showing individual large files discovered in this category",
            ["CloseInspector"] = "Close Inspector (Esc)"
        },
        ["ar"] = new()
        {
            ["AppTitle"] = "ديلتيمبو",
            ["AppSubtitle"] = "حارس نظام ويندوز وملفات المستخدم",
            ["AdminLabel"] = "مسؤول النظام",
            ["ReclaimableSpace"] = "المساحة القابلة للاسترداد",
            ["HeroScanSubtext"] = "فحص شامل للملفات المؤقتة وكاش البرامج وكروت الشاشة",
            ["DriveOsLabel"] = "قرص النظام (C:)",
            ["DriveFree"] = "متاح",
            ["DriveOf"] = "متاح من إجمالي",
            ["SelectSafe"] = "تحديد الآمن 100%",
            ["SelectAll"] = "تحديد الكل",
            ["Clear"] = "إلغاء التحديد",
            ["Rescan"] = "إعادة فحص (F5)",
            ["SafetyShield"] = "درع الأمان (أقدم من 24 ساعة)",
            ["ReadyStatus"] = "جاهز للتنظيف الدقيق والآمن",
            ["ActivityLog"] = "سجل النشاط",
            ["HideLog"] = "إخفاء السجل",
            ["ExportReport"] = "تصدير التقرير",
            ["CleanSelected"] = "تنظيف المحدد",
            ["Cancel"] = "إلغاء",
            ["Inspect"] = "معاينة",
            ["ConfirmTitle"] = "تأكيد التنظيف الدقيق",
            ["ConfirmSubtitle"] = "جاهز لحذف ملفات الكاش والمهملات المحددة",
            ["ConfirmReclaimableLabel"] = "المساحة المتوقع استردادها",
            ["ConfirmShieldOn"] = "🟢 درع الأمان: مفعّل",
            ["ConfirmShieldOff"] = "⚠️ درع الأمان: معطّل",
            ["ConfirmSummary"] = "تنظيف الفئات المحددة. حساباتك وملفاتك الشخصية محمية 100%.",
            ["StartCleanup"] = "بدء التنظيف",
            ["CompletedTitle"] = "اكتمل التنظيف بنجاح!",
            ["SuccessfullyReclaimed"] = "تم استرداد بنجاح",
            ["Awesome"] = "رائع وممتاز!",
            ["FilesDeleted"] = "الملفات المحذوفة",
            ["FoldersPurged"] = "المجلدات المفرغة",
            ["TimeElapsed"] = "الوقت المستغرق",
            ["InspectorTitle"] = "فاحص أكبر الملفات حجماً",
            ["InspectorSubtitle"] = "عرض الملفات الفردية الكبيرة المكتشفة في هذا القسم",
            ["CloseInspector"] = "إغلاق الفاحص (Esc)"
        },
        ["es"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Guardián de Windows y Perfiles de Usuario",
            ["AdminLabel"] = "Administrador",
            ["ReclaimableSpace"] = "ESPACIO RECUPERABLE",
            ["HeroScanSubtext"] = "Escaneo profundo de AppData, shaders GPU y temporales",
            ["DriveOsLabel"] = "Disco SO",
            ["DriveFree"] = "Libre",
            ["DriveOf"] = "libre de",
            ["SelectSafe"] = "🟢 100% Seguro",
            ["SelectAll"] = "Seleccionar Todo",
            ["Clear"] = "Limpiar",
            ["Rescan"] = "Reescanear (F5)",
            ["SafetyShield"] = "Escudo de Seguridad (>24h)",
            ["ReadyStatus"] = "Listo para limpieza de precisión",
            ["ActivityLog"] = "Registro de Actividad",
            ["HideLog"] = "Ocultar Registro",
            ["ExportReport"] = "Exportar Informe",
            ["CleanSelected"] = "Limpiar Seleccionado",
            ["Cancel"] = "Cancelar",
            ["Inspect"] = "Inspeccionar",
            ["ConfirmTitle"] = "Confirmar Limpieza",
            ["ConfirmSubtitle"] = "Listo para purgar archivos temporales seleccionados",
            ["ConfirmReclaimableLabel"] = "ESPACIO ESTIMADO A RECUPERAR",
            ["ConfirmShieldOn"] = "🟢 Escudo de Seguridad: ACTIVO",
            ["ConfirmShieldOff"] = "⚠️ Escudo de Seguridad: INACTIVO",
            ["ConfirmSummary"] = "Purgando categorías seleccionadas. Cuentas y archivos personales 100% protegidos.",
            ["StartCleanup"] = "Comenzar Limpieza",
            ["CompletedTitle"] = "¡Limpieza Completada!",
            ["SuccessfullyReclaimed"] = "Espacio Recuperado",
            ["Awesome"] = "¡Genial!",
            ["FilesDeleted"] = "ARCHIVOS ELIMINADOS",
            ["FoldersPurged"] = "CARPETAS PURGADAS",
            ["TimeElapsed"] = "TIEMPO TRANSCURRIDO",
            ["InspectorTitle"] = "Inspector de Archivos Grandes",
            ["InspectorSubtitle"] = "Mostrando archivos más pesados en esta categoría",
            ["CloseInspector"] = "Cerrar Inspector (Esc)"
        },
        ["fr"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Gardien de Windows et Profils Utilisateur",
            ["AdminLabel"] = "Administrateur",
            ["ReclaimableSpace"] = "ESPACE RÉCUPÉRABLE",
            ["HeroScanSubtext"] = "Analyse approfondie d'AppData, shaders GPU et fichiers temporaires",
            ["DriveOsLabel"] = "Disque Système",
            ["DriveFree"] = "Libre",
            ["DriveOf"] = "libre sur",
            ["SelectSafe"] = "🟢 100% Sûr",
            ["SelectAll"] = "Tout Sélectionner",
            ["Clear"] = "Effacer",
            ["Rescan"] = "Re-scanner (F5)",
            ["SafetyShield"] = "Bouclier de Sécurité (>24h)",
            ["ReadyStatus"] = "Prêt pour le nettoyage de précision",
            ["ActivityLog"] = "Journal d'Activité",
            ["HideLog"] = "Masquer le Journal",
            ["ExportReport"] = "Exporter le Rapport",
            ["CleanSelected"] = "Nettoyer la Sélection",
            ["Cancel"] = "Annuler",
            ["Inspect"] = "Inspecter",
            ["ConfirmTitle"] = "Confirmer le Nettoyage",
            ["ConfirmSubtitle"] = "Prêt à purger les fichiers de cache sélectionnés",
            ["ConfirmReclaimableLabel"] = "ESPACE ESTIMÉ À RÉCUPÉRER",
            ["ConfirmShieldOn"] = "🟢 Bouclier de Sécurité: ACTIF",
            ["ConfirmShieldOff"] = "⚠️ Bouclier de Sécurité: INACTIF",
            ["ConfirmSummary"] = "Nettoyage des catégories. Comptes et documents protégés à 100%.",
            ["StartCleanup"] = "Démarrer",
            ["CompletedTitle"] = "Nettoyage Terminé!",
            ["SuccessfullyReclaimed"] = "Espace Récupéré",
            ["Awesome"] = "Super!",
            ["FilesDeleted"] = "FICHIERS SUPPRIMÉS",
            ["FoldersPurged"] = "DOSSIERS PURGÉS",
            ["TimeElapsed"] = "TEMPS ÉCOULÉ",
            ["InspectorTitle"] = "Inspecteur de Gros Fichiers",
            ["InspectorSubtitle"] = "Affichage des fichiers volumineux dans cette catégorie",
            ["CloseInspector"] = "Fermer (Esc)"
        },
        ["de"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Windows & Benutzerprofil Wächter",
            ["AdminLabel"] = "Administrator",
            ["ReclaimableSpace"] = "RÜCKGEWINNBARER SPEICHERPLATZ",
            ["HeroScanSubtext"] = "Tiefenscan von AppData, GPU-Shadern & System-Temporärdateien",
            ["DriveOsLabel"] = "Systemlaufwerk (C:)",
            ["DriveFree"] = "Frei",
            ["DriveOf"] = "frei von",
            ["SelectSafe"] = "🟢 100% Sicher",
            ["SelectAll"] = "Alles Auswählen",
            ["Clear"] = "Auswahl Aufheben",
            ["Rescan"] = "Erneut Scannen (F5)",
            ["SafetyShield"] = "Sicherheitsschild (>24h)",
            ["ReadyStatus"] = "Bereit für Präzisionsreinigung",
            ["ActivityLog"] = "Aktivitätsprotokoll",
            ["HideLog"] = "Protokoll Ausblenden",
            ["ExportReport"] = "Bericht Exportieren",
            ["CleanSelected"] = "Auswahl Bereinigen",
            ["Cancel"] = "Abbrechen",
            ["Inspect"] = "Inspizieren",
            ["ConfirmTitle"] = "Bereinigung Bestätigen",
            ["ConfirmSubtitle"] = "Bereit zum Löschen ausgewählter Cache-Dateien",
            ["ConfirmReclaimableLabel"] = "GESCHÄTZTER SPEICHERPLATZ",
            ["ConfirmShieldOn"] = "🟢 Sicherheitsschild: AKTIV",
            ["ConfirmShieldOff"] = "⚠️ Sicherheitsschild: INAKTIV",
            ["ConfirmSummary"] = "Bereinigung ausgewählter Kategorien. Konten & persönliche Daten 100% geschützt.",
            ["StartCleanup"] = "Bereinigung Starten",
            ["CompletedTitle"] = "Bereinigung Abgeschlossen!",
            ["SuccessfullyReclaimed"] = "Erfolgreich Freigegeben",
            ["Awesome"] = "Hervorragend!",
            ["FilesDeleted"] = "GELÖSCHTE DATEIEN",
            ["FoldersPurged"] = "GELÖSCHTE ORDNER",
            ["TimeElapsed"] = "BENÖTIGTE ZEIT",
            ["InspectorTitle"] = "Große Dateien Inspektor",
            ["InspectorSubtitle"] = "Zeigt die größten gefundenen temporären Dateien",
            ["CloseInspector"] = "Schließen (Esc)"
        }
    };

    public static string Get(string key)
    {
        if (Translations.TryGetValue(CurrentLanguage, out var dict) && dict.TryGetValue(key, out var val))
        {
            return val;
        }
        if (Translations["en"].TryGetValue(key, out var fallbackVal))
        {
            return fallbackVal;
        }
        return key;
    }

    public static void LocalizeTarget(TargetFolderInfo target)
    {
        switch (target.Id)
        {
            case "UserTemp":
                target.Name = CurrentLanguage switch {
                    "ar" => "ملفات المستخدم المؤقتة (%TEMP%)",
                    "es" => "Archivos Temporales de Usuario",
                    "fr" => "Fichiers Temporaires Utilisateur",
                    "de" => "Benutzer-Temp & Zwischenspeicher",
                    _ => "User Temp & Scratchpad"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "كاش المستخدم",
                    "es" => "Caché Usuario",
                    "fr" => "Cache Utilisateur",
                    "de" => "Benutzer-Cache",
                    _ => "User Cache"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "كاش البرامج، استخراج ملفات التثبيت المؤقتة والتحميلات (%TEMP%)",
                    "es" => "Caché de aplicaciones, extracciones de instalación (%TEMP%)",
                    "fr" => "Cache d'applications et extractions temporaires (%TEMP%)",
                    "de" => "Anwendungs-Cache und temporäre Setup-Extrakte (%TEMP%)",
                    _ => "Application cache, temporary setup extracts, downloads (%TEMP%)"
                };
                break;

            case "WinTemp":
                target.Name = CurrentLanguage switch {
                    "ar" => "ملفات نظام ويندوز المؤقتة",
                    "es" => "Temporales del Sistema Windows",
                    "fr" => "Fichiers Temporaires Système",
                    "de" => "Windows System-Temp",
                    _ => "Windows System Temp"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "النظام وكارت الشاشة",
                    "es" => "Sistema y GPU",
                    "fr" => "Système & GPU",
                    "de" => "System & GPU",
                    _ => "System & GPU"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "سجلات تشخيص النظام ومسودات تحديثات ويندوز (C:\\Windows\\Temp)",
                    "es" => "Registros de diagnóstico y temporales de actualización",
                    "fr" => "Traces de diagnostic et fichiers de mise à jour système",
                    "de" => "Betriebssystem-Diagnose und Update-Zwischenspeicher",
                    _ => "OS diagnostic traces, system update scratchpad (C:\\Windows\\Temp)"
                };
                break;

            case "WinPrefetch":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش التشغيل المسبق (Prefetch)",
                    "es" => "Caché Prefetch de Windows",
                    "fr" => "Cache Windows Prefetch",
                    "de" => "Windows Prefetch-Cache",
                    _ => "Windows Prefetch Cache"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "آثار التشغيل القديمة وترويسات بدء التشغيل المؤقتة",
                    "es" => "Rastros de ejecución antiguos y cabeceras de inicio",
                    "fr" => "Traces d'exécution obsolètes et en-têtes de démarrage",
                    "de" => "Veraltete Ausführungsspuren und Start-Header",
                    _ => "Stale execution traces & cached startup headers (C:\\Windows\\Prefetch)"
                };
                break;

            case "WinUpdateCache":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش تنزيلات تحديثات ويندوز",
                    "es" => "Caché de Windows Update",
                    "fr" => "Téléchargements Windows Update",
                    "de" => "Windows Update Download-Cache",
                    _ => "Windows Update Cache"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "حزم التثبيت التي تم تنزيلها وكاش التسليم (SoftwareDistribution\\Download)",
                    "es" => "Instaladores descargados y caché de distribución",
                    "fr" => "Packages d'installation téléchargés et cache de distribution",
                    "de" => "Heruntergeladene Update-Installer und Bereitstellungs-Cache",
                    _ => "Downloaded update installers & delivery cache (SoftwareDistribution\\Download)"
                };
                break;

            case "WinDeliveryOpt":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش تسليم التحديثات عبر الشبكة (WUDO)",
                    "es" => "Optimización de Entrega de Windows",
                    "fr" => "Optimisation de Livraison Windows",
                    "de" => "Windows Übermittlungsoptimierung",
                    _ => "Windows Delivery Optimization"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "النظام والتحديثات",
                    "es" => "Sistema y SO",
                    "fr" => "Système & OS",
                    "de" => "System & OS",
                    _ => "System & OS"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "أجزاء كاش التحديثات الموزعة بين الأجهزة عبر الشبكة (DeliveryOptimization)",
                    "es" => "Fragmentos de entrega P2P de actualizaciones de Windows",
                    "fr" => "Morceaux de mise à jour P2P et cache d'optimisation",
                    "de" => "P2P-Update-Bereitstellungsfragmente und Hintergrund-Cache",
                    _ => "P2P Windows update delivery chunks and background bits cache (DeliveryOptimization)"
                };
                break;

            case "GpuShaderCaches":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش كروت الشاشة و DirectX",
                    "es" => "Shaders de GPU y DirectX",
                    "fr" => "Shaders GPU et DirectX",
                    "de" => "DirectX & GPU Shader-Caches",
                    _ => "DirectX & GPU Shader Caches"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "كارت الشاشة",
                    "es" => "GPU Shaders",
                    "fr" => "GPU Shaders",
                    "de" => "GPU-Shader",
                    _ => "System & GPU"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "شيدرز الرسوميات المترجمة لكروت NVIDIA و AMD و Intel و D3DSCache",
                    "es" => "Shaders gráficos compilados de NVIDIA, AMD e Intel",
                    "fr" => "Shaders graphiques compilés NVIDIA, AMD et Intel",
                    "de" => "Kompilierte Grafik-Shader von NVIDIA, AMD, D3DSCache & Intel",
                    _ => "Compiled graphics shaders from NVIDIA, AMD, D3DSCache & Intel"
                };
                break;

            case "GamingLaunchers":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش منصات الألعاب (Steam / Epic / Battle.net)",
                    "es" => "Lanzadores de Juegos y Shaders",
                    "fr" => "Lanceurs de Jeux et Shaders",
                    "de" => "Gaming-Launcher & Shader-Caches",
                    _ => "Game Launchers & Shaders"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "الألعاب والوسائط",
                    "es" => "Juegos y Medios",
                    "fr" => "Jeux & Médias",
                    "de" => "Gaming & Medien",
                    _ => "Gaming & Media"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "ملفات تنزيلات Steam المؤقتة وكاش المتصفح لـ Epic Games و Battle.net و EA App",
                    "es" => "Descargas temporales de Steam, caché web de Epic Games y Battle.net",
                    "fr" => "Fichiers de téléchargement Steam, caches web Epic Games et Battle.net",
                    "de" => "Steam Download-Fragmente & Shader, Epic Games Webcache, Battle.net & EA App",
                    _ => "Steam download chunks & shadercache, Epic Games webcache, Battle.net & EA App caches"
                };
                break;

            case "MediaCreatorCaches":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش برامج المونتاج والتصميم (Adobe / DaVinci)",
                    "es" => "Cachés de Renderizado y Creadores",
                    "fr" => "Caches de Rendu et Créateurs",
                    "de" => "Medien- & Render-Caches",
                    _ => "Media & Creator Render Caches"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "صناع المحتوى والميديا",
                    "es" => "Creadores y Medios",
                    "fr" => "Créateurs & Médias",
                    "de" => "Kreativ & Medien",
                    _ => "Creator & Media"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "ملفات Media Cache و Peak في Adobe Premiere و DaVinci Resolve وسجلات OBS",
                    "es" => "Media Cache y Peak de Adobe Premiere, caché proxy de DaVinci y logs de OBS",
                    "fr" => "Fichiers Media Cache Adobe Premiere, proxies DaVinci et logs OBS",
                    "de" => "Adobe Premiere Media Cache & Peak-Dateien, DaVinci Resolve Proxy-Cache, OBS-Logs",
                    _ => "Adobe Premiere / After Effects Media Cache & Peak files, DaVinci Resolve proxy scratch, OBS logs"
                };
                break;

            case "AppCacheSweeper":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش برامج سطح المكتب (Discord/Spotify)",
                    "es" => "Caché de Apps de Escritorio",
                    "fr" => "Cache des Applications Desktop",
                    "de" => "Desktop-App Cache-Bereiniger",
                    _ => "Desktop Apps Cache Sweeper"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "كاش GPU و Code Cache المؤقت في Discord و Spotify و Slack و VS Code و Notion",
                    "es" => "Caché GPU y código de Discord, Spotify, Slack, VS Code y Notion",
                    "fr" => "Cache GPU et code de Discord, Spotify, Slack, VS Code et Notion",
                    "de" => "Flüchtiger GPU- & Code-Cache in Discord, Spotify, Slack, VS Code, Teams, Notion",
                    _ => "Disposable GPU & Code Cache in Discord, Spotify, Slack, VS Code, Teams, Notion"
                };
                break;

            case "BrowserCaches":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش متصفحات الإنترنت (Chrome/Edge/Brave)",
                    "es" => "Caché de Navegadores Web",
                    "fr" => "Cache des Navigateurs Web",
                    "de" => "Webbrowser-Caches",
                    _ => "Web Browsers Cache Pool"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "كاش صفحات الويب والوسائط في Chrome و Edge و Brave (مع الحفاظ على تسجيلات الدخول)",
                    "es" => "Caché web de Chrome, Edge, Brave (preserva sesiones y contraseñas)",
                    "fr" => "Cache web Chrome, Edge, Brave (mots de passe et sessions préservés)",
                    "de" => "Chrome, Edge, Brave, Firefox Web-Cache (Cookies und Logins bleiben erhalten)",
                    _ => "Chrome, Edge, Brave, Firefox web cache (cookies and logins preserved)"
                };
                break;

            case "DevPackageCaches":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش حزم المطورين (pip / npm / gradle)",
                    "es" => "Caché de Paquetes de Desarrollo",
                    "fr" => "Caches de Packages Développeur",
                    "de" => "Entwickler- & Paket-Caches",
                    _ => "Developer & Package Caches"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "كاش تنزيلات الحزم في pip و npm و yarn و gradle و nuget",
                    "es" => "Descargas temporales de pip, npm, yarn, gradle y nuget",
                    "fr" => "Téléchargements de packages pip, npm, yarn, gradle et nuget",
                    "de" => "pip, npm, .gradle, yarn, .cache und nuget Paket-Download-Caches",
                    _ => "pip, npm, .gradle, yarn, .cache, and nuget package download caches"
                };
                break;

            case "MobileDevResiduals":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش مزامنة الهواتف ومحاكيات التطوير",
                    "es" => "Sincronización Móvil y Demonios Dev",
                    "fr" => "Synchro Mobile et Démons Dev",
                    "de" => "Mobile Synchronisation & Dev-Daemons",
                    _ => "Mobile Sync & Dev Daemons"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "المطورين والهواتف",
                    "es" => "Móvil y Desarrollo",
                    "fr" => "Mobile & Développeur",
                    "de" => "Entwicklung & Mobil",
                    _ => "Dev & Mobile"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "كاش مزامنة iTunes المؤقت، كاش محاكي Android Studio، ومسودات Gradle و Cargo",
                    "es" => "Caché temporal de Apple iTunes, emulador Android Studio y daemons Gradle",
                    "fr" => "Cache temporaire iTunes, émulateur Android Studio et démons Gradle",
                    "de" => "Apple iTunes Sync-Temp-Cache, Android Studio Emulator-Cache, Gradle & Cargo",
                    _ => "Apple iTunes temp sync cache, Android Studio emulator cache, Gradle & Cargo caches"
                };
                break;

            case "WinServicingLogs":
                target.Name = CurrentLanguage switch {
                    "ar" => "سجلات صيانة ويندوز (CBS & DISM)",
                    "es" => "Registros de Mantenimiento y CBS",
                    "fr" => "Journaux de Maintenance et CBS",
                    "de" => "Windows Wartungs- & CBS-Protokolle",
                    _ => "Windows Servicing & CBS Logs"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "التشخيص والسجلات",
                    "es" => "Diagnóstico",
                    "fr" => "Diagnostic",
                    "de" => "Diagnose",
                    _ => "Diagnostics"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "سجلات خدمة المكونات القديمة وسجلات نشر DISM وتتبعات التثبيت (CbsPersist)",
                    "es" => "Registros antiguos de servicio de componentes y despliegue DISM",
                    "fr" => "Journaux de maintenance des composants et déploiement DISM",
                    "de" => "Veraltete Component-Based Servicing Protokolle, DISM-Logs & Setup-Spuren",
                    _ => "Stale Component-Based Servicing logs, DISM deployment logs & setup traces (CbsPersist)"
                };
                break;

            case "CrashDumps":
                target.Name = CurrentLanguage switch {
                    "ar" => "تقارير أخطاء النظام وسجلات الانهيار",
                    "es" => "Informes de Errores y Volcados",
                    "fr" => "Rapports d'Erreurs et Vidages",
                    "de" => "Fehlerberichte & Speicherabdrücke",
                    _ => "Error Reports & Crash Dumps"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "سجلات تقارير أخطاء ويندوز (WER) وتفريغات ذاكرة العمليات عند الانهيار",
                    "es" => "Registros de Windows Error Reporting y volcados de memoria",
                    "fr" => "Journaux Windows Error Reporting et vidages mémoire processus",
                    "de" => "Windows Error Reporting Protokolle und Prozess-Speicherabdrücke (WER / Dumps)",
                    _ => "Windows Error Reporting logs & process memory dumps (WER / Dumps)"
                };
                break;

            case "Thumbnails":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش مصغرات مستكشف الملفات",
                    "es" => "Caché de Miniaturas de Windows",
                    "fr" => "Cache des Miniatures de l'Explorateur",
                    "de" => "Explorer Miniaturansichten-Cache",
                    _ => "Explorer Thumbnail Cache"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "قواعد بيانات مصغرات الصور ومقاطع الفيديو المؤقتة (thumbcache_*.db)",
                    "es" => "Bases de datos de miniaturas de fotos y videos",
                    "fr" => "Bases de données de miniatures d'images et vidéos",
                    "de" => "Gecachte Bild- & Video-Miniaturansichten (thumbcache_*.db)",
                    _ => "Cached image & video thumbnail databases (thumbcache_*.db)"
                };
                break;

            case "RecycleBin":
                target.Name = CurrentLanguage switch {
                    "ar" => "سلة المحذوفات لجميع الأقراص",
                    "es" => "Papelera de Reciclaje (Todos los Discos)",
                    "fr" => "Corbeille Windows (Tous les Disques)",
                    "de" => "Windows Papierkorb (Alle Laufwerke)",
                    _ => "Windows Recycle Bin"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "تفريغ سلة المحذوفات عبر جميع الأقراص المتصلة باستخدام Windows Shell API",
                    "es" => "Papeleras de todas las unidades físicas mediante Windows Shell API",
                    "fr" => "Corbeilles de tous les disques physiques via l'API Windows Shell",
                    "de" => "Papierkörbe aller physischen Laufwerke über die Windows-Shell-API",
                    _ => "All physical drive Recycle Bins via Windows Shell API (SHEmptyRecycleBin)"
                };
                break;

            case "OrphanedAppData":
                target.Name = CurrentLanguage switch {
                    "ar" => "بقايا البرامج المحذوفة غير المثبتة",
                    "es" => "Restos de Apps Desinstaladas",
                    "fr" => "Résidus d'Applications Désinstallées",
                    "de" => "Reste Deinstallierter Programme",
                    _ => "Orphaned AppData Leftovers"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "بقايا برامج",
                    "es" => "Huérfanos",
                    "fr" => "Orphelins",
                    "de" => "Verwaist",
                    _ => "Residuals"
                };
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟡 بقايا محققة",
                    "es" => "🟡 Restos Verificados",
                    "fr" => "🟡 Résidus Vérifiés",
                    "de" => "🟡 Verifizierte Reste",
                    _ => "🟡 Verified Leftovers"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "مجلدات البرامج المحذوفة المتبقية في AppData والمطابقة مع سجل إلغاء التثبيت في ويندوز",
                    "es" => "Carpetas de AppData huérfanas cotejadas con el Registro de Desinstalación",
                    "fr" => "Dossiers AppData résiduels vérifiés avec le Registre de Désinstallation",
                    "de" => "Verwaiste AppData-Ordner abgeglichen mit der Windows-Deinstallationsregistrierung",
                    _ => "Residual AppData folders from uninstalled programs verified against Windows Registry"
                };
                break;
        }
    }
}
