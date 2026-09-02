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
            ["InspectorSubtitle"] = "Archivos individuales descubiertos en esta categoría",
            ["CloseInspector"] = "Cerrar Inspector (Esc)"
        },
        ["fr"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Gardien de Windows et Profil Utilisateur",
            ["AdminLabel"] = "Administrateur",
            ["ReclaimableSpace"] = "ESPACE RÉCUPÉRABLE",
            ["HeroScanSubtext"] = "Scan profond d'AppData, shaders GPU et fichiers temporaires",
            ["DriveOsLabel"] = "Disque Système",
            ["DriveFree"] = "Libre",
            ["DriveOf"] = "libre sur",
            ["SelectSafe"] = "🟢 100% Sécurisé",
            ["SelectAll"] = "Tout Sélectionner",
            ["Clear"] = "Effacer",
            ["Rescan"] = "Re-scanner (F5)",
            ["SafetyShield"] = "Bouclier de Sécurité (>24h)",
            ["ReadyStatus"] = "Prêt pour le nettoyage de précision",
            ["ActivityLog"] = "Journal d'Activité",
            ["HideLog"] = "Masquer le Journal",
            ["ExportReport"] = "Exporter le Rapport",
            ["CleanSelected"] = "Nettoyer Sélection",
            ["Cancel"] = "Annuler",
            ["Inspect"] = "Inspecter",
            ["ConfirmTitle"] = "Confirmer le Nettoyage",
            ["ConfirmSubtitle"] = "Prêt à purger les fichiers de cache sélectionnés",
            ["ConfirmReclaimableLabel"] = "ESPACE ESTIMÉ RÉCUPÉRABLE",
            ["ConfirmShieldOn"] = "🟢 Bouclier: ACTIF",
            ["ConfirmShieldOff"] = "⚠️ Bouclier: INACTIF",
            ["ConfirmSummary"] = "Nettoyage en cours. Vos comptes et documents restent strictement protégés.",
            ["StartCleanup"] = "Lancer le Nettoyage",
            ["CompletedTitle"] = "Nettoyage Terminé !",
            ["SuccessfullyReclaimed"] = "Espace Récupéré",
            ["Awesome"] = "Super !",
            ["FilesDeleted"] = "FICHIERS SUPPRIMÉS",
            ["FoldersPurged"] = "DOSSIERS PURGÉS",
            ["TimeElapsed"] = "TEMPS ÉCOULÉ",
            ["InspectorTitle"] = "Inspecteur des Fichiers Volumineux",
            ["InspectorSubtitle"] = "Fichiers volumineux découverts dans cette catégorie",
            ["CloseInspector"] = "Fermer (Esc)"
        },
        ["de"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Windows- & Benutzerprofil-Wächter",
            ["AdminLabel"] = "Administrator",
            ["ReclaimableSpace"] = "WIEDERHERSTELLBARER SPEICHER",
            ["HeroScanSubtext"] = "Tiefenscan von Benutzerprofil, GPU-Shadern, AppData & Temp-Dateien",
            ["DriveOsLabel"] = "Systemlaufwerk",
            ["DriveFree"] = "Frei",
            ["DriveOf"] = "frei von",
            ["SelectSafe"] = "🟢 100% Sicher",
            ["SelectAll"] = "Alles Auswählen",
            ["Clear"] = "Auswahl Aufheben",
            ["Rescan"] = "Neu Scannen (F5)",
            ["SafetyShield"] = "Sicherheitsschild (>24h alt)",
            ["ReadyStatus"] = "Bereit für Präzisionsbereinigung",
            ["ActivityLog"] = "Aktivitätsprotokoll",
            ["HideLog"] = "Protokoll Ausblenden",
            ["ExportReport"] = "Bericht Exportieren",
            ["CleanSelected"] = "Ausgewählte Bereinigen",
            ["Cancel"] = "Abbrechen",
            ["Inspect"] = "Inspizieren",
            ["ConfirmTitle"] = "Präzisionsbereinigung Bestätigen",
            ["ConfirmSubtitle"] = "Bereit zum Löschen temporärer Cache-Dateien",
            ["ConfirmReclaimableLabel"] = "GESCHÄTZTER FREIER SPEICHER",
            ["ConfirmShieldOn"] = "🟢 Sicherheitsschild: AN",
            ["ConfirmShieldOff"] = "⚠️ Sicherheitsschild: AUS",
            ["ConfirmSummary"] = "Bereinigt ausgewählte Kategorien. Konten und persönliche Dokumente bleiben 100% geschützt.",
            ["StartCleanup"] = "Bereinigung Starten",
            ["CompletedTitle"] = "Bereinigung Abgeschlossen!",
            ["SuccessfullyReclaimed"] = "Erfolgreich Freigegeben",
            ["Awesome"] = "Klasse!",
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

            case "WinUpgradeLeftovers":
                target.Name = CurrentLanguage switch {
                    "ar" => "بقايا ترقيات وتثبيت ويندوز السابقة",
                    "es" => "Restos de Actualizaciones de Windows",
                    "fr" => "Résidus de Mises à Niveau Windows",
                    "de" => "Windows Upgrade- & Setup-Rückstände",
                    _ => "Windows Upgrade & Setup Leftovers"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "النظام والتحديثات",
                    "es" => "Sistema y SO",
                    "fr" => "Système & OS",
                    "de" => "System & OS",
                    _ => "System & OS"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "بقايا ترقيات النظام السابقة ($WINDOWS.~BT و $WINDOWS.~WS و ESD ومسودات الإعداد)",
                    "es" => "Restos de instalaciones anteriores, $WINDOWS.~BT, $WINDOWS.~WS y ESD",
                    "fr" => "Restes d'anciennes installations d'OS, $WINDOWS.~BT, $WINDOWS.~WS, ESD",
                    "de" => "Alte OS-Installationsreste, $WINDOWS.~BT, $WINDOWS.~WS, ESD und Setup-Dateien",
                    _ => "Old OS installation leftovers, $WINDOWS.~BT, $WINDOWS.~WS, ESD, and Setup scratchpads"
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

            case "WinComponentCaches":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش مكونات وخطوط ويندوز",
                    "es" => "Cachés de Componentes y Fuentes de Windows",
                    "fr" => "Caches Composants & Polices Windows",
                    "de" => "Windows Komponenten- & Schriftarten-Caches",
                    _ => "Windows Component & Font Caches"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "النظام والتحديثات",
                    "es" => "Sistema y SO",
                    "fr" => "Système & OS",
                    "de" => "System & OS",
                    _ => "System & OS"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "كاش خطوط ويندوز FontCache ومجلد البرامج المنزلة ومسودات WinSxS و DISM و BranchCache",
                    "es" => "FontCache de Windows, Archivos de programa descargados, temporal WinSxS y DISM",
                    "fr" => "FontCache Windows, fichiers téléchargés, WinSxS temp, DISM et BranchCache",
                    "de" => "Windows FontCache, Downloaded Program Files, WinSxS-Temp, DISM & BranchCache",
                    _ => "Windows FontCache, Downloaded Program Files, WinSxS temp, DISM scratch & BranchCache"
                };
                break;

            case "DeviceDriverPackages":
                target.Name = CurrentLanguage switch {
                    "ar" => "حزم تعاريف الأجهزة وتحديثات GPU",
                    "es" => "Paquetes de Controladores y GPU",
                    "fr" => "Pilotes Périphériques & Mises à Jour GPU",
                    "de" => "Gerätetreiber-Pakete & GPU-Updates",
                    _ => "Device Driver Packages & GPU Updates"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "النظام والتعريفات",
                    "es" => "Sistema y Controladores",
                    "fr" => "Système & Pilotes",
                    "de" => "System & Treiber",
                    _ => "System & Drivers"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "حزم تحديثات NVIDIA App و AMD و Intel وكاش DriverStore المؤقت",
                    "es" => "Paquetes OTA de NVIDIA App, instaladores AMD e Intel, temp DriverStore",
                    "fr" => "Packages OTA NVIDIA App, installateurs AMD/Intel, DriverStore temp",
                    "de" => "NVIDIA App/GeForce OTA-Treiberpakete, AMD- & Intel-Caches, DriverStore temp",
                    _ => "NVIDIA App/GeForce OTA driver packages, AMD & Intel installer caches, DriverStore temp"
                };
                break;

            case "DefenderAntivirus":
                target.Name = CurrentLanguage switch {
                    "ar" => "سجلات وفحوصات حماية Microsoft Defender",
                    "es" => "Soporte y Análisis de Microsoft Defender",
                    "fr" => "Support & Analyses Microsoft Defender",
                    "de" => "Microsoft Defender Support & Scans",
                    _ => "Microsoft Defender Support & Scans"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "الأمان والسجلات",
                    "es" => "Seguridad y Registros",
                    "fr" => "Sécurité & Journaux",
                    "de" => "Sicherheit & Protokolle",
                    _ => "Security & Logs"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "سجلات تشخيص Defender (MPLog)، النسخ الاحتياطية لتحديثات التواقيع وسجل الفحص",
                    "es" => "Registros MPLog de Defender, copias de seguridad de definiciones y caché de análisis",
                    "fr" => "Journaux de support Defender (MPLog), sauvegardes de définitions et historique d'analyse",
                    "de" => "Defender-Diagnoseprotokolle (MPLog), Definitions-Backups & Scan-Verlauf",
                    _ => "Defender support diagnostic logs (MPLog), definition update backups & scan history cache"
                };
                break;

            case "WinSystemLogs":
                target.Name = CurrentLanguage switch {
                    "ar" => "سجلات تشخيص النظام",
                    "es" => "Registros de Diagnóstico de Windows",
                    "fr" => "Journaux de Diagnostic Système",
                    "de" => "Windows System-Diagnoseprotokolle",
                    _ => "Windows System Diagnostic Logs"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "التشخيص والسجلات",
                    "es" => "Diagnóstico",
                    "fr" => "Diagnostic",
                    "de" => "Diagnose",
                    _ => "Diagnostics"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "سجلات CBS و DISM و Panther و SetupAPI و LogFiles وسجلات التتبع",
                    "es" => "Registros CBS, DISM, Panther, SetupAPI, LogFiles y seguimiento",
                    "fr" => "Journaux CBS, DISM, Panther, SetupAPI, LogFiles et traces",
                    "de" => "CBS, DISM, Panther, SetupAPI, LogFiles (WMI/HTTPERR) und Ablaufverfolgungen",
                    _ => "CBS, DISM, Panther, SetupAPI, LogFiles (WMI/HTTPERR), and tracing logs"
                };
                break;

            case "SystemDumps":
                target.Name = CurrentLanguage switch {
                    "ar" => "تفريغات انهيار النظام وتقارير الكيرنل",
                    "es" => "Volcados de Bloqueo y Minivolcados BSOD",
                    "fr" => "Vidages de Mémoire et Rapports Noyau",
                    "de" => "BSOD-Minidumps & Kernel-Berichte",
                    _ => "BSOD Minidumps & Kernel Reports"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "التشخيص والسجلات",
                    "es" => "Diagnóstico",
                    "fr" => "Diagnostic",
                    "de" => "Diagnose",
                    _ => "Diagnostics"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "تفريغات أخطاء الشاشة الزرقاء (*.dmp) و MEMORY.DMP وتقارير LiveKernelReports",
                    "es" => "Minivolcados BSOD (*.dmp), MEMORY.DMP y LiveKernelReports",
                    "fr" => "Minividages BSOD (*.dmp), MEMORY.DMP et LiveKernelReports",
                    "de" => "Windows Crash-Minidumps (*.dmp), MEMORY.DMP und LiveKernelReports",
                    _ => "Windows crash minidumps (*.dmp), MEMORY.DMP, and LiveKernelReports"
                };
                break;

            case "TemporaryInternetFiles":
                target.Name = CurrentLanguage switch {
                    "ar" => "ملفات الإنترنت المؤقتة و WebCache",
                    "es" => "Archivos Temporales de Internet y WebCache",
                    "fr" => "Fichiers Internet Temporaires & WebCache",
                    "de" => "Temporäre Internetdateien & WebCache",
                    _ => "Temporary Internet Files & WebCache"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "كاش الإنترنت",
                    "es" => "Caché de Internet",
                    "fr" => "Cache Internet",
                    "de" => "Internet-Cache",
                    _ => "Internet Cache"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "كاش INetCache و WebCache ومحتوى شهادات CryptnetUrlCache",
                    "es" => "INetCache de Windows, WebCache y certificados CryptnetUrlCache",
                    "fr" => "INetCache Windows, WebCache et contenu de certificat CryptnetUrlCache",
                    "de" => "Windows INetCache, WebCache und CryptnetUrlCache-Zertifikatinhalte",
                    _ => "Windows INetCache, WebCache, and CryptnetUrlCache certificate content"
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
                    "es" => "Sistema y GPU",
                    "fr" => "Système & GPU",
                    "de" => "System & GPU",
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
                    "ar" => "كاش منصات الألعاب (Steam / Epic / Battle.net / Riot)",
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
                    "ar" => "تنزيلات Steam، كاش Epic Games و Battle.net و EA App وسجلات Riot Games و Roblox",
                    "es" => "Descargas temporales de Steam, caché de Epic Games, Battle.net, Riot y Roblox",
                    "fr" => "Téléchargements Steam, caches web Epic Games, Battle.net, Riot et Roblox",
                    "de" => "Steam Downloads & Shader, Epic Games Webcache, Battle.net, EA App, Riot Games, Roblox",
                    _ => "Steam downloads & shaders, Epic Games webcache, Battle.net, EA App, Riot Games, Roblox"
                };
                break;

            case "MediaCreatorCaches":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش برامج المونتاج والتصميم (Adobe / CapCut / DaVinci)",
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
                    "ar" => "ملفات Media Cache في Adobe و CapCut و DaVinci Resolve وسجلات OBS ومؤقتات Blender",
                    "es" => "Media Cache de Adobe, CapCut, proxy de DaVinci, logs de OBS y Blender temp",
                    "fr" => "Media Cache Adobe, CapCut, proxies DaVinci, logs OBS et Blender temp",
                    "de" => "Adobe Premiere/Photoshop Scratch, CapCut Cache, DaVinci Proxy, OBS Logs, Blender Temp",
                    _ => "Adobe Premiere/After Effects/Photoshop scratch, CapCut cache, DaVinci proxy, OBS logs, Blender temp"
                };
                break;

            case "AppCacheSweeper":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش برامج سطح المكتب (Discord/WhatsApp/Teams)",
                    "es" => "Caché de Apps de Escritorio",
                    "fr" => "Cache des Applications Desktop",
                    "de" => "Desktop-App Cache-Bereiniger",
                    _ => "Desktop Apps Cache Sweeper"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "كاش GPU و Code Cache المؤقت في Discord و Spotify و Slack و VS Code و WhatsApp و Notion",
                    "es" => "Caché GPU y código de Discord, Spotify, Slack, VS Code, WhatsApp y Notion",
                    "fr" => "Cache GPU et code de Discord, Spotify, Slack, VS Code, WhatsApp et Notion",
                    "de" => "Flüchtiger GPU- & Code-Cache in Discord, Spotify, Slack, VS Code, Teams, WhatsApp, Notion",
                    _ => "Disposable GPU & Code Cache in Discord, Spotify, Slack, VS Code, Cursor, Teams, WhatsApp, Notion"
                };
                break;

            case "WinStoreAppCaches":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش تطبيقات متجر ويندوز و UWP",
                    "es" => "Caché de Apps de la Tienda y UWP",
                    "fr" => "Caches Applications Windows Store & UWP",
                    "de" => "Windows Store-Apps & UWP Caches",
                    _ => "Windows Store Apps & UWP Caches"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "تطبيقات المتجر",
                    "es" => "Apps de Tienda",
                    "fr" => "Applications Store",
                    "de" => "Store-Apps",
                    _ => "Store Apps"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "ملفات LocalCache و INetCache المؤقتة لحزم تطبيقات متجر ويندوز (Teams الجديد و Xbox وغيرها)",
                    "es" => "LocalCache e INetCache temporales de paquetes Windows Store (Teams, Xbox, etc.)",
                    "fr" => "LocalCache et INetCache temporaires des applications Store (New Teams, Xbox, etc.)",
                    "de" => "Temporärer LocalCache & INetCache über Windows Store-Pakete (New Teams, Xbox, etc.)",
                    _ => "Temporary LocalCache & INetCache across Windows Store packages (New Teams, Xbox, WhatsApp, etc.)"
                };
                break;

            case "BrowserCaches":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش متصفحات الإنترنت متعددة الحسابات",
                    "es" => "Caché de Navegadores Web Multiprofil",
                    "fr" => "Cache des Navigateurs Web Multi-Profils",
                    "de" => "Webbrowser-Caches (Multi-Profil)",
                    _ => "Web Browsers Cache Pool"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "فحص عميق وشامل لكافة حسابات ومتصفحات Chrome و Edge و Brave و Opera و Firefox و Arc و Vivaldi",
                    "es" => "Caché web y shaders de todos los perfiles de Chrome, Edge, Brave, Opera, Firefox, Arc, Vivaldi",
                    "fr" => "Cache web et shaders multi-profils de Chrome, Edge, Brave, Opera, Firefox, Arc, Vivaldi",
                    "de" => "Chrome, Edge, Brave, Opera, Firefox, Arc, Vivaldi Multi-Profil Web- & Shader-Caches",
                    _ => "Chrome, Edge, Brave, Opera, Firefox, Arc, Vivaldi multi-profile web & shader cache (logins preserved)"
                };
                break;

            case "DevPackageCaches":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش حزم وأدوات المطورين",
                    "es" => "Caché de Paquetes de Desarrollo",
                    "fr" => "Caches de Packages Développeur",
                    "de" => "Entwickler- & Paket-Caches",
                    _ => "Developer & Package Caches"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "كاش تنزيلات الحزم في pip و npm و yarn و pnpm و NuGet و Cargo و Go و Bun و .NET",
                    "es" => "Descargas temporales de pip, npm, yarn, pnpm, NuGet, Cargo, Go, Bun y .NET",
                    "fr" => "Téléchargements de packages pip, npm, yarn, pnpm, NuGet, Cargo, Go, Bun et .NET",
                    "de" => "pip, npm, yarn, pnpm, NuGet, .gradle, Cargo, Go build, Bun, Deno und .NET Caches",
                    _ => "pip, npm, yarn, pnpm, NuGet, .gradle, Cargo, Go build, Bun, Deno, and .NET temp caches"
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

            case "CrashDumps":
                target.Name = CurrentLanguage switch {
                    "ar" => "تقارير أخطاء النظام وسجلات الانهيار",
                    "es" => "Informes de Errores y Volcados",
                    "fr" => "Rapports d'Erreurs et Vidages",
                    "de" => "Fehlerberichte & Speicherabdrücke",
                    _ => "Windows Error Reports (WER)"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "سجلات تقارير أخطاء ويندوز (WER) وتفريغات ذاكرة العمليات عند الانهيار",
                    "es" => "Registros de Windows Error Reporting y volcados de memoria",
                    "fr" => "Journaux Windows Error Reporting et vidages mémoire processus",
                    "de" => "Windows Error Reporting Protokolle und Prozess-Speicherabdrücke (WER / Dumps)",
                    _ => "Windows Error Reporting logs & diagnostic queues (WER ReportArchive/ReportQueue)"
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

            case "SystemUsageTraces":
                target.Name = CurrentLanguage switch {
                    "ar" => "آثار استخدام النظام ومستكشف الملفات",
                    "es" => "Rastros de Uso del Sistema y Explorador",
                    "fr" => "Traces d'Utilisation Système & Explorateur",
                    "de" => "System- & Explorer-Nutzungsspuren",
                    _ => "System & Explorer Usage Traces"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "آثار الخصوصية",
                    "es" => "Privacidad",
                    "fr" => "Confidentialité",
                    "de" => "Privatsphäre",
                    _ => "Privacy Traces"
                };
                target.Description = CurrentLanguage switch {
                    "ar" => "اختصارات العناصر الأخيرة وقوائم الانتقال السريع (Jump Lists)",
                    "es" => "Accesos directos a elementos recientes y listas de accesos rápidos Jump Lists",
                    "fr" => "Raccourcis d'éléments récents et listes de raccourcis Jump Lists",
                    "de" => "Zuletzt verwendete Elemente und Jump-Listen (Automatic/CustomDestinations)",
                    _ => "Recent items shortcuts, AutomaticDestinations, and CustomDestinations Jump Lists"
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
