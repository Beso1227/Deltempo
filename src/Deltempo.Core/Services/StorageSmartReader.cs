using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WinTempCleaner.Models;

/// <summary>
/// The NVMe SMART / Health log page (Log Identifier 02h), read straight from the
/// controller. Values are the firmware's own counters - nothing here is estimated.
/// </summary>
internal readonly record struct NvmeSmartHealth(
    double TemperatureCelsius,
    int WearPercent,
    long PowerOnHours,
    long MediaErrors);

/// <summary>
/// Why a direct SMART read ended the way it did. The distinction matters: only
/// "the provider refused this process" is something elevation can fix. A driver
/// that answers "incorrect function" / "not supported" to the documented queries
/// does not forward the controller's log page at all, so an administrator gets
/// exactly the same answer.
/// </summary>
internal enum SmartReadOutcome
{
    /// <summary>The log page was read and parsed.</summary>
    Read,

    /// <summary>The storage driver does not implement the SMART log query.</summary>
    NotSupported,

    /// <summary>The provider refused the request to this process; elevation may help.</summary>
    AccessDenied,

    /// <summary>The request was made but failed for another reason.</summary>
    Failed,
}

/// <summary>
/// The subset of the storage driver's ATA-style SMART attribute table that is
/// unambiguous. Attribute 190 (Airflow/Drive Temperature, raw byte 0) and attribute
/// 194 (Temperature, raw byte 0) are Celsius; attribute 9 is power-on hours.
/// Attributes 231 (SSD Life Left) and 233 (Media Wearout Indicator) carry remaining
/// life in their normalized value, so wear is <c>100 - value</c>.
///
/// Attribute 5 is deliberately not read: its official meaning is "Reallocated Sector
/// Count", and the value some drivers synthesize for it cannot be proven to be
/// NVMe "Percentage Used", so reporting wear from it would be a guess.
/// </summary>
internal readonly record struct WmiSmartCounters(
    double? TemperatureCelsius,
    int? WearPercent,
    long? PowerOnHours);

