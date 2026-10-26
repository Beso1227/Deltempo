using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public static class LocalizationService
{
    private static string _currentLanguage = "en";

    /// <summary>
    /// Raised after <see cref="CurrentLanguage"/> actually changed value. Shells
    /// subscribe to re-apply their static copy; WPF still calls ApplyLocalization
    /// directly because that pass also refreshes measured totals.
    /// </summary>
    public static event EventHandler? LanguageChanged;

    /// <summary>
    /// Active language code ("en", "ar", "es", "fr", "de"). Assigning the same
    /// value is a no-op, so a settings round-trip cannot re-raise the event.
    /// </summary>
    public static string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (string.Equals(_currentLanguage, value, StringComparison.Ordinal))
            {
                return;
            }

            _currentLanguage = value;
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// True for the one language that reads right to left. Shells mirror their
    /// layout on this rather than testing the language code themselves.
    /// </summary>
    public static bool IsRightToLeft => string.Equals(CurrentLanguage, "ar", StringComparison.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["en"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Windows & User Profile Guardian",
            ["AdminLabel"] = "Admin",
            ["ReclaimableSpace"] = "RECLAIMABLE JUNK & APPDATA",
            ["HeroScanSubtext"] = "Deep scan of User Profile, GPU Shaders, AppData & System Temp",
            ["DriveOsLabel"] = "OS Drive (C:)",
            ["DriveFree"] = "Free",
            ["DriveOf"] = "free of",
            ["SelectSafe"] = "Select 100% Safe",
            ["SmartClean"] = "Smart Clean",
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
            ["CloseInspector"] = "Close Inspector (Esc)",
            ["NavCleaner"] = "Cleaner",
            ["NavLargeFiles"] = "Large Files",
            ["NavStartup"] = "Startup",
            ["NavProcesses"] = "Processes",
            ["NavMemory"] = "Memory",
            ["NavRepair"] = "Repair",
            ["NavApps"] = "Apps",
            ["NavAbout"] = "About",
            ["NavForceDelete"] = "Force Delete",
            ["NavSettings"] = "Settings",
            ["HeroScanNow"] = "Scan Now",
            ["HeroOneClick"] = "1-Click Deep Clean",
            ["HeroGameBoost"] = "Game Boost",
            ["HeroWinUtil"] = "CTT WinUtil",
            ["DriveLargeFiles"] = "Large Files",
            ["HeroRamLabel"] = "RAM Memory",
            ["HeroRamBtn"] = "RAM Cleaner",
            ["HeroBoostRam"] = "Quick Boost",
            ["FilterAll"] = "All Scopes",
            ["FilterSafe"] = "Safe Only",
            ["FilterSystem"] = "System & Drivers",
            ["FilterGaming"] = "Gaming",
            ["FilterMedia"] = "Media & Apps",
            ["FilterDev"] = "Dev Tools",
            ["FilterRulepack"] = "Rulepacks",
            ["SearchPlaceholder"] = "Search 25+ scopes...",
            ["JumpToTool"] = "Jump to a tool",
            ["PageEyebrowCleanup"] = "CLEANUP",
            ["PageEyebrowDisk"] = "DISK SPACE",
            ["PageEyebrowBoot"] = "BOOT AND LOGIN",
            ["PageEyebrowProcesses"] = "PROCESSES",
            ["PageEyebrowPerformance"] = "PERFORMANCE",
            ["PageEyebrowIntegrity"] = "INTEGRITY",
            ["PageEyebrowSoftware"] = "SOFTWARE",
            ["PageEyebrowLastResort"] = "LAST RESORT",
            ["PageEyebrowPreferences"] = "PREFERENCES",
            ["PageEyebrowAbout"] = "ABOUT",
            ["PageDescCleaner"] = "Temp files, caches and junk Windows can rebuild. Nothing is removed until you press Clean.",
            ["PageDescLargeFiles"] = "Files above your size threshold. Protected system locations are listed but held back.",
            ["PageDescStartup"] = "What runs when Windows starts. Disabling an entry never deletes it.",
            ["PageDescProcesses"] = "Memory-heavy applications, read from the live system. Protected Windows processes are never listed.",
            ["PageDescMemory"] = "Working sets, standby cache and per-process memory pressure, read from the live system.",
            ["PageDescRepair"] = "System health first, then one repair at a time, only when you ask for it.",
            ["PageDescApps"] = "Installed programs with their uninstall strings, estimates and leftover folders.",
            ["PageDescForceDelete"] = "Staged paths removed with a forced purge, for files that refuse a normal delete.",
            ["PageDescSettings"] = "Appearance, sound, language, automation and updates. Shared with the desktop app.",
            ["PageDescAbout"] = "Version, license and where to find the source.",
            ["StatReclaimable"] = "Reclaimable",
            ["StatCategories"] = "Categories",
            ["AppDataReport"] = "AppData Report",
            ["SelectionLabel"] = "Selection:",
            ["FilterLabel"] = "Filter:",
            ["SearchTargets"] = "Search targets...",
            ["NoCategoriesTitle"] = "No categories to scan",
            ["NoCategoriesBody"] = "Click Scan Now to discover cleanable temporary locations.",
            ["ActivityLogStream"] = "ACTIVITY LOG STREAM",
            ["CopyLog"] = "Copy",
            ["ClearLog"] = "Clear",
            ["ScheduleAllReboot"] = "Schedule All for Reboot Purge",
            ["LockedFilesHint"] = "You can close the locking application, or schedule files for automatic deletion on next reboot.",
            ["ScheduleRebootDelete"] = "Schedule Reboot Delete",
            ["MemoryOptimizerTitle"] = "Windows Memory Cleaner & Standby Cache",
            ["MemoryOptimizerSubtitle"] = "Kernel-level Standby List, System File Cache & Working Set Optimizer",
            ["TotalPhysicalRam"] = "TOTAL PHYSICAL RAM",
            ["StandbyCacheLabel"] = "RECLAIMABLE STANDBY CACHE",
            ["KernelShieldLabel"] = "KERNEL ISOLATION SHIELD",
            ["DeselectAll"] = "Deselect All",
            ["Flush"] = "Flush",
            ["QuickTrim"] = "Quick Trim Working Sets",
            ["PurgeSelected"] = "Purge Selected Zones",
            ["Close"] = "Close",
            ["AppDataFootprintTitle"] = "AppData Footprint",
            ["AppDataFootprintSubtitle"] = "Where your application data is actually stored",
            ["AppDataFootprintNote"] = "Report only - nothing here is selected, and nothing is deleted. Rows marked 'covered by an existing scope' can be cleaned by the matching Deltempo scope in the Cleaner.",
            ["Remeasure"] = "Re-measure",
            ["UpdateAvailable"] = "Update Available",
            ["UpdateAvailableBody"] = "A new official release of Deltempo is available",
            ["UpdatePreviewNote"] = "This preview build never downloads or replaces itself. The new version opens on GitHub Releases, where you download it yourself.",
            ["Later"] = "Later",
            ["DownloadOnGitHub"] = "Download on GitHub",
            ["Ready"] = "Ready"
        },
        ["ar"] = new()
        {
            ["AppTitle"] = "ديلتيمبو",
            ["AppSubtitle"] = "حارس نظام ويندوز وملفات المستخدم",
            ["AdminLabel"] = "مسؤول",
            ["ReclaimableSpace"] = "المساحة القابلة للاسترداد",
            ["HeroScanSubtext"] = "فحص شامل للملفات المؤقتة وكاش البرامج وكروت الشاشة",
            ["DriveOsLabel"] = "قرص النظام (C:)",
            ["DriveFree"] = "متاح",
            ["DriveOf"] = "متاح من إجمالي",
            ["SelectSafe"] = "تحديد الآمن 100%",
            ["SmartClean"] = "تنظيف ذكي",
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
            ["CloseInspector"] = "إغلاق الفاحص (Esc)",
            ["NavCleaner"] = "منظف",
            ["NavLargeFiles"] = "الملفات الكبيرة",
            ["NavStartup"] = "بدء التشغيل",
            ["NavProcesses"] = "العمليات",
            ["NavMemory"] = "الذاكرة",
            ["NavRepair"] = "إصلاح النظام",
            ["NavApps"] = "التطبيقات",
            ["NavAbout"] = "حول",
            ["NavForceDelete"] = "حذف قسري",
            ["NavSettings"] = "الإعدادات",
            ["HeroScanNow"] = "فحص الآن",
            ["HeroOneClick"] = "تنظيف عميق بنقرة",
            ["HeroGameBoost"] = "تسريع الألعاب",
            ["HeroWinUtil"] = "أداة WinUtil",
            ["DriveLargeFiles"] = "الملفات الكبيرة",
            ["HeroRamLabel"] = "ذاكرة النظام (RAM)",
            ["HeroRamBtn"] = "تنظيف الذاكرة",
            ["HeroBoostRam"] = "تسريع فوري",
            ["FilterAll"] = "جميع النطاقات",
            ["FilterSafe"] = "الآمن فقط",
            ["FilterSystem"] = "النظام والتعريفات",
            ["FilterGaming"] = "الألعاب",
            ["FilterMedia"] = "الوسائط والتطبيقات",
            ["FilterDev"] = "أدوات التطوير",
            ["FilterRulepack"] = "حزم القواعد",
            ["SearchPlaceholder"] = "ابحث في أكثر من 25 نطاقاً...",
            ["JumpToTool"] = "الانتقال إلى أداة",
            ["PageEyebrowCleanup"] = "التنظيف",
            ["PageEyebrowDisk"] = "مساحة القرص",
            ["PageEyebrowBoot"] = "بدء التشغيل والدخول",
            ["PageEyebrowProcesses"] = "العمليات",
            ["PageEyebrowPerformance"] = "الأداء",
            ["PageEyebrowIntegrity"] = "سلامة النظام",
            ["PageEyebrowSoftware"] = "البرامج",
            ["PageEyebrowLastResort"] = "الخيار الأخير",
            ["PageEyebrowPreferences"] = "التفضيلات",
            ["PageEyebrowAbout"] = "حول",
            ["PageDescCleaner"] = "ملفات مؤقتة وكاش ومهملات يستطيع ويندوز إعادة إنشائها. لا يُحذف شيء قبل الضغط على تنظيف.",
            ["PageDescLargeFiles"] = "الملفات الأكبر من الحد الذي تحدده. مواقع النظام المحمية تُعرض لكنها تبقى محجوزة.",
            ["PageDescStartup"] = "ما يعمل عند بدء ويندوز. تعطيل عنصر لا يحذفه أبداً.",
            ["PageDescProcesses"] = "التطبيقات المستهلكة للذاكرة، تُقرأ من النظام المباشر. عمليات ويندوز المحمية لا تُعرض أبداً.",
            ["PageDescMemory"] = "مجموعات العمل والكاش الاحتياطي وضغط الذاكرة لكل عملية، تُقرأ من النظام المباشر.",
            ["PageDescRepair"] = "سلامة النظام أولاً، ثم إصلاح واحد في كل مرة، فقط عندما تطلبه.",
            ["PageDescApps"] = "البرامج المثبتة مع أوامر إلغاء التثبيت والتقديرات والمجلدات المتبقية.",
            ["PageDescForceDelete"] = "مسارات مجهّزة تُحذف بحذف قسري، للملفات التي ترفض الحذف العادي.",
            ["PageDescSettings"] = "المظهر والصوت واللغة والأتمتة والتحديثات. مشتركة مع تطبيق سطح المكتب.",
            ["PageDescAbout"] = "الإصدار والترخيص وأين تجد الشفرة المصدرية.",
            ["StatReclaimable"] = "قابل للاسترداد",
            ["StatCategories"] = "الفئات",
            ["AppDataReport"] = "تقرير AppData",
            ["SelectionLabel"] = "التحديد:",
            ["FilterLabel"] = "التصفية:",
            ["SearchTargets"] = "ابحث في الفئات...",
            ["NoCategoriesTitle"] = "لا توجد فئات للفحص",
            ["NoCategoriesBody"] = "اضغط فحص الآن لاكتشاف المواقع المؤقتة القابلة للتنظيف.",
            ["ActivityLogStream"] = "سجل النشاط المباشر",
            ["CopyLog"] = "نسخ",
            ["ClearLog"] = "مسح",
            ["ScheduleAllReboot"] = "جدولة الكل لإعادة التشغيل",
            ["LockedFilesHint"] = "يمكنك إغلاق التطبيق الذي يحجز الملفات، أو جدولة حذفها تلقائياً عند إعادة التشغيل.",
            ["ScheduleRebootDelete"] = "جدولة الحذف عند إعادة التشغيل",
            ["MemoryOptimizerTitle"] = "منظف ذاكرة ويندوز والكاش الاحتياطي",
            ["MemoryOptimizerSubtitle"] = "محسّن قائمة الانتظار وكاش ملفات النظام ومجموعات العمل على مستوى النواة",
            ["TotalPhysicalRam"] = "إجمالي الذاكرة الفعلية",
            ["StandbyCacheLabel"] = "الكاش الاحتياطي القابل للاسترداد",
            ["KernelShieldLabel"] = "درع عزل النواة",
            ["DeselectAll"] = "إلغاء تحديد الكل",
            ["Flush"] = "تفريغ",
            ["QuickTrim"] = "تقليم سريع لمجموعات العمل",
            ["PurgeSelected"] = "تطهير المناطق المحددة",
            ["Close"] = "إغلاق",
            ["AppDataFootprintTitle"] = "بصمة AppData",
            ["AppDataFootprintSubtitle"] = "أين تُخزَّن بيانات تطبيقاتك فعلياً",
            ["AppDataFootprintNote"] = "تقرير فقط - لا شيء محدد هنا ولا يُحذف شيء. الصفوف المعلّمة بأنها مغطاة بنطاق قائم يمكن تنظيفها بالنطاق المقابل في المنظف.",
            ["Remeasure"] = "إعادة القياس",
            ["UpdateAvailable"] = "تحديث متاح",
            ["UpdateAvailableBody"] = "يتوفر إصدار رسمي جديد من Deltempo",
            ["UpdatePreviewNote"] = "هذا الإصدار التجريبي لا ينزّل نفسه ولا يستبدلها. تفتح النسخة الجديدة على GitHub Releases حيث تنزّلها بنفسك.",
            ["Later"] = "لاحقاً",
            ["DownloadOnGitHub"] = "التنزيل من GitHub",
            ["Ready"] = "جاهز"
        },
        ["es"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Guardián de Windows y Perfiles de Usuario",
            ["AdminLabel"] = "Admin",
            ["ReclaimableSpace"] = "ESPACIO RECUPERABLE",
            ["HeroScanSubtext"] = "Escaneo profundo de AppData, shaders GPU y temporales",
            ["DriveOsLabel"] = "Disco SO",
            ["DriveFree"] = "Libre",
            ["DriveOf"] = "libre de",
            ["SelectSafe"] = "🟢 100% Seguro",
            ["SmartClean"] = "Limpieza Inteligente",
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
            ["CloseInspector"] = "Cerrar Inspector (Esc)",
            ["NavCleaner"] = "Limpiador",
            ["NavLargeFiles"] = "Archivos Grandes",
            ["NavStartup"] = "Inicio",
            ["NavProcesses"] = "Procesos",
            ["NavMemory"] = "Memoria",
            ["NavRepair"] = "Reparación",
            ["NavApps"] = "Aplicaciones",
            ["NavAbout"] = "Acerca de",
            ["NavForceDelete"] = "Borrado Forzado",
            ["NavSettings"] = "Ajustes",
            ["HeroScanNow"] = "Escanear Ahora",
            ["HeroOneClick"] = "Limpieza 1-Clic",
            ["HeroGameBoost"] = "Acelerador Juegos",
            ["HeroWinUtil"] = "CTT WinUtil",
            ["DriveLargeFiles"] = "Archivos Grandes",
            ["HeroRamLabel"] = "Memoria RAM",
            ["HeroRamBtn"] = "Limpiar RAM",
            ["HeroBoostRam"] = "Optimizar",
            ["FilterAll"] = "Todos los Ámbitos",
            ["FilterSafe"] = "Solo Seguro",
            ["FilterSystem"] = "Sistema y Controladores",
            ["FilterGaming"] = "Juegos",
            ["FilterMedia"] = "Multimedia y Apps",
            ["FilterDev"] = "Herramientas Dev",
            ["FilterRulepack"] = "Paquetes de Reglas",
            ["SearchPlaceholder"] = "Buscar más de 25 ámbitos...",
            ["JumpToTool"] = "Ir a una herramienta",
            ["PageEyebrowCleanup"] = "LIMPIEZA",
            ["PageEyebrowDisk"] = "ESPACIO EN DISCO",
            ["PageEyebrowBoot"] = "INICIO Y SESIÓN",
            ["PageEyebrowProcesses"] = "PROCESOS",
            ["PageEyebrowPerformance"] = "RENDIMIENTO",
            ["PageEyebrowIntegrity"] = "INTEGRIDAD",
            ["PageEyebrowSoftware"] = "SOFTWARE",
            ["PageEyebrowLastResort"] = "ÚLTIMO RECURSO",
            ["PageEyebrowPreferences"] = "PREFERENCIAS",
            ["PageEyebrowAbout"] = "ACERCA DE",
            ["PageDescCleaner"] = "Archivos temporales, cachés y basura que Windows puede regenerar. Nada se elimina hasta que pulses Limpiar.",
            ["PageDescLargeFiles"] = "Archivos por encima de tu umbral de tamaño. Las ubicaciones protegidas del sistema se listan pero quedan retenidas.",
            ["PageDescStartup"] = "Lo que se ejecuta al iniciar Windows. Desactivar una entrada nunca la borra.",
            ["PageDescProcesses"] = "Aplicaciones con alto consumo de memoria, leídas del sistema en vivo. Los procesos protegidos de Windows nunca se listan.",
            ["PageDescMemory"] = "Conjuntos de trabajo, caché en espera y presión de memoria por proceso, leídos del sistema en vivo.",
            ["PageDescRepair"] = "Primero la salud del sistema y luego una reparación a la vez, solo cuando la pidas.",
            ["PageDescApps"] = "Programas instalados con sus cadenas de desinstalación, estimaciones y carpetas residuales.",
            ["PageDescForceDelete"] = "Rutas preparadas que se eliminan con purga forzada, para archivos que rechazan un borrado normal.",
            ["PageDescSettings"] = "Apariencia, sonido, idioma, automatización y actualizaciones. Compartido con la app de escritorio.",
            ["PageDescAbout"] = "Versión, licencia y dónde encontrar el código fuente.",
            ["StatReclaimable"] = "Recuperable",
            ["StatCategories"] = "Categorías",
            ["AppDataReport"] = "Informe AppData",
            ["SelectionLabel"] = "Selección:",
            ["FilterLabel"] = "Filtro:",
            ["SearchTargets"] = "Buscar categorías...",
            ["NoCategoriesTitle"] = "No hay categorías para escanear",
            ["NoCategoriesBody"] = "Pulsa Escanear Ahora para descubrir ubicaciones temporales limpiables.",
            ["ActivityLogStream"] = "FLUJO DE ACTIVIDAD",
            ["CopyLog"] = "Copiar",
            ["ClearLog"] = "Vaciar",
            ["ScheduleAllReboot"] = "Programar todo para reinicio",
            ["LockedFilesHint"] = "Puedes cerrar la aplicación que bloquea los archivos o programarlos para eliminación automática al reiniciar.",
            ["ScheduleRebootDelete"] = "Eliminar al reiniciar",
            ["MemoryOptimizerTitle"] = "Limpiador de memoria y caché en espera de Windows",
            ["MemoryOptimizerSubtitle"] = "Optimizador de lista en espera, caché de archivos del sistema y conjuntos de trabajo",
            ["TotalPhysicalRam"] = "RAM FÍSICA TOTAL",
            ["StandbyCacheLabel"] = "CACHÉ EN ESPERA RECUPERABLE",
            ["KernelShieldLabel"] = "ESCUDO DE AISLAMIENTO DEL KERNEL",
            ["DeselectAll"] = "Deseleccionar todo",
            ["Flush"] = "Vaciar",
            ["QuickTrim"] = "Ajuste rápido de conjuntos",
            ["PurgeSelected"] = "Purgar zonas seleccionadas",
            ["Close"] = "Cerrar",
            ["AppDataFootprintTitle"] = "Huella de AppData",
            ["AppDataFootprintSubtitle"] = "Dónde se guardan realmente los datos de tus aplicaciones",
            ["AppDataFootprintNote"] = "Solo informe: nada aquí está seleccionado y nada se elimina. Las filas marcadas como «cubiertas por un ámbito existente» se pueden limpiar con el ámbito correspondiente en el Limpiador.",
            ["Remeasure"] = "Medir de nuevo",
            ["UpdateAvailable"] = "Actualización disponible",
            ["UpdateAvailableBody"] = "Hay una nueva versión oficial de Deltempo disponible",
            ["UpdatePreviewNote"] = "Esta compilación de vista previa nunca se descarga ni se reemplaza a sí misma. La nueva versión se abre en GitHub Releases, donde la descargas tú.",
            ["Later"] = "Más tarde",
            ["DownloadOnGitHub"] = "Descargar en GitHub",
            ["Ready"] = "Listo"
        },
        ["fr"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Gardien de Windows et Profil Utilisateur",
            ["AdminLabel"] = "Admin",
            ["ReclaimableSpace"] = "ESPACE RÉCUPÉRABLE",
            ["HeroScanSubtext"] = "Scan profond d'AppData, shaders GPU et fichiers temporaires",
            ["DriveOsLabel"] = "Disque Système",
            ["DriveFree"] = "Libre",
            ["DriveOf"] = "libre sur",
            ["SelectSafe"] = "🟢 100% Sécurisé",
            ["SmartClean"] = "Nettoyage Intelligent",
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
            ["CloseInspector"] = "Fermer (Esc)",
            ["NavCleaner"] = "Nettoyeur",
            ["NavLargeFiles"] = "Fichiers Volumineux",
            ["NavStartup"] = "Démarrage",
            ["NavProcesses"] = "Processus",
            ["NavMemory"] = "Mémoire",
            ["NavRepair"] = "Réparation",
            ["NavApps"] = "Applications",
            ["NavAbout"] = "À propos",
            ["NavForceDelete"] = "Suppression Forcée",
            ["NavSettings"] = "Paramètres",
            ["HeroScanNow"] = "Scanner",
            ["HeroOneClick"] = "Nettoyage 1-Clic",
            ["HeroGameBoost"] = "Mode Jeu",
            ["HeroWinUtil"] = "CTT WinUtil",
            ["DriveLargeFiles"] = "Fichiers Volumineux",
            ["HeroRamLabel"] = "Mémoire RAM",
            ["HeroRamBtn"] = "Nettoyeur RAM",
            ["HeroBoostRam"] = "Optimiser",
            ["FilterAll"] = "Tous les Éléments",
            ["FilterSafe"] = "Sécurisé Uniquement",
            ["FilterSystem"] = "Système & Pilotes",
            ["FilterGaming"] = "Jeux Vidéo",
            ["FilterMedia"] = "Médias & Apps",
            ["FilterDev"] = "Outils Développeur",
            ["FilterRulepack"] = "Packs de Règles",
            ["SearchPlaceholder"] = "Rechercher parmi 25+ éléments...",
            ["JumpToTool"] = "Aller à un outil",
            ["PageEyebrowCleanup"] = "NETTOYAGE",
            ["PageEyebrowDisk"] = "ESPACE DISQUE",
            ["PageEyebrowBoot"] = "DÉMARRAGE ET CONNEXION",
            ["PageEyebrowProcesses"] = "PROCESSUS",
            ["PageEyebrowPerformance"] = "PERFORMANCES",
            ["PageEyebrowIntegrity"] = "INTÉGRITÉ",
            ["PageEyebrowSoftware"] = "LOGICIELS",
            ["PageEyebrowLastResort"] = "DERNIER RECOURS",
            ["PageEyebrowPreferences"] = "PRÉFÉRENCES",
            ["PageEyebrowAbout"] = "À PROPOS",
            ["PageDescCleaner"] = "Fichiers temporaires, caches et déchets que Windows peut régénérer. Rien n'est supprimé avant que vous ne cliquiez sur Nettoyer.",
            ["PageDescLargeFiles"] = "Fichiers dépassant votre seuil de taille. Les emplacements système protégés sont listés mais retenus.",
            ["PageDescStartup"] = "Ce qui s'exécute au démarrage de Windows. Désactiver une entrée ne la supprime jamais.",
            ["PageDescProcesses"] = "Applications gourmandes en mémoire, lues sur le système en direct. Les processus Windows protégés ne sont jamais listés.",
            ["PageDescMemory"] = "Ensembles de travail, cache en veille et pression mémoire par processus, lus sur le système en direct.",
            ["PageDescRepair"] = "La santé du système d'abord, puis une réparation à la fois, uniquement lorsque vous la demandez.",
            ["PageDescApps"] = "Programmes installés avec leurs chaînes de désinstallation, estimations et dossiers résiduels.",
            ["PageDescForceDelete"] = "Chemins préparés supprimés par purge forcée, pour les fichiers qui refusent une suppression normale.",
            ["PageDescSettings"] = "Apparence, son, langue, automatisation et mises à jour. Partagé avec l'application de bureau.",
            ["PageDescAbout"] = "Version, licence et où trouver le code source.",
            ["StatReclaimable"] = "Récupérable",
            ["StatCategories"] = "Catégories",
            ["AppDataReport"] = "Rapport AppData",
            ["SelectionLabel"] = "Sélection :",
            ["FilterLabel"] = "Filtre :",
            ["SearchTargets"] = "Rechercher des catégories...",
            ["NoCategoriesTitle"] = "Aucune catégorie à analyser",
            ["NoCategoriesBody"] = "Cliquez sur Scanner pour découvrir les emplacements temporaires nettoyables.",
            ["ActivityLogStream"] = "FLUX D'ACTIVITÉ",
            ["CopyLog"] = "Copier",
            ["ClearLog"] = "Vider",
            ["ScheduleAllReboot"] = "Tout planifier au redémarrage",
            ["LockedFilesHint"] = "Vous pouvez fermer l'application qui verrouille les fichiers, ou les planifier pour une suppression automatique au prochain redémarrage.",
            ["ScheduleRebootDelete"] = "Supprimer au redémarrage",
            ["MemoryOptimizerTitle"] = "Nettoyeur de mémoire et de cache en veille Windows",
            ["MemoryOptimizerSubtitle"] = "Optimiseur de liste en veille, cache de fichiers système et ensembles de travail",
            ["TotalPhysicalRam"] = "RAM PHYSIQUE TOTALE",
            ["StandbyCacheLabel"] = "CACHE EN VEILLE RÉCUPÉRABLE",
            ["KernelShieldLabel"] = "BOUCLIER D'ISOLATION DU NOYAU",
            ["DeselectAll"] = "Tout désélectionner",
            ["Flush"] = "Vider",
            ["QuickTrim"] = "Ajustement rapide des ensembles",
            ["PurgeSelected"] = "Purger les zones sélectionnées",
            ["Close"] = "Fermer",
            ["AppDataFootprintTitle"] = "Empreinte AppData",
            ["AppDataFootprintSubtitle"] = "Où vos données d'application sont réellement stockées",
            ["AppDataFootprintNote"] = "Rapport uniquement : rien ici n'est sélectionné et rien n'est supprimé. Les lignes marquées « couvertes par une portée existante » peuvent être nettoyées par la portée correspondante dans le Nettoyeur.",
            ["Remeasure"] = "Remesurer",
            ["UpdateAvailable"] = "Mise à jour disponible",
            ["UpdateAvailableBody"] = "Une nouvelle version officielle de Deltempo est disponible",
            ["UpdatePreviewNote"] = "Cette version d'aperçu ne se télécharge ni ne se remplace jamais elle-même. La nouvelle version s'ouvre sur GitHub Releases, où vous la téléchargez vous-même.",
            ["Later"] = "Plus tard",
            ["DownloadOnGitHub"] = "Télécharger sur GitHub",
            ["Ready"] = "Prêt"
        },
        ["de"] = new()
        {
            ["AppTitle"] = "Deltempo",
            ["AppSubtitle"] = "Windows- & Benutzerprofil-Wächter",
            ["AdminLabel"] = "Admin",
            ["ReclaimableSpace"] = "WIEDERHERSTELLBARER SPEICHER",
            ["HeroScanSubtext"] = "Tiefenscan von Benutzerprofil, GPU-Shadern, AppData & Temp-Dateien",
            ["DriveOsLabel"] = "Systemlaufwerk",
            ["DriveFree"] = "Frei",
            ["DriveOf"] = "frei von",
            ["SelectSafe"] = "🟢 100% Sicher",
            ["SmartClean"] = "Smart-Bereinigung",
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
            ["CloseInspector"] = "Schließen (Esc)",
            ["NavCleaner"] = "Bereiniger",
            ["NavLargeFiles"] = "Große Dateien",
            ["NavStartup"] = "Autostart",
            ["NavProcesses"] = "Prozesse",
            ["NavMemory"] = "Speicher",
            ["NavRepair"] = "Reparatur",
            ["NavApps"] = "Programme",
            ["NavAbout"] = "Über",
            ["NavForceDelete"] = "Erzwungenes Löschen",
            ["NavSettings"] = "Einstellungen",
            ["HeroScanNow"] = "Jetzt Scannen",
            ["HeroOneClick"] = "1-Klick Tiefenreinigung",
            ["HeroGameBoost"] = "Game-Boost",
            ["HeroWinUtil"] = "CTT WinUtil",
            ["DriveLargeFiles"] = "Große Dateien",
            ["HeroRamLabel"] = "Arbeitsspeicher (RAM)",
            ["HeroRamBtn"] = "RAM Bereinigen",
            ["HeroBoostRam"] = "Schnell-Boost",
            ["FilterAll"] = "Alle Bereiche",
            ["FilterSafe"] = "Nur Sicher",
            ["FilterSystem"] = "System & Treiber",
            ["FilterGaming"] = "Gaming-Caches",
            ["FilterMedia"] = "Medien & Apps",
            ["FilterDev"] = "Entwicklertools",
            ["FilterRulepack"] = "Regelpakete",
            ["SearchPlaceholder"] = "25+ Bereiche durchsuchen...",
            ["JumpToTool"] = "Zu einem Werkzeug springen",
            ["PageEyebrowCleanup"] = "BEREINIGUNG",
            ["PageEyebrowDisk"] = "FESTPLATTENPLATZ",
            ["PageEyebrowBoot"] = "START UND ANMELDUNG",
            ["PageEyebrowProcesses"] = "PROZESSE",
            ["PageEyebrowPerformance"] = "LEISTUNG",
            ["PageEyebrowIntegrity"] = "INTEGRITÄT",
            ["PageEyebrowSoftware"] = "SOFTWARE",
            ["PageEyebrowLastResort"] = "LETZTE OPTION",
            ["PageEyebrowPreferences"] = "EINSTELLUNGEN",
            ["PageEyebrowAbout"] = "ÜBER",
            ["PageDescCleaner"] = "Temporäre Dateien, Caches und Müll, die Windows neu aufbauen kann. Nichts wird entfernt, bevor Sie auf Bereinigen klicken.",
            ["PageDescLargeFiles"] = "Dateien über Ihrer Größenschwelle. Geschützte Systemorte werden aufgelistet, aber zurückgehalten.",
            ["PageDescStartup"] = "Was beim Start von Windows läuft. Das Deaktivieren eines Eintrags löscht ihn nie.",
            ["PageDescProcesses"] = "Speicherintensive Anwendungen, live aus dem System gelesen. Geschützte Windows-Prozesse werden nie aufgelistet.",
            ["PageDescMemory"] = "Arbeitssätze, Standby-Cache und Speicherdruck pro Prozess, live aus dem System gelesen.",
            ["PageDescRepair"] = "Zuerst die Systemintegrität, dann eine Reparatur nach der anderen, nur wenn Sie sie anfordern.",
            ["PageDescApps"] = "Installierte Programme mit Deinstallationsbefehlen, Schätzungen und Restordnern.",
            ["PageDescForceDelete"] = "Vorbereitete Pfade, die mit erzwungener Bereinigung entfernt werden, für Dateien, die ein normales Löschen verweigern.",
            ["PageDescSettings"] = "Darstellung, Ton, Sprache, Automatisierung und Updates. Mit der Desktop-App geteilt.",
            ["PageDescAbout"] = "Version, Lizenz und wo der Quellcode zu finden ist.",
            ["StatReclaimable"] = "Rückgewinnbar",
            ["StatCategories"] = "Kategorien",
            ["AppDataReport"] = "AppData-Bericht",
            ["SelectionLabel"] = "Auswahl:",
            ["FilterLabel"] = "Filter:",
            ["SearchTargets"] = "Kategorien durchsuchen...",
            ["NoCategoriesTitle"] = "Keine Kategorien zum Scannen",
            ["NoCategoriesBody"] = "Klicken Sie auf Jetzt scannen, um bereinigbare temporäre Orte zu finden.",
            ["ActivityLogStream"] = "AKTIVITÄTSPROTOKOLL-STREAM",
            ["CopyLog"] = "Kopieren",
            ["ClearLog"] = "Leeren",
            ["ScheduleAllReboot"] = "Alle für Neustart planen",
            ["LockedFilesHint"] = "Sie können die Anwendung schließen, die die Dateien sperrt, oder sie für das automatische Löschen beim nächsten Neustart planen.",
            ["ScheduleRebootDelete"] = "Beim Neustart löschen",
            ["MemoryOptimizerTitle"] = "Windows Speicher- & Standby-Cache-Reiniger",
            ["MemoryOptimizerSubtitle"] = "Optimierer für Standbyliste, Systemdateicache & Arbeitssätze",
            ["TotalPhysicalRam"] = "GESAMTER PHYSIKALISCHER RAM",
            ["StandbyCacheLabel"] = "RÜCKGEWINNBARER STANDBY-CACHE",
            ["KernelShieldLabel"] = "KERNEL-ISOLATIONSSCHILD",
            ["DeselectAll"] = "Auswahl aufheben",
            ["Flush"] = "Leeren",
            ["QuickTrim"] = "Arbeitssätze schnell trimmen",
            ["PurgeSelected"] = "Ausgewählte Zonen bereinigen",
            ["Close"] = "Schließen",
            ["AppDataFootprintTitle"] = "AppData-Fußabdruck",
            ["AppDataFootprintSubtitle"] = "Wo Ihre Anwendungsdaten tatsächlich liegen",
            ["AppDataFootprintNote"] = "Nur Bericht: Hier ist nichts ausgewählt und nichts wird gelöscht. Zeilen, die als von einem vorhandenen Bereich abgedeckt markiert sind, kann der passende Bereich im Bereiniger säubern.",
            ["Remeasure"] = "Neu messen",
            ["UpdateAvailable"] = "Update verfügbar",
            ["UpdateAvailableBody"] = "Eine neue offizielle Version von Deltempo ist verfügbar",
            ["UpdatePreviewNote"] = "Dieser Vorschau-Build lädt sich niemals selbst herunter oder ersetzt sich. Die neue Version öffnet sich auf GitHub Releases, wo Sie sie selbst herunterladen.",
            ["Later"] = "Später",
            ["DownloadOnGitHub"] = "Auf GitHub herunterladen",
            ["Ready"] = "Bereit"
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

    /// <summary>
    /// True when the English dictionary defines <paramref name="key"/>. <see cref="Get"/>
    /// cannot answer this: it returns the key itself for an unknown key, which is
    /// indistinguishable from an English value that happens to equal its key ("Cancel",
    /// "Close", "Later"). Callers that need to prove a binding resolves use this.
    /// </summary>
    public static bool ContainsKey(string key) => Translations["en"].ContainsKey(key);

    /// <summary>
    /// The raw dictionaries, for tests only: the suite proves every English key is
    /// actually translated in the other four languages, which <see cref="Get"/> can
    /// never show because it silently falls back to English. The UI never enumerates
    /// translations - it asks for one key at a time.
    /// </summary>
    internal static IReadOnlyDictionary<string, Dictionary<string, string>> TranslationsByLanguage => Translations;

    public static void LocalizeTarget(TargetFolderInfo target)
    {
        switch (target.Id)
        {
            case "UserTemp":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "ملفات المستخدم المؤقتة (%TEMP%)",
                    "es" => "Archivos Temporales de Usuario",
                    "fr" => "Fichiers Temporaires Utilisateur",
                    "de" => "Benutzer-Temp & Zwischenspeicher",
                    _ => "User Temp & Scratchpad"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "كاش المستخدم",
                    "es" => "Caché Usuario",
                    "fr" => "Cache Utilisateur",
                    "de" => "Benutzer-Cache",
                    _ => "User Cache"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "كاش البرامج، استخراج ملفات التثبيت المؤقتة والتحميلات (%TEMP%)",
                    "es" => "Caché de aplicaciones, extracciones de instalación (%TEMP%)",
                    "fr" => "Cache d'applications et extractions temporaires (%TEMP%)",
                    "de" => "Anwendungs-Cache und temporäre Setup-Extrakte (%TEMP%)",
                    _ => "Application cache, temporary setup extracts, downloads (%TEMP%)"
                };
                break;

            case "WinTemp":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "ملفات نظام ويندوز المؤقتة",
                    "es" => "Temporales del Sistema Windows",
                    "fr" => "Fichiers Temporaires Système",
                    "de" => "Windows System-Temp",
                    _ => "Windows System Temp"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "النظام وكارت الشاشة",
                    "es" => "Sistema y GPU",
                    "fr" => "Système & GPU",
                    "de" => "System & GPU",
                    _ => "System & GPU"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "سجلات تشخيص النظام ومسودات تحديثات ويندوز (C:\\Windows\\Temp)",
                    "es" => "Registros de diagnóstico y temporales de actualización",
                    "fr" => "Traces de diagnostic et fichiers de mise à jour système",
                    "de" => "Betriebssystem-Diagnose und Update-Zwischenspeicher",
                    _ => "OS diagnostic traces, system update scratchpad (C:\\Windows\\Temp)"
                };
                break;

            case "WinPrefetch":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش التشغيل المسبق (Prefetch)",
                    "es" => "Caché Prefetch de Windows",
                    "fr" => "Cache Windows Prefetch",
                    "de" => "Windows Prefetch-Cache",
                    _ => "Windows Prefetch Cache"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "آثار التشغيل القديمة وترويسات بدء التشغيل المؤقتة",
                    "es" => "Rastros de ejecución antiguos y cabeceras de inicio",
                    "fr" => "Traces d'exécution obsolètes et en-têtes de démarrage",
                    "de" => "Veraltete Ausführungsspuren und Start-Header",
                    _ => "Stale execution traces & cached startup headers (C:\\Windows\\Prefetch)"
                };
                break;

            case "WinUpdateCache":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش تنزيلات تحديثات ويندوز",
                    "es" => "Caché de Windows Update",
                    "fr" => "Téléchargements Windows Update",
                    "de" => "Windows Update Download-Cache",
                    _ => "Windows Update Cache"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "حزم التثبيت التي تم تنزيلها وكاش التسليم (SoftwareDistribution\\Download)",
                    "es" => "Instaladores descargados y caché de distribución",
                    "fr" => "Packages d'installation téléchargés et cache de distribution",
                    "de" => "Heruntergeladene Update-Installer und Bereitstellungs-Cache",
                    _ => "Downloaded update installers & delivery cache (SoftwareDistribution\\Download)"
                };
                break;

            case "WinUpgradeLeftovers":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "بقايا ترقيات وتثبيت ويندوز السابقة",
                    "es" => "Restos de Actualizaciones de Windows",
                    "fr" => "Résidus de Mises à Niveau Windows",
                    "de" => "Windows Upgrade- & Setup-Rückstände",
                    _ => "Windows Upgrade & Setup Leftovers"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "النظام والتحديثات",
                    "es" => "Sistema y SO",
                    "fr" => "Système & OS",
                    "de" => "System & OS",
                    _ => "System & OS"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "بقايا ترقيات النظام السابقة ($WINDOWS.~BT و $WINDOWS.~WS و ESD ومسودات الإعداد)",
                    "es" => "Restos de instalaciones anteriores, $WINDOWS.~BT, $WINDOWS.~WS y ESD",
                    "fr" => "Restes d'anciennes installations d'OS, $WINDOWS.~BT, $WINDOWS.~WS, ESD",
                    "de" => "Alte OS-Installationsreste, $WINDOWS.~BT, $WINDOWS.~WS, ESD und Setup-Dateien",
                    _ => "Old OS installation leftovers, $WINDOWS.~BT, $WINDOWS.~WS, ESD, and Setup scratchpads"
                };
                break;

            case "WinDeliveryOpt":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش تسليم التحديثات عبر الشبكة (WUDO)",
                    "es" => "Optimización de Entrega de Windows",
                    "fr" => "Optimisation de Livraison Windows",
                    "de" => "Windows Übermittlungsoptimierung",
                    _ => "Windows Delivery Optimization"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "النظام والتحديثات",
                    "es" => "Sistema y SO",
                    "fr" => "Système & OS",
                    "de" => "System & OS",
                    _ => "System & OS"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "أجزاء كاش التحديثات الموزعة بين الأجهزة عبر الشبكة (DeliveryOptimization)",
                    "es" => "Fragmentos de entrega P2P de actualizaciones de Windows",
                    "fr" => "Morceaux de mise à jour P2P et cache d'optimisation",
                    "de" => "P2P-Update-Bereitstellungsfragmente und Hintergrund-Cache",
                    _ => "P2P Windows update delivery chunks and background bits cache (DeliveryOptimization)"
                };
                break;

            case "WinComponentCaches":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش مكونات وخطوط ويندوز",
                    "es" => "Cachés de Componentes y Fuentes de Windows",
                    "fr" => "Caches Composants & Polices Windows",
                    "de" => "Windows Komponenten- & Schriftarten-Caches",
                    _ => "Windows Component & Font Caches"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "النظام والتحديثات",
                    "es" => "Sistema y SO",
                    "fr" => "Système & OS",
                    "de" => "System & OS",
                    _ => "System & OS"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "كاش خطوط ويندوز FontCache ومجلد البرامج المنزلة ومسودات WinSxS و DISM و BranchCache",
                    "es" => "FontCache de Windows, Archivos de programa descargados, temporal WinSxS y DISM",
                    "fr" => "FontCache Windows, fichiers téléchargés, WinSxS temp, DISM et BranchCache",
                    "de" => "Windows FontCache, Downloaded Program Files, WinSxS-Temp, DISM & BranchCache",
                    _ => "Windows FontCache, Downloaded Program Files, WinSxS temp, DISM scratch & BranchCache"
                };
                break;

            case "DeviceDriverPackages":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "حزم تعاريف الأجهزة وتحديثات GPU",
                    "es" => "Paquetes de Controladores y GPU",
                    "fr" => "Pilotes Périphériques & Mises à Jour GPU",
                    "de" => "Gerätetreiber-Pakete & GPU-Updates",
                    _ => "Device Driver Packages & GPU Updates"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "النظام والتعريفات",
                    "es" => "Sistema y Controladores",
                    "fr" => "Système & Pilotes",
                    "de" => "System & Treiber",
                    _ => "System & Drivers"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "حزم تحديثات NVIDIA App و AMD و Intel وكاش DriverStore المؤقت",
                    "es" => "Paquetes OTA de NVIDIA App, instaladores AMD e Intel, temp DriverStore",
                    "fr" => "Packages OTA NVIDIA App, installateurs AMD/Intel, DriverStore temp",
                    "de" => "NVIDIA App/GeForce OTA-Treiberpakete, AMD- & Intel-Caches, DriverStore temp",
                    _ => "NVIDIA App/GeForce OTA driver packages, AMD & Intel installer caches, DriverStore temp"
                };
                break;

            case "DefenderAntivirus":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "سجلات وفحوصات حماية Microsoft Defender",
                    "es" => "Soporte y Análisis de Microsoft Defender",
                    "fr" => "Support & Analyses Microsoft Defender",
                    "de" => "Microsoft Defender Support & Scans",
                    _ => "Microsoft Defender Support & Scans"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "الأمان والسجلات",
                    "es" => "Seguridad y Registros",
                    "fr" => "Sécurité & Journaux",
                    "de" => "Sicherheit & Protokolle",
                    _ => "Security & Logs"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "سجلات تشخيص Defender (MPLog)، النسخ الاحتياطية لتحديثات التواقيع وسجل الفحص",
                    "es" => "Registros MPLog de Defender, copias de seguridad de definiciones y caché de análisis",
                    "fr" => "Journaux de support Defender (MPLog), sauvegardes de définitions et historique d'analyse",
                    "de" => "Defender-Diagnoseprotokolle (MPLog), Definitions-Backups & Scan-Verlauf",
                    _ => "Defender support diagnostic logs (MPLog), definition update backups & scan history cache"
                };
                break;

            case "WinSystemLogs":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "سجلات تشخيص النظام",
                    "es" => "Registros de Diagnóstico de Windows",
                    "fr" => "Journaux de Diagnostic Système",
                    "de" => "Windows System-Diagnoseprotokolle",
                    _ => "Windows System Diagnostic Logs"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "التشخيص والسجلات",
                    "es" => "Diagnóstico",
                    "fr" => "Diagnostic",
                    "de" => "Diagnose",
                    _ => "Diagnostics"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "سجلات CBS و DISM و Panther و SetupAPI و LogFiles وسجلات التتبع",
                    "es" => "Registros CBS, DISM, Panther, SetupAPI, LogFiles y seguimiento",
                    "fr" => "Journaux CBS, DISM, Panther, SetupAPI, LogFiles et traces",
                    "de" => "CBS, DISM, Panther, SetupAPI, LogFiles (WMI/HTTPERR) und Ablaufverfolgungen",
                    _ => "CBS, DISM, Panther, SetupAPI, LogFiles (WMI/HTTPERR), and tracing logs"
                };
                break;

            case "SystemDumps":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "تفريغات انهيار النظام وتقارير الكيرنل",
                    "es" => "Volcados de Bloqueo y Minivolcados BSOD",
                    "fr" => "Vidages de Mémoire et Rapports Noyau",
                    "de" => "BSOD-Minidumps & Kernel-Berichte",
                    _ => "BSOD Minidumps & Kernel Reports"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "التشخيص والسجلات",
                    "es" => "Diagnóstico",
                    "fr" => "Diagnostic",
                    "de" => "Diagnose",
                    _ => "Diagnostics"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "تفريغات أخطاء الشاشة الزرقاء (*.dmp) و MEMORY.DMP وتقارير LiveKernelReports",
                    "es" => "Minivolcados BSOD (*.dmp), MEMORY.DMP y LiveKernelReports",
                    "fr" => "Minividages BSOD (*.dmp), MEMORY.DMP et LiveKernelReports",
                    "de" => "Windows Crash-Minidumps (*.dmp), MEMORY.DMP und LiveKernelReports",
                    _ => "Windows crash minidumps (*.dmp), MEMORY.DMP, and LiveKernelReports"
                };
                break;

            case "TemporaryInternetFiles":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "ملفات الإنترنت المؤقتة و WebCache",
                    "es" => "Archivos Temporales de Internet y WebCache",
                    "fr" => "Fichiers Internet Temporaires & WebCache",
                    "de" => "Temporäre Internetdateien & WebCache",
                    _ => "Temporary Internet Files & WebCache"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "كاش الإنترنت",
                    "es" => "Caché de Internet",
                    "fr" => "Cache Internet",
                    "de" => "Internet-Cache",
                    _ => "Internet Cache"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "كاش INetCache و WebCache ومحتوى شهادات CryptnetUrlCache",
                    "es" => "INetCache de Windows, WebCache y certificados CryptnetUrlCache",
                    "fr" => "INetCache Windows, WebCache et contenu de certificat CryptnetUrlCache",
                    "de" => "Windows INetCache, WebCache und CryptnetUrlCache-Zertifikatinhalte",
                    _ => "Windows INetCache, WebCache, and CryptnetUrlCache certificate content"
                };
                break;

            case "GpuShaderCaches":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش كروت الشاشة و DirectX",
                    "es" => "Shaders de GPU y DirectX",
                    "fr" => "Shaders GPU et DirectX",
                    "de" => "DirectX & GPU Shader-Caches",
                    _ => "DirectX & GPU Shader Caches"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "كارت الشاشة",
                    "es" => "Sistema y GPU",
                    "fr" => "Système & GPU",
                    "de" => "System & GPU",
                    _ => "System & GPU"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "شيدرز الرسوميات المترجمة لكروت NVIDIA و AMD و Intel و D3DSCache",
                    "es" => "Shaders gráficos compilados de NVIDIA, AMD e Intel",
                    "fr" => "Shaders graphiques compilés NVIDIA, AMD et Intel",
                    "de" => "Kompilierte Grafik-Shader von NVIDIA, AMD, D3DSCache & Intel",
                    _ => "Compiled graphics shaders from NVIDIA, AMD, D3DSCache & Intel"
                };
                break;

            case "GamingLaunchers":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش منصات الألعاب (Steam / Epic / Battle.net / Riot)",
                    "es" => "Lanzadores de Juegos y Shaders",
                    "fr" => "Lanceurs de Jeux et Shaders",
                    "de" => "Gaming-Launcher & Shader-Caches",
                    _ => "Game Launchers & Shaders"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "الألعاب والوسائط",
                    "es" => "Juegos y Medios",
                    "fr" => "Jeux & Médias",
                    "de" => "Gaming & Medien",
                    _ => "Gaming & Media"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "تنزيلات Steam، كاش Epic Games و Battle.net و EA App وسجلات Riot Games و Roblox",
                    "es" => "Descargas temporales de Steam, caché de Epic Games, Battle.net, Riot y Roblox",
                    "fr" => "Téléchargements Steam, caches web Epic Games, Battle.net, Riot et Roblox",
                    "de" => "Steam Downloads & Shader, Epic Games Webcache, Battle.net, EA App, Riot Games, Roblox",
                    _ => "Steam downloads & shaders, Epic Games webcache, Battle.net, EA App, Riot Games, Roblox"
                };
                break;

            case "MediaCreatorCaches":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش برامج المونتاج والتصميم (Adobe / CapCut / DaVinci)",
                    "es" => "Cachés de Renderizado y Creadores",
                    "fr" => "Caches de Rendu et Créateurs",
                    "de" => "Medien- & Render-Caches",
                    _ => "Media & Creator Render Caches"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "صناع المحتوى والميديا",
                    "es" => "Creadores y Medios",
                    "fr" => "Créateurs & Médias",
                    "de" => "Kreativ & Medien",
                    _ => "Creator & Media"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "ملفات Media Cache في Adobe و CapCut و DaVinci Resolve وسجلات OBS ومؤقتات Blender",
                    "es" => "Media Cache de Adobe, CapCut, proxy de DaVinci, logs de OBS y Blender temp",
                    "fr" => "Media Cache Adobe, CapCut, proxies DaVinci, logs OBS et Blender temp",
                    "de" => "Adobe Premiere/Photoshop Scratch, CapCut Cache, DaVinci Proxy, OBS Logs, Blender Temp",
                    _ => "Adobe Premiere/After Effects/Photoshop scratch, CapCut cache, DaVinci proxy, OBS logs, Blender temp"
                };
                break;

            case "AppCacheSweeper":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش برامج سطح المكتب (Discord/WhatsApp/Teams)",
                    "es" => "Caché de Apps de Escritorio",
                    "fr" => "Cache des Applications Desktop",
                    "de" => "Desktop-App Cache-Bereiniger",
                    _ => "Desktop Apps Cache Sweeper"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "كاش GPU و Code Cache المؤقت في Discord و Spotify و Slack و VS Code و WhatsApp و Notion",
                    "es" => "Caché GPU y código de Discord, Spotify, Slack, VS Code, WhatsApp y Notion",
                    "fr" => "Cache GPU et code de Discord, Spotify, Slack, VS Code, WhatsApp et Notion",
                    "de" => "Flüchtiger GPU- & Code-Cache in Discord, Spotify, Slack, VS Code, Teams, WhatsApp, Notion",
                    _ => "Disposable GPU & Code Cache in Discord, Spotify, Slack, VS Code, Cursor, Teams, WhatsApp, Notion"
                };
                break;

            case "WinStoreAppCaches":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش تطبيقات متجر ويندوز و UWP",
                    "es" => "Caché de Apps de la Tienda y UWP",
                    "fr" => "Caches Applications Windows Store & UWP",
                    "de" => "Windows Store-Apps & UWP Caches",
                    _ => "Windows Store Apps & UWP Caches"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "تطبيقات المتجر",
                    "es" => "Apps de Tienda",
                    "fr" => "Applications Store",
                    "de" => "Store-Apps",
                    _ => "Store Apps"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "ملفات LocalCache و INetCache المؤقتة لحزم تطبيقات متجر ويندوز (Teams الجديد و Xbox وغيرها)",
                    "es" => "LocalCache e INetCache temporales de paquetes Windows Store (Teams, Xbox, etc.)",
                    "fr" => "LocalCache et INetCache temporaires des applications Store (New Teams, Xbox, etc.)",
                    "de" => "Temporärer LocalCache & INetCache über Windows Store-Pakete (New Teams, Xbox, etc.)",
                    _ => "Temporary LocalCache & INetCache across Windows Store packages (New Teams, Xbox, etc.)"
                };
                break;

            case "MessagingAppCaches":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش برامج المحادثة والمراسلة",
                    "es" => "Caché de Apps de Mensajería y Chat",
                    "fr" => "Cache des Applications de Messagerie",
                    "de" => "Messenger & Chat-Apps Cache",
                    _ => "Messaging & Social Apps Caches"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "المحادثة والتواصل",
                    "es" => "Comunicación",
                    "fr" => "Communication",
                    "de" => "Kommunikation",
                    _ => "Communication"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "كاش الصور والفيديو و GPU المؤقتة لتطبيقات WhatsApp و Telegram و Discord و Slack و Teams و Signal (مع حماية تامة لجلسات الدخول)",
                    "es" => "Caché GPU y multimedia de WhatsApp, Telegram, Discord, Slack, Teams, Signal (sesiones e inicios de sesión preservados)",
                    "fr" => "Caches GPU et médias de WhatsApp, Telegram, Discord, Slack, Teams, Signal (sessions et connexions préservées)",
                    "de" => "Sicherer GPU- & Medien-Cache für WhatsApp, Telegram, Discord, Slack, Teams, Signal (Anmeldesitzungen geschützt)",
                    _ => "Safe media & GPU caches for WhatsApp, Telegram, Discord, Slack, Teams, Signal, Skype, Viber, Zoom (logins strictly preserved)"
                };
                break;

            case "BrowserCaches":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش متصفحات الإنترنت متعددة الحسابات",
                    "es" => "Caché de Navegadores Web Multiprofil",
                    "fr" => "Cache des Navigateurs Web Multi-Profils",
                    "de" => "Webbrowser-Caches (Multi-Profil)",
                    _ => "Web Browsers Cache Pool"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "فحص عميق وشامل لكافة حسابات ومتصفحات Chrome و Edge و Brave و Opera و Firefox و Arc و Vivaldi",
                    "es" => "Caché web y shaders de todos los perfiles de Chrome, Edge, Brave, Opera, Firefox, Arc, Vivaldi",
                    "fr" => "Cache web et shaders multi-profils de Chrome, Edge, Brave, Opera, Firefox, Arc, Vivaldi",
                    "de" => "Chrome, Edge, Brave, Opera, Firefox, Arc, Vivaldi Multi-Profil Web- & Shader-Caches",
                    _ => "Chrome, Edge, Brave, Opera, Firefox, Arc, Vivaldi multi-profile web & shader cache (logins preserved)"
                };
                break;

            case "DevPackageCaches":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش حزم وأدوات المطورين",
                    "es" => "Caché de Paquetes de Desarrollo",
                    "fr" => "Caches de Packages Développeur",
                    "de" => "Entwickler- & Paket-Caches",
                    _ => "Developer & Package Caches"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "كاش تنزيلات الحزم في pip و npm و yarn و pnpm و NuGet و Cargo و Go و Bun و .NET",
                    "es" => "Descargas temporales de pip, npm, yarn, pnpm, NuGet, Cargo, Go, Bun y .NET",
                    "fr" => "Téléchargements de packages pip, npm, yarn, pnpm, NuGet, Cargo, Go, Bun et .NET",
                    "de" => "pip, npm, yarn, pnpm, NuGet, .gradle, Cargo, Go build, Bun, Deno und .NET Caches",
                    _ => "pip, npm, yarn, pnpm, NuGet, .gradle, Cargo, Go build, Bun, Deno, and .NET temp caches"
                };
                break;

            case "MobileDevResiduals":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش مزامنة الهواتف ومحاكيات التطوير",
                    "es" => "Sincronización Móvil y Demonios Dev",
                    "fr" => "Synchro Mobile et Démons Dev",
                    "de" => "Mobile Synchronisation & Dev-Daemons",
                    _ => "Mobile Sync & Dev Daemons"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "المطورين والهواتف",
                    "es" => "Móvil y Desarrollo",
                    "fr" => "Mobile & Développeur",
                    "de" => "Entwicklung & Mobil",
                    _ => "Dev & Mobile"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "كاش مزامنة iTunes المؤقت، كاش محاكي Android Studio، ومسودات Gradle و Cargo",
                    "es" => "Caché temporal de Apple iTunes, emulador Android Studio y daemons Gradle",
                    "fr" => "Cache temporaire iTunes, émulateur Android Studio et démons Gradle",
                    "de" => "Apple iTunes Sync-Temp-Cache, Android Studio Emulator-Cache, Gradle & Cargo",
                    _ => "Apple iTunes temp sync cache, Android Studio emulator cache, Gradle & Cargo caches"
                };
                break;

            case "CrashDumps":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "تقارير أخطاء النظام وسجلات الانهيار",
                    "es" => "Informes de Errores y Volcados",
                    "fr" => "Rapports d'Erreurs et Vidages",
                    "de" => "Fehlerberichte & Speicherabdrücke",
                    _ => "Windows Error Reports (WER)"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "سجلات تقارير أخطاء ويندوز (WER) وتفريغات ذاكرة العمليات عند الانهيار",
                    "es" => "Registros de Windows Error Reporting y volcados de memoria",
                    "fr" => "Journaux Windows Error Reporting et vidages mémoire processus",
                    "de" => "Windows Error Reporting Protokolle und Prozess-Speicherabdrücke (WER / Dumps)",
                    _ => "Windows Error Reporting logs & diagnostic queues (WER ReportArchive/ReportQueue)"
                };
                break;

            case "Thumbnails":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "كاش مصغرات مستكشف الملفات",
                    "es" => "Caché de Miniaturas de Windows",
                    "fr" => "Cache des Miniatures de l'Explorateur",
                    "de" => "Explorer Miniaturansichten-Cache",
                    _ => "Explorer Thumbnail Cache"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "قواعد بيانات مصغرات الصور ومقاطع الفيديو المؤقتة (thumbcache_*.db)",
                    "es" => "Bases de datos de miniaturas de fotos y videos",
                    "fr" => "Bases de données de miniatures d'images et vidéos",
                    "de" => "Gecachte Bild- & Video-Miniaturansichten (thumbcache_*.db)",
                    _ => "Cached image & video thumbnail databases (thumbcache_*.db)"
                };
                break;

            case "SystemUsageTraces":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "آثار استخدام النظام ومستكشف الملفات",
                    "es" => "Rastros de Uso del Sistema y Explorador",
                    "fr" => "Traces d'Utilisation Système & Explorateur",
                    "de" => "System- & Explorer-Nutzungsspuren",
                    _ => "System & Explorer Usage Traces"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "آثار الخصوصية",
                    "es" => "Privacidad",
                    "fr" => "Confidentialité",
                    "de" => "Privatsphäre",
                    _ => "Privacy Traces"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "اختصارات العناصر الأخيرة وقوائم الانتقال السريع (Jump Lists)",
                    "es" => "Accesos directos a elementos recientes y listas de accesos rápidos Jump Lists",
                    "fr" => "Raccourcis d'éléments récents et listes de raccourcis Jump Lists",
                    "de" => "Zuletzt verwendete Elemente und Jump-Listen (Automatic/CustomDestinations)",
                    _ => "Recent items shortcuts, AutomaticDestinations, and CustomDestinations Jump Lists"
                };
                break;

            case "RecycleBin":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "سلة المحذوفات لجميع الأقراص",
                    "es" => "Papelera de Reciclaje (Todos los Discos)",
                    "fr" => "Corbeille Windows (Tous les Disques)",
                    "de" => "Windows Papierkorb (Alle Laufwerke)",
                    _ => "Windows Recycle Bin"
                };
                target.Description = CurrentLanguage switch
                {
                    "ar" => "تفريغ سلة المحذوفات عبر جميع الأقراص المتصلة باستخدام Windows Shell API",
                    "es" => "Papeleras de todas las unidades físicas mediante Windows Shell API",
                    "fr" => "Corbeilles de tous les disques physiques via l'API Windows Shell",
                    "de" => "Papierkörbe aller physischen Laufwerke über die Windows-Shell-API",
                    _ => "All physical drive Recycle Bins via Windows Shell API (SHEmptyRecycleBin)"
                };
                break;

            case "OrphanedAppData":
                target.Name = CurrentLanguage switch
                {
                    "ar" => "بقايا البرامج المحذوفة غير المثبتة",
                    "es" => "Restos de Apps Desinstaladas",
                    "fr" => "Résidus d'Applications Désinstallées",
                    "de" => "Reste Deinstallierter Programme",
                    _ => "Orphaned AppData Leftovers"
                };
                target.Category = CurrentLanguage switch
                {
                    "ar" => "بقايا برامج",
                    "es" => "Huérfanos",
                    "fr" => "Orphelins",
                    "de" => "Verwaist",
                    _ => "Residuals"
                };
                target.SafetyBadge = CurrentLanguage switch
                {
                    "ar" => "🟡 بقايا محققة",
                    "es" => "🟡 Restos Verificados",
                    "fr" => "🟡 Résidus Vérifiés",
                    "de" => "🟡 Verifizierte Reste",
                    _ => "🟡 Verified Leftovers"
                };
                target.Description = CurrentLanguage switch
                {
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
