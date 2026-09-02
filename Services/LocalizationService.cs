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
            ["ReclaimableSpace"] = "RECLAIMABLE JUNK & APPDATA",
            ["SelectSafe"] = "🟢 Select 100% Safe",
            ["SelectAll"] = "Select All",
            ["Clear"] = "Clear",
            ["Rescan"] = "Rescan",
            ["SafetyShield"] = "Safety Shield (>24h Old)",
            ["ActivityLog"] = "Activity Log",
            ["ExportReport"] = "Export Report",
            ["CleanSelected"] = "Clean Selected",
            ["ConfirmTitle"] = "Confirm Precision Cleanup",
            ["ConfirmSubtitle"] = "Ready to purge selected disposable cache files",
            ["StartCleanup"] = "Start Cleanup",
            ["Cancel"] = "Cancel",
            ["CompletedTitle"] = "Cleanup Completed!",
            ["Awesome"] = "Awesome!",
            ["FilesDeleted"] = "FILES DELETED",
            ["FoldersPurged"] = "FOLDERS PURGED",
            ["TimeElapsed"] = "TIME ELAPSED"
        },
        ["ar"] = new()
        {
            ["AppTitle"] = "ديلتيمبو",
            ["AppSubtitle"] = "حارس نظام ويندوز وملفات المستخدم",
            ["ReclaimableSpace"] = "المساحة القابلة للاسترداد",
            ["SelectSafe"] = "🟢 تحديد الآمن 100%",
            ["SelectAll"] = "تحديد الكل",
            ["Clear"] = "مسح التحديد",
            ["Rescan"] = "إعادة الفحص",
            ["SafetyShield"] = "درع الأمان (أقدم من 24 ساعة)",
            ["ActivityLog"] = "سجل العمليات",
            ["ExportReport"] = "تصدير التقرير",
            ["CleanSelected"] = "تنظيف المحدد",
            ["ConfirmTitle"] = "تأكيد التنظيف الدقيق",
            ["ConfirmSubtitle"] = "جاهز لمسح الملفات المؤقتة المحددة بأمان",
            ["StartCleanup"] = "بدء التنظيف",
            ["Cancel"] = "إلغاء",
            ["CompletedTitle"] = "تم التنظيف بنجاح!",
            ["Awesome"] = "رائع وممتاز!",
            ["FilesDeleted"] = "الملفات المحذوفة",
            ["FoldersPurged"] = "المجلدات الممسوحة",
            ["TimeElapsed"] = "الوقت المستغرق"
        },
        ["es"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Guardián de Windows y Perfiles de Usuario",
            ["ReclaimableSpace"] = "ESPACIO RECUPERABLE",
            ["SelectSafe"] = "🟢 100% Seguro",
            ["SelectAll"] = "Seleccionar Todo",
            ["Clear"] = "Limpiar",
            ["Rescan"] = "Reescanear",
            ["SafetyShield"] = "Escudo de Seguridad (>24h)",
            ["ActivityLog"] = "Registro de Actividad",
            ["ExportReport"] = "Exportar Informe",
            ["CleanSelected"] = "Limpiar Seleccionado",
            ["ConfirmTitle"] = "Confirmar Limpieza",
            ["ConfirmSubtitle"] = "Listo para purgar archivos temporales",
            ["StartCleanup"] = "Comenzar Limpieza",
            ["Cancel"] = "Cancelar",
            ["CompletedTitle"] = "¡Limpieza Completada!",
            ["Awesome"] = "¡Genial!",
            ["FilesDeleted"] = "ARCHIVOS ELIMINADOS",
            ["FoldersPurged"] = "CARPETAS PURGADAS",
            ["TimeElapsed"] = "TIEMPO TRANSCURRIDO"
        },
        ["fr"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Gardien de Windows et Profils Utilisateur",
            ["ReclaimableSpace"] = "ESPACE RÉCUPÉRABLE",
            ["SelectSafe"] = "🟢 100% Sûr",
            ["SelectAll"] = "Tout Sélectionner",
            ["Clear"] = "Effacer",
            ["Rescan"] = "Re-scanner",
            ["SafetyShield"] = "Bouclier de Sécurité (>24h)",
            ["ActivityLog"] = "Journal d'Activité",
            ["ExportReport"] = "Exporter le Rapport",
            ["CleanSelected"] = "Nettoyer la Sélection",
            ["ConfirmTitle"] = "Confirmer le Nettoyage",
            ["ConfirmSubtitle"] = "Prêt à purger les fichiers de cache",
            ["StartCleanup"] = "Démarrer",
            ["Cancel"] = "Annuler",
            ["CompletedTitle"] = "Nettoyage Terminé!",
            ["Awesome"] = "Super!",
            ["FilesDeleted"] = "FICHIERS SUPPRIMÉS",
            ["FoldersPurged"] = "DOSSIERS PURGÉS",
            ["TimeElapsed"] = "TEMPS ÉCOULÉ"
        },
        ["de"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Windows & Benutzerprofil Wächter",
            ["ReclaimableSpace"] = "WIEDERGEWINNBARER SPEICHER",
            ["SelectSafe"] = "🟢 100% Sicher",
            ["SelectAll"] = "Alles Auswählen",
            ["Clear"] = "Leeren",
            ["Rescan"] = "Neu Scannen",
            ["SafetyShield"] = "Sicherheitsschild (>24h)",
            ["ActivityLog"] = "Aktivitätsprotokoll",
            ["ExportReport"] = "Bericht Exportieren",
            ["CleanSelected"] = "Bereinigen",
            ["ConfirmTitle"] = "Bereinigung Bestätigen",
            ["ConfirmSubtitle"] = "Bereit zum Bereinigen der temporären Dateien",
            ["StartCleanup"] = "Bereinigung Starten",
            ["Cancel"] = "Abbrechen",
            ["CompletedTitle"] = "Bereinigung Abgeschlossen!",
            ["Awesome"] = "Großartig!",
            ["FilesDeleted"] = "GELÖSCHTE DATEIEN",
            ["FoldersPurged"] = "GELÖSCHTE ORDNER",
            ["TimeElapsed"] = "BENÖTIGTE ZEIT"
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
}