/// <summary>
/// Direct NVMe SMART reader.
///
/// Why this exists: <c>MSFT_StorageReliabilityCounter</c> is the only source the
/// Windows Storage provider offers, and it denies the class to a standard user. Even
/// elevated it can answer with an empty Temperature/Wear for an NVMe device, because
/// those fields depend on the storage driver forwarding the controller's log page.
/// This reader asks the controller directly, so temperature, wear and power-on hours
/// can still be reported when the provider's row is empty.
///
/// Two documented request shapes are attempted, adapter then device, and finally the
/// raw protocol-command pass-through:
///   * <c>IOCTL_STORAGE_QUERY_PROPERTY</c> + <c>STORAGE_PROTOCOL_SPECIFIC_DATA</c>
///     (<c>StorageAdapterProtocolSpecificProperty</c>, then
///     <c>StorageDeviceProtocolSpecificProperty</c>);
///   * <c>IOCTL_STORAGE_PROTOCOL_COMMAND</c> carrying an NVMe Get Log Page command.
///
/// Everything is read-only and guarded: a failed or unrecognisable page is rejected
/// with the exact Win32 error instead of being turned into a number.
/// </summary>
internal static class StorageSmartReader
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint OpenExisting = 3;

    private const string WmiNamespace = @"root\wmi";

    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const uint IoctlStorageProtocolCommand = 0x002DD3C0;

    private const uint StorageAdapterProtocolSpecificProperty = 17;
    private const uint StorageDeviceProtocolSpecificProperty = 18;

    private const uint ProtocolTypeNvme = 3;
    private const uint NvmeDataTypeLogPage = 2;
    private const uint NvmeSmartHealthLogPage = 0x02;

    // STORAGE_PROPERTY_QUERY: PropertyId (0), QueryType (4); AdditionalParameters starts at 8.
    private const int QueryHeaderSize = 8;
    private const int ProtocolSpecificSize = 40;
    private const int SmartPageSize = 512;

    // STORAGE_PROTOCOL_COMMAND with the inline Command[] array at offset 80.
    private const int ProtocolCommandHeaderSize = 80;
    private const int NvmeCommandSize = 64;

    private const int MinimumSmartPageLength = 192;
    private const int MinimumPlausibleKelvin = 200;
    private const int MaximumPlausibleKelvin = 400;

    private const int ErrorAccessDenied = 5;
    private const int ErrorInvalidFunction = 1;
    private const int ErrorNotSupported = 50;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    /// <summary>
    /// Reads one physical drive's SMART log page. Returns false with a concrete
    /// <paramref name="failure"/> (the Win32 error, or the reason the page was
    /// rejected) and an <paramref name="outcome"/> that says whether elevation could
    /// possibly change the answer, so callers can say what actually happened instead
    /// of guessing.
    /// </summary>
    public static bool TryReadNvmeSmart(
        uint diskNumber,
        out NvmeSmartHealth? health,
        out SmartReadOutcome outcome,
        out string? failure)
    {
        health = null;
        failure = null;
        outcome = SmartReadOutcome.Failed;
        string path = $@"\\.\PhysicalDrive{diskNumber}";

        // A firmware request needs a read/write handle, which needs elevation. The
        // query-only fallback still lets the property path try, and its failure text
        // makes the elevation requirement explicit rather than silently empty.
        SafeFileHandle handle = CreateFileW(
            path, GenericRead | GenericWrite, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

        bool writable = !handle.IsInvalid;
        int openError = writable ? 0 : Marshal.GetLastWin32Error();
        if (!writable)
        {
            handle.Dispose();
            handle = CreateFileW(path, 0, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int queryOnlyError = Marshal.GetLastWin32Error();
                handle.Dispose();
                failure = $"could not open {path} (read/write: {Describe(openError)}; query-only: {Describe(queryOnlyError)})";
                outcome = queryOnlyError == ErrorAccessDenied
                    ? SmartReadOutcome.AccessDenied
                    : SmartReadOutcome.Failed;
                return false;
            }
        }

        using (handle)
        {
            var failures = new List<string>(3);
            var errors = new List<int>(3);

            if (TryQueryProtocolSpecific(
                    handle, StorageAdapterProtocolSpecificProperty, out health, out string? adapterFailure, out int adapterError))
            {
                outcome = SmartReadOutcome.Read;
                return true;
            }

            failures.Add(adapterFailure!);
            errors.Add(adapterError);

            if (TryQueryProtocolSpecific(
                    handle, StorageDeviceProtocolSpecificProperty, out health, out string? deviceFailure, out int deviceError))
            {
                outcome = SmartReadOutcome.Read;
                return true;
            }

            failures.Add(deviceFailure!);
            errors.Add(deviceError);

            if (TryProtocolCommand(handle, out health, out string? commandFailure, out int commandError))
            {
                outcome = SmartReadOutcome.Read;
                return true;
            }

            failures.Add(commandFailure!);
            errors.Add(commandError);

            string detail = string.Join("; ", failures);
            failure = writable
                ? detail
                : $"reading the drive firmware needs administrator rights (open: {Describe(openError)}); {detail}";
            outcome = Classify(errors);
            return false;
        }
    }

    /// <summary>
    /// Reads the ATA-style SMART attribute table the Windows storage driver exposes
    /// through <c>root\wmi</c>. This is the source that still answers when the NVMe
    /// protocol query is rejected (Intel RST / VMD) and the Storage provider's
    /// reliability row is empty; the numbers are the drive's own counters as the
    /// storage stack reports them, not an estimate. The class is normally readable
    /// only by an administrator, so an access denial is reported as something
    /// elevation can fix rather than as a device that lacks the data.
    ///
    /// Rows are matched to a disk number through <c>Win32_DiskDrive.Index</c> and
    /// <c>PNPDeviceID</c>. When a row cannot be matched it is dropped: attributing a
    /// temperature to the wrong disk would be worse than reporting nothing.
    /// </summary>
    public static Dictionary<uint, WmiSmartCounters> ReadWmiSmartCounters(out string? failure, out bool accessDenied)
    {
        var counters = new Dictionary<uint, WmiSmartCounters>();
        failure = null;
        accessDenied = false;
        try
        {
            Dictionary<uint, string> pnpByDisk = QueryDiskPnpIds();
            using var searcher = new ManagementObjectSearcher(
                WmiNamespace,
                "SELECT InstanceName, VendorSpecific FROM MSStorageDriver_ATAPISmartData");
            using ManagementObjectCollection rows = searcher.Get();
            foreach (ManagementObject row in rows)
            {
                using (row)
                {
                    if (row["VendorSpecific"] is not byte[] page) continue;
                    string instance = row["InstanceName"]?.ToString() ?? string.Empty;
                    if (!TryMatchDisk(instance, pnpByDisk, out uint diskNumber)) continue;
                    if (TryParseSmartAttributes(page, out WmiSmartCounters parsed))
                    {
                        counters[diskNumber] = parsed;
                    }
                }
            }

            if (counters.Count == 0)
            {
                failure = "the storage driver returned no SMART attributes for any disk";
            }
        }
        catch (ManagementException ex) when (
            ex.ErrorCode is ManagementStatus.AccessDenied or ManagementStatus.PrivilegeNotHeld)
        {
            accessDenied = true;
            failure = "MSStorageDriver_ATAPISmartData is denied to this process (Access denied)";
        }
        catch (UnauthorizedAccessException)
        {
            accessDenied = true;
            failure = "MSStorageDriver_ATAPISmartData is denied to this process (Access denied)";
        }
        catch (ManagementException ex)
        {
            failure = $"MSStorageDriver_ATAPISmartData is unavailable ({ex.Message.Trim()})";
        }
        catch (Exception ex)
        {
            failure = $"MSStorageDriver_ATAPISmartData failed ({ex.GetType().Name})";
        }

        return counters;
    }

    /// <summary>
    /// Parses one SMART attribute table. The table starts at offset 2 for both the
    /// 362-byte <c>MSStorageDriver_ATAPISmartData</c> page and the 512-byte
    /// <c>MSStorageDriver_FailurePredictData</c> page; every entry is 12 bytes.
    /// Fields the page does not carry stay null.
    /// </summary>
    public static bool TryParseSmartAttributes(ReadOnlySpan<byte> page, out WmiSmartCounters counters)
    {
        counters = default;
        if (page.Length < 2 + 12) return false;

        double? temperature = null;
        int? wear = null;
        long? powerOnHours = null;

        for (int at = 2; at + 12 <= page.Length; at += 12)
        {
            byte id = page[at];
            if (id == 0) break;

            byte normalized = page[at + 3];
            uint raw = BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(at + 5, 4));

            if (id is 194 or 190 && temperature is null)
            {
                // Raw byte 0 is the current temperature in Celsius on every ATA SMART
                // implementation that publishes these attributes.
                double celsius = page[at + 5];
                if (celsius is > 0 and <= 100) temperature = celsius;
            }
            else if (id == 9 && powerOnHours is null)
            {
                powerOnHours = raw;
            }
            else if (id is 231 or 233 && wear is null && normalized is > 0 and <= 100)
            {
                // Normalized value is remaining life; 100 - value is percentage used.
                wear = 100 - normalized;
            }
        }

        counters = new WmiSmartCounters(temperature, wear, powerOnHours);
        return temperature.HasValue || wear.HasValue || powerOnHours.HasValue;
    }

    private static Dictionary<uint, string> QueryDiskPnpIds()
    {
        var map = new Dictionary<uint, string>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\cimv2", "SELECT Index, PNPDeviceID FROM Win32_DiskDrive");
            using ManagementObjectCollection rows = searcher.Get();
            foreach (ManagementObject row in rows)
            {
                using (row)
                {
                    string? pnp = row["PNPDeviceID"]?.ToString();
                    if (string.IsNullOrWhiteSpace(pnp)) continue;
                    if (!uint.TryParse(row["Index"]?.ToString(), out uint index)) continue;
                    map[index] = pnp;
                }
            }
        }
        catch (Exception)
        {
            // Without the association the counters cannot be attributed to a disk.
        }

        return map;
    }

    private static bool TryMatchDisk(
        string instanceName,
        Dictionary<uint, string> pnpByDisk,
        out uint diskNumber)
    {
        foreach (KeyValuePair<uint, string> entry in pnpByDisk)
        {
            if (instanceName.StartsWith(entry.Value, StringComparison.OrdinalIgnoreCase))
            {
                diskNumber = entry.Key;
                return true;
            }
        }

        diskNumber = 0;
        return false;
    }

    private static bool TryQueryProtocolSpecific(
        SafeFileHandle handle,
        uint propertyId,
        out NvmeSmartHealth? health,
        out string? failure,
        out int win32Error)
    {
        health = null;
        failure = null;
        win32Error = 0;

        int inputSize = QueryHeaderSize + ProtocolSpecificSize;
        int outputSize = QueryHeaderSize + ProtocolSpecificSize + SmartPageSize;

        IntPtr input = Marshal.AllocHGlobal(inputSize);
        IntPtr output = Marshal.AllocHGlobal(outputSize);
        try
        {
            Zero(input, inputSize);
            Zero(output, outputSize);

            Marshal.WriteInt32(input, 0, (int)propertyId);
            Marshal.WriteInt32(input, 4, 0); // PropertyStandardQuery

            int p = QueryHeaderSize;
            Marshal.WriteInt32(input, p + 0, (int)ProtocolTypeNvme);
            Marshal.WriteInt32(input, p + 4, (int)NvmeDataTypeLogPage);
            Marshal.WriteInt32(input, p + 8, (int)NvmeSmartHealthLogPage); // ProtocolDataRequestValue (log id)
            Marshal.WriteInt32(input, p + 12, 0);                          // ProtocolDataRequestSubValue
            Marshal.WriteInt32(input, p + 16, ProtocolSpecificSize);       // ProtocolDataOffset
            Marshal.WriteInt32(input, p + 20, SmartPageSize);              // ProtocolDataLength

            if (!DeviceIoControl(
                    handle, IoctlStorageQueryProperty,
                    input, (uint)inputSize,
                    output, (uint)outputSize,
                    out _, IntPtr.Zero))
            {
                win32Error = Marshal.GetLastWin32Error();
                failure = $"IOCTL_STORAGE_QUERY_PROPERTY({propertyId}) failed: {Describe(win32Error)}";
                return false;
            }

            int dataOffset = Marshal.ReadInt32(output, QueryHeaderSize + 16);
            int dataLength = Marshal.ReadInt32(output, QueryHeaderSize + 20);
            if (dataLength < SmartPageSize)
            {
                failure = $"IOCTL_STORAGE_QUERY_PROPERTY({propertyId}) returned {dataLength} byte(s), need {SmartPageSize}";
                return false;
            }

            int dataAt = QueryHeaderSize + (dataOffset >= ProtocolSpecificSize ? dataOffset : ProtocolSpecificSize);
            if (dataAt + SmartPageSize > outputSize)
            {
                failure = $"IOCTL_STORAGE_QUERY_PROPERTY({propertyId}) put the data at offset {dataAt}, outside the {outputSize}-byte buffer";
                return false;
            }

            byte[] page = new byte[SmartPageSize];
            Marshal.Copy(output + dataAt, page, 0, SmartPageSize);
            if (TryParseNvmeSmartLog(page, out health))
            {
                return true;
            }

            failure = $"IOCTL_STORAGE_QUERY_PROPERTY({propertyId}) returned a page that is not a valid NVMe SMART log";
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(input);
            Marshal.FreeHGlobal(output);
        }
    }

    private static bool TryProtocolCommand(
        SafeFileHandle handle,
        out NvmeSmartHealth? health,
        out string? failure,
        out int win32Error)
    {
        health = null;
        failure = null;
        win32Error = 0;

        int headerSize = ProtocolCommandHeaderSize;
        int total = headerSize + NvmeCommandSize + SmartPageSize;

        IntPtr buffer = Marshal.AllocHGlobal(total);
        try
        {
            Zero(buffer, total);

            Marshal.WriteInt32(buffer, 0, 1);                         // Version = STORAGE_PROTOCOL_STRUCTURE_VERSION
            Marshal.WriteInt32(buffer, 4, headerSize);                // Length
            Marshal.WriteInt32(buffer, 8, (int)ProtocolTypeNvme);     // ProtocolType
            Marshal.WriteInt32(buffer, 12, 0);                        // Flags
            Marshal.WriteInt32(buffer, 24, NvmeCommandSize);          // CommandLength
            Marshal.WriteInt32(buffer, 28, 0);                        // ErrorInfoLength
            Marshal.WriteInt32(buffer, 32, 0);                        // DataToDeviceTransferLength
            Marshal.WriteInt32(buffer, 36, SmartPageSize);            // DataFromDeviceTransferLength
            Marshal.WriteInt32(buffer, 40, 60);                       // TimeOutValue (seconds)
            Marshal.WriteInt32(buffer, 44, 0);                        // ErrorInfoOffset
            Marshal.WriteInt32(buffer, 48, 0);                        // DataToDeviceBufferOffset
            Marshal.WriteInt32(buffer, 52, headerSize + NvmeCommandSize); // DataFromDeviceBufferOffset

            int command = headerSize;
            Marshal.WriteByte(buffer, command + 0, 0x02);             // NVMe Get Log Page
            Marshal.WriteInt32(buffer, command + 4, -1);              // NSID = 0xFFFFFFFF
            Marshal.WriteInt32(buffer, command + 40, 0x02 | (127 << 16)); // CDW10: LID 02h, NUMDL 127 = 512 bytes

            if (!DeviceIoControl(
                    handle, IoctlStorageProtocolCommand,
                    buffer, (uint)total,
                    buffer, (uint)total,
                    out _, IntPtr.Zero))
            {
                win32Error = Marshal.GetLastWin32Error();
                failure = $"IOCTL_STORAGE_PROTOCOL_COMMAND failed: {Describe(win32Error)}";
                return false;
            }

            int returnStatus = Marshal.ReadInt32(buffer, 16);
            if (returnStatus != 0)
            {
                failure = $"IOCTL_STORAGE_PROTOCOL_COMMAND returned status 0x{returnStatus:X}";
                return false;
            }

            byte[] page = new byte[SmartPageSize];
            Marshal.Copy(buffer + headerSize + NvmeCommandSize, page, 0, SmartPageSize);
            if (TryParseNvmeSmartLog(page, out health))
            {
                return true;
            }

            failure = "IOCTL_STORAGE_PROTOCOL_COMMAND returned a page that is not a valid NVMe SMART log";
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>
    /// Parses the 512-byte NVMe SMART / Health log page. The layout is fixed by the
    /// NVMe specification: critical warning (0), composite temperature in Kelvin
    /// (1-2, little-endian), percentage used (5), power-on hours (128, 128-bit LE),
    /// media/data-integrity errors (160, 128-bit LE).
    /// </summary>
    public static bool TryParseNvmeSmartLog(ReadOnlySpan<byte> page, out NvmeSmartHealth? health)
    {
        health = null;
        if (page.Length < MinimumSmartPageLength) return false;

        // An all-zero page is a failed read, not a 0 K drive with 0 % wear.
        bool anyNonZero = false;
        foreach (byte value in page)
        {
            if (value == 0) continue;
            anyNonZero = true;
            break;
        }

        if (!anyNonZero) return false;

        int kelvin = page[1] | (page[2] << 8);
        if (kelvin is < MinimumPlausibleKelvin or > MaximumPlausibleKelvin) return false;

        double celsius = Math.Round(kelvin - 273.15, 1);
        int wearPercent = page[5];
        long powerOnHours = ToInt64Saturating(BinaryPrimitives.ReadUInt64LittleEndian(page.Slice(128, 8)));
        long mediaErrors = ToInt64Saturating(BinaryPrimitives.ReadUInt64LittleEndian(page.Slice(160, 8)));

        health = new NvmeSmartHealth(celsius, wearPercent, powerOnHours, mediaErrors);
        return true;
    }

    private static void Zero(IntPtr buffer, int length)
    {
        for (int i = 0; i < length; i++)
        {
            Marshal.WriteByte(buffer, i, 0);
        }
    }

    /// <summary>
    /// Decides what a set of failed attempts means. A documented driver answer of
    /// "incorrect function" (1) or "not supported" (50) is definitive: that query is
    /// not implemented by the driver stack, so an elevated process receives exactly
    /// the same answer and the report must not send the user to a UAC prompt that
    /// cannot help. Access denied (5) is the one signal elevation can repair, so it is
    /// only used when no attempt proved the query unsupported.
    /// </summary>
    internal static SmartReadOutcome Classify(IReadOnlyList<int> errors)
    {
        bool anyNotSupported = false;
        bool anyAccessDenied = false;
        foreach (int error in errors)
        {
            if (IsNotSupportedError(error))
            {
                anyNotSupported = true;
            }
            else if (error == ErrorAccessDenied)
            {
                anyAccessDenied = true;
            }
        }

        if (anyNotSupported) return SmartReadOutcome.NotSupported;
        if (anyAccessDenied) return SmartReadOutcome.AccessDenied;
        return SmartReadOutcome.Failed;
    }

    private static bool IsNotSupportedError(int error) =>
        error is ErrorInvalidFunction or ErrorNotSupported;

    private static long ToInt64Saturating(ulong value) => value > long.MaxValue ? long.MaxValue : (long)value;

    private static string Describe(int error) => $"{error} ({new Win32Exception(error).Message})";
}
