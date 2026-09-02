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
            ["DriveOsLabel"] = "OS Drive",
            ["DriveFree"] = "Free",
            ["DriveOf"] = "free of",
            ["SelectSafe"] = "🟢 Select 100% Safe",
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
            ["ConfirmSummary"] = "Purging selected categories. System integrity, active accounts, and work documents are strictly protected.",
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
            ["HeroScanSubtext"] = "فحص شامل للملفات المؤقتة وكاش كروت الشاشة والبرامج",
            ["DriveOsLabel"] = "قرص النظام",
            ["DriveFree"] = "متاح",
            ["DriveOf"] = "متاح من إجمالي",
            ["SelectSafe"] = "🟢 تحديد الآمن 100%",
            ["SelectAll"] = "تحديد الكل",
            ["Clear"] = "مسح التحديد",
            ["Rescan"] = "إعادة الفحص (F5)",
            ["SafetyShield"] = "درع الأمان (أقدم من 24 ساعة)",
            ["ReadyStatus"] = "جاهز لبدء عملية التنظيف الدقيق",
            ["ActivityLog"] = "سجل العمليات",
            ["HideLog"] = "إخفاء السجل",
            ["ExportReport"] = "تصدير التقرير",
            ["CleanSelected"] = "تنظيف المحدد",
            ["Cancel"] = "إلغاء",
            ["Inspect"] = "معاينة",
            ["ConfirmTitle"] = "تأكيد التنظيف الدقيق",
            ["ConfirmSubtitle"] = "جاهز لمسح الملفات المؤقتة المحددة بأمان تام",
            ["ConfirmReclaimableLabel"] = "المساحة المقدرة للاسترداد",
            ["ConfirmShieldOn"] = "🟢 درع الأمان: مفعّل",
            ["ConfirmShieldOff"] = "⚠️ درع الأمان: معطّل",
            ["ConfirmSummary"] = "تنظيف الأقسام المحددة. بياناتك الشخصية وحساباتك وملفاتك محمية 100%.",
            ["StartCleanup"] = "بدء التنظيف",
            ["CompletedTitle"] = "تم التنظيف بنجاح!",
            ["SuccessfullyReclaimed"] = "تم استرداد مساحة قدرها",
            ["Awesome"] = "رائع وممتاز!",
            ["FilesDeleted"] = "الملفات المحذوفة",
            ["FoldersPurged"] = "المجلدات الممسوحة",
            ["TimeElapsed"] = "الوقت المستغرق",
            ["InspectorTitle"] = "معاينة أكبر الملفات المؤقتة",
            ["InspectorSubtitle"] = "عرض الملفات الأكبر حجماً المكتشفة في هذا القسم",
            ["CloseInspector"] = "إغلاق المعاينة (Esc)"
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
            ["InspectorTitle"] = "Inspecteur de Fichiers Volumineux",
            ["InspectorSubtitle"] = "Affichage des fichiers les plus volumineux",
            ["CloseInspector"] = "Fermer (Esc)"
        },
        ["de"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Windows & Benutzerprofil Wächter",
            ["AdminLabel"] = "Administrator",
            ["ReclaimableSpace"] = "WIEDERGEWINNBARER SPEICHER",
            ["HeroScanSubtext"] = "Tiefenscan von AppData, GPU-Shadern und temporären Daten",
            ["DriveOsLabel"] = "Systemlaufwerk",
            ["DriveFree"] = "Frei",
            ["DriveOf"] = "frei von",
            ["SelectSafe"] = "🟢 100% Sicher",
            ["SelectAll"] = "Alles Auswählen",
            ["Clear"] = "Leeren",
            ["Rescan"] = "Neu Scannen (F5)",
            ["SafetyShield"] = "Sicherheitsschild (>24h)",
            ["ReadyStatus"] = "Bereit für Präzisionsreinigung",
            ["ActivityLog"] = "Aktivitätsprotokoll",
            ["HideLog"] = "Protokoll Ausblenden",
            ["ExportReport"] = "Bericht Exportieren",
            ["CleanSelected"] = "Auswahl Bereinigen",
            ["Cancel"] = "Abbrechen",
            ["Inspect"] = "Prüfen",
            ["ConfirmTitle"] = "Bereinigung Bestätigen",
            ["ConfirmSubtitle"] = "Bereit zum Bereinigen der ausgewählten Cache-Dateien",
            ["ConfirmReclaimableLabel"] = "GESCHÄTZTER FREIER SPEICHER",
            ["ConfirmShieldOn"] = "🟢 Sicherheitsschild: AN",
            ["ConfirmShieldOff"] = "⚠️ Sicherheitsschild: AUS",
            ["ConfirmSummary"] = "Ausgewählte Kategorien werden bereinigt. Benutzerdaten bleiben 100% geschützt.",
            ["StartCleanup"] = "Bereinigung Starten",
            ["CompletedTitle"] = "Bereinigung Abgeschlossen!",
            ["SuccessfullyReclaimed"] = "Erfolgreich Wiederhergestellt",
            ["Awesome"] = "Großartig!",
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
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟢 كاش آمن 100%",
                    "es" => "🟢 100% Seguro",
                    "fr" => "🟢 100% Sûr",
                    "de" => "🟢 100% Sicher",
                    _ => "🟢 100% Safe Cache"
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
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟢 كاش آمن 100%",
                    "es" => "🟢 100% Seguro",
                    "fr" => "🟢 100% Sûr",
                    "de" => "🟢 100% Sicher",
                    _ => "🟢 100% Safe Cache"
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
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟢 كاش آمن 100%",
                    "es" => "🟢 100% Seguro",
                    "fr" => "🟢 100% Sûr",
                    "de" => "🟢 100% Sicher",
                    _ => "🟢 100% Safe Cache"
                };
                break;

            case "WinUpdate":
                target.Name = CurrentLanguage switch {
                    "ar" => "حزم تحديثات ويندوز المؤقتة",
                    "es" => "Caché de Windows Update",
                    "fr" => "Téléchargements Windows Update",
                    "de" => "Windows Update Download-Cache",
                    _ => "Windows Update Delivery Cache"
                };
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟢 كاش آمن 100%",
                    "es" => "🟢 100% Seguro",
                    "fr" => "🟢 100% Sûr",
                    "de" => "🟢 100% Sicher",
                    _ => "🟢 100% Safe Cache"
                };
                break;

            case "GpuShaders":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش كروت الشاشة (NVIDIA/AMD/Intel)",
                    "es" => "Shaders de GPU (NVIDIA/AMD/Intel)",
                    "fr" => "Shaders GPU (NVIDIA/AMD/Intel)",
                    "de" => "DirectX & GPU Shader-Pools",
                    _ => "DirectX & GPU Shader Pools"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "كارت الشاشة",
                    "es" => "GPU Shaders",
                    "fr" => "GPU Shaders",
                    "de" => "GPU-Shader",
                    _ => "GPU Shaders"
                };
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟢 كاش آمن 100%",
                    "es" => "🟢 100% Seguro",
                    "fr" => "🟢 100% Sûr",
                    "de" => "🟢 100% Sicher",
                    _ => "🟢 100% Safe Cache"
                };
                break;

            case "DesktopApps":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش برامج سطح المكتب (Discord/Spotify)",
                    "es" => "Caché de Apps (Discord/Spotify)",
                    "fr" => "Cache d'Applications (Discord/Spotify)",
                    "de" => "Desktop- & Electron-App Caches",
                    _ => "Desktop & Electron App Caches"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "البرامج",
                    "es" => "Apps",
                    "fr" => "Apps",
                    "de" => "Apps",
                    _ => "Desktop Apps"
                };
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟢 كاش آمن 100%",
                    "es" => "🟢 100% Seguro",
                    "fr" => "🟢 100% Sûr",
                    "de" => "🟢 100% Sicher",
                    _ => "🟢 100% Safe Cache"
                };
                break;

            case "BrowserCaches":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش متصفحات الويب (Chrome/Edge/Brave)",
                    "es" => "Caché de Navegadores Web",
                    "fr" => "Cache des Navigateurs Web",
                    "de" => "Webbrowser-Caches",
                    _ => "Web Browser Caches"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "المتصفحات",
                    "es" => "Navegadores",
                    "fr" => "Navigateurs",
                    "de" => "Browser",
                    _ => "Web Browsers"
                };
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟢 كاش آمن 100%",
                    "es" => "🟢 100% Seguro",
                    "fr" => "🟢 100% Sûr",
                    "de" => "🟢 100% Sicher",
                    _ => "🟢 100% Safe Cache"
                };
                break;

            case "DeveloperCaches":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش أدوات التطوير (pip/npm/gradle)",
                    "es" => "Caché de Desarrollador (npm/pip)",
                    "fr" => "Caches de Développement (npm/pip)",
                    "de" => "Entwickler- & Paket-Caches",
                    _ => "Developer & Package Caches"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "المطورين",
                    "es" => "Desarrollo",
                    "fr" => "Développement",
                    "de" => "Entwicklung",
                    _ => "Development"
                };
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟢 كاش آمن 100%",
                    "es" => "🟢 100% Seguro",
                    "fr" => "🟢 100% Sûr",
                    "de" => "🟢 100% Sicher",
                    _ => "🟢 100% Safe Cache"
                };
                break;

            case "WerCrash":
                target.Name = CurrentLanguage switch {
                    "ar" => "تقارير أخطاء النظام وسجلات الانهيار",
                    "es" => "Informes de Errores y Volcados WER",
                    "fr" => "Rapports d'Erreurs Windows (WER)",
                    "de" => "Fehlerberichte & Speicherabdrücke",
                    _ => "Windows Error Reports & Crash Dumps"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "تقارير الأخطاء",
                    "es" => "Diagnóstico",
                    "fr" => "Diagnostic",
                    "de" => "Diagnose",
                    _ => "Diagnostics"
                };
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟢 كاش آمن 100%",
                    "es" => "🟢 100% Seguro",
                    "fr" => "🟢 100% Sûr",
                    "de" => "🟢 100% Sicher",
                    _ => "🟢 100% Safe Cache"
                };
                break;

            case "ExplorerThumbs":
                target.Name = CurrentLanguage switch {
                    "ar" => "كاش مصغرات الصور ومستكشف الملفات",
                    "es" => "Caché de Miniaturas de Windows",
                    "fr" => "Cache des Miniatures de l'Explorateur",
                    "de" => "Explorer Miniaturansichten-Cache",
                    _ => "Explorer Thumbnail Caches"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "مستكشف الملفات",
                    "es" => "Explorador",
                    "fr" => "Explorateur",
                    "de" => "Explorer",
                    _ => "File Explorer"
                };
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟢 كاش آمن 100%",
                    "es" => "🟢 100% Seguro",
                    "fr" => "🟢 100% Sûr",
                    "de" => "🟢 100% Sicher",
                    _ => "🟢 100% Safe Cache"
                };
                break;

            case "RecycleBin":
                target.Name = CurrentLanguage switch {
                    "ar" => "سلة المحذوفات لجميع الأقراص",
                    "es" => "Papelera de Reciclaje de Windows",
                    "fr" => "Corbeille Windows (Tous les disques)",
                    "de" => "Windows Papierkorb (Alle Laufwerke)",
                    _ => "Windows Recycle Bin (All Drives)"
                };
                target.Category = CurrentLanguage switch {
                    "ar" => "التخزين",
                    "es" => "Almacenamiento",
                    "fr" => "Stockage",
                    "de" => "Speicher",
                    _ => "Storage"
                };
                target.SafetyBadge = CurrentLanguage switch {
                    "ar" => "🟢 كاش آمن 100%",
                    "es" => "🟢 100% Seguro",
                    "fr" => "🟢 100% Sûr",
                    "de" => "🟢 100% Sicher",
                    _ => "🟢 100% Safe Cache"
                };
                break;

            case "OrphanedApps":
                target.Name = CurrentLanguage switch {
                    "ar" => "بقايا البرامج المحذوفة غير المثبتة",
                    "es" => "Restos de Apps Desinstaladas",
                    "fr" => "Résidus d'Applications Désinstallées",
                    "de" => "Reste Deinstallierter Programme",
                    _ => "Verified Uninstalled Software Leftovers"
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
                break;
        }
    }
}
