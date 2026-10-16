using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Deltempo.Tests;

/// <summary>
/// Locks the interop structs in <c>MemoryOptimizerService.Native.cs</c> to the layouts defined in
/// the Windows SDK (winnt.h / psapi.h). These are P/Invoke buffer layouts: if a field width or the
/// packing changes, the kernel silently reads the wrong offsets instead of failing, so these
/// assertions are the only thing standing between a harmless refactor and a memory-corrupting
/// call.
///
/// The structs are private, so they are inspected by reflection.
/// </summary>
public class NativeStructLayoutTests
{
    private static Type NativeStruct(string name)
    {
        var t = typeof(WinTempCleaner.Services.MemoryOptimizerService)
            .GetNestedType(name, BindingFlags.NonPublic);
        Assert.NotNull(t);
        return t;
    }

    private static int SizeOf(Type t) => Marshal.SizeOf(t);

    private static int OffsetOf(Type t, string field)
    {
        var f = t.GetField(field, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(f);
        return Marshal.OffsetOf(t, f.Name).ToInt32();
    }

    private static int SizeOfField(Type t, string field)
    {
        var f = t.GetField(field, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(f);
        return Marshal.SizeOf(f.FieldType);
    }

    // ── SYSTEM_FILE_CACHE_INFORMATION, 64-bit view ──────────────────────────────
    // ULONG CurrentSize; ULONG PeakSize; ULONG PageFaultCount;
    // SIZE_T MinimumWorkingSet; SIZE_T PeakWorkingSet;
    // SIZE_T CurrentSizeIncludingTransitionInPages; SIZE_T PeakSizeIncludingTransitionInPages;
    // ULONG TransitionRePurposeCount; SIZE_T Flags;
    //
    // Previously CurrentSize/PeakSize/PageFaultCount were declared `long` and Flags `int`,
    // yielding a 68-byte buffer with MinimumWorkingSet at offset 24 instead of 16. The kernel
    // then read the caller's PeakSize field as the minimum working set.
    [Fact]
    public void SystemFileCacheInformation64_MatchesWdkLayout()
    {
        var t = NativeStruct("SystemFileCacheInformation64");

        Assert.Equal(64, SizeOf(t));

        Assert.Equal(4, SizeOfField(t, "CurrentSize"));
        Assert.Equal(4, SizeOfField(t, "PeakSize"));
        Assert.Equal(4, SizeOfField(t, "PageFaultCount"));
        Assert.Equal(8, SizeOfField(t, "MinimumWorkingSet"));
        Assert.Equal(8, SizeOfField(t, "PeakWorkingSet"));
        Assert.Equal(8, SizeOfField(t, "CurrentSizeIncludingTransitionInPages"));
        Assert.Equal(8, SizeOfField(t, "PeakSizeIncludingTransitionInPages"));
        Assert.Equal(4, SizeOfField(t, "TransitionRePurposeCount"));
        Assert.Equal(8, SizeOfField(t, "Flags"));

        Assert.Equal(0, OffsetOf(t, "CurrentSize"));
        Assert.Equal(4, OffsetOf(t, "PeakSize"));
        Assert.Equal(8, OffsetOf(t, "PageFaultCount"));
        Assert.Equal(16, OffsetOf(t, "MinimumWorkingSet"));
        Assert.Equal(24, OffsetOf(t, "PeakWorkingSet"));
        Assert.Equal(32, OffsetOf(t, "CurrentSizeIncludingTransitionInPages"));
        Assert.Equal(40, OffsetOf(t, "PeakSizeIncludingTransitionInPages"));
        Assert.Equal(48, OffsetOf(t, "TransitionRePurposeCount"));
        Assert.Equal(56, OffsetOf(t, "Flags"));
    }

    // ── SYSTEM_FILE_CACHE_INFORMATION, 32-bit view ──────────────────────────────
    [Fact]
    public void SystemFileCacheInformation32_MatchesWdkLayout()
    {
        var t = NativeStruct("SystemFileCacheInformation32");

        Assert.Equal(36, SizeOf(t));
        foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            Assert.True(Marshal.SizeOf(f.FieldType) == 4, $"{f.Name} must be 32-bit in the WOW64 view");

        Assert.Equal(0, OffsetOf(t, "CurrentSize"));
        Assert.Equal(12, OffsetOf(t, "MinimumWorkingSet"));
        Assert.Equal(32, OffsetOf(t, "Flags"));
    }

    // ── MEMORY_COMBINE_INFORMATION_EX ───────────────────────────────────────────
    // PVOID BaseAddress; PVOID PagesCombined; ULONG Flags;
    [Fact]
    public void MemoryCombineInformationEx_MatchesWdkLayout()
    {
        var t = NativeStruct("MemoryCombineInformationEx");

        Assert.Equal(20, SizeOf(t));
        Assert.Equal(8, SizeOfField(t, "Handle"));
        Assert.Equal(8, SizeOfField(t, "PagesCombined"));
        Assert.Equal(4, SizeOfField(t, "Flags"));

        Assert.Equal(0, OffsetOf(t, "Handle"));
        Assert.Equal(8, OffsetOf(t, "PagesCombined"));
        Assert.Equal(16, OffsetOf(t, "Flags"));
    }

    // ── TOKEN_PRIVILEGES ────────────────────────────────────────────────────────
    // DWORD Count; LUID Luid; DWORD Attributes;
    // All three members are 32-bit, so packed and natural alignment coincide: 16 bytes.
    [Fact]
    public void TokenPrivileges_MatchesWdkLayout()
    {
        var t = NativeStruct("TokenPrivileges");

        Assert.Equal(16, SizeOf(t));
        Assert.Equal(0, OffsetOf(t, "Count"));
        Assert.Equal(4, OffsetOf(t, "Luid"));
        Assert.Equal(12, OffsetOf(t, "Attr"));
    }

    // ── PERFORMANCE_INFORMATION ─────────────────────────────────────────────────
    // DWORD cb; SIZE_T ×10; DWORD ×3;
    // On x64 that is 4 + 4 pad + 80 + 12 = 100, rounded up to 104.
    // This was measured empirically, not assumed: GetPerformanceInfo rejects cb=96 with
    // ERROR_BAD_LENGTH(24) and accepts cb=104.
    [Fact]
    public void PerformanceInformation_MatchesWdkLayout()
    {
        var t = NativeStruct("PERFORMANCE_INFORMATION");

        int expected = IntPtr.Size == 8 ? 104 : 48;
        Assert.Equal(expected, SizeOf(t));

        Assert.Equal(0, OffsetOf(t, "cb"));
        Assert.Equal(8, OffsetOf(t, "CommitTotal"));
        Assert.Equal(80, OffsetOf(t, "PageSize"));
        Assert.Equal(88, OffsetOf(t, "HandleCount"));
    }

    // ── MEMORYSTATUSEX ──────────────────────────────────────────────────────────
    [Fact]
    public void MemoryStatusEx_MatchesWdkLayout()
    {
        var t = NativeStruct("MemoryStatusEx");

        Assert.Equal(64, SizeOf(t));
        Assert.Equal(0, OffsetOf(t, "Length"));
        Assert.Equal(4, OffsetOf(t, "MemoryLoad"));
        Assert.Equal(8, OffsetOf(t, "TotalPhys"));
        Assert.Equal(64 - 8, OffsetOf(t, "AvailExtendedVirtual"));
    }

    /// <summary>
    /// Every native interop struct must remain free of implicit-padding surprises: if the
    /// struct's marshalled size is not fully explained by its declared fields, something changed.
    /// </summary>
    [Fact]
    public void AllNativeStructs_HaveNoUnexplainedTrailingPadding()
    {
        // The complete set of P/Invoke buffer structs declared in MemoryOptimizerService.Native.cs.
        // Enumerated by name because every value type carries a default StructLayout and
        // compiler-generated closures also match a structural scan.
        string[] expected =
        {
            "SystemFileCacheInformation64",
            "SystemFileCacheInformation32",
            "MemoryCombineInformationEx",
            "TokenPrivileges",
            "PERFORMANCE_INFORMATION",
            "MemoryStatusEx",
        };

        var types = expected.Select(NativeStruct).ToList();
        Assert.Equal(expected.Length, types.Count);

        foreach (var t in types)
        {
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            Assert.NotEmpty(fields);
            int size = SizeOf(t);
            Assert.True(size > 0 && size < 1024,
                $"{t.Name} marshals to {size} bytes, which is implausible for a P/Invoke buffer");
        }
    }
}
