using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WinTempCleaner.Models;

namespace WinTempCleaner.Services;

public class FileForensicProfile
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FormattedSize => TargetFolderInfo.FormatBytes(SizeBytes);
    public DateTime LastModified { get; set; }
    public string DirectoryPath { get; set; } = string.Empty;

    // Forensic findings
    public string? DetectedMagicType { get; set; }
    public string? CompanyName { get; set; }
    public string? ProductName { get; set; }
    public string? FileDescription { get; set; }
    public string? DigitalSigner { get; set; }
    public bool IsDigitallySigned => !string.IsNullOrEmpty(DigitalSigner);
    public string? DetectedEcosystem { get; set; }
    public string? EcosystemItemName { get; set; }

    /// <summary>
    /// Generates an anonymized, privacy-safe summary of facts for AI and online lookup.
    /// Excludes user names or private paths while retaining actionable software context.
    /// </summary>
    public string BuildAnonymizedSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"File: {FileName}");
        sb.AppendLine($"Size: {FormattedSize} ({SizeBytes} bytes)");
        sb.AppendLine($"Extension: {Extension}");
        sb.AppendLine($"Last Modified: {LastModified:yyyy-MM-dd HH:mm:ss} UTC");

        if (!string.IsNullOrWhiteSpace(DetectedMagicType))
            sb.AppendLine($"True File Format (Magic Bytes): {DetectedMagicType}");

        if (!string.IsNullOrWhiteSpace(DetectedEcosystem))
        {
            sb.Append($"Software Ecosystem: {DetectedEcosystem}");
            if (!string.IsNullOrWhiteSpace(EcosystemItemName))
                sb.Append($" ({EcosystemItemName})");
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(ProductName))
            sb.AppendLine($"Product: {ProductName}");

        if (!string.IsNullOrWhiteSpace(CompanyName))
            sb.AppendLine($"Company / Vendor: {CompanyName}");

        if (!string.IsNullOrWhiteSpace(FileDescription))
            sb.AppendLine($"Description: {FileDescription}");

        if (!string.IsNullOrWhiteSpace(DigitalSigner))
            sb.AppendLine($"Authenticode Signer: {DigitalSigner}");

        // Generalized directory relative context (e.g., AppData\Local\Programs\... or Steam\steamapps\common\...)
        string generalizedFolder = GeneralizeFolderPath(DirectoryPath);
        if (!string.IsNullOrWhiteSpace(generalizedFolder))
            sb.AppendLine($"Location Context: {generalizedFolder}");

        return sb.ToString();
    }

    private static string GeneralizeFolderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        string lower = path.ToLowerInvariant();

        // Anonymize user profile
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).ToLowerInvariant();
        if (lower.StartsWith(userProfile))
        {
            string rel = path[userProfile.Length..].TrimStart('\\', '/');
            return $"%USERPROFILE%\\{rel}";
        }

        // Anonymize Program Files
        string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).ToLowerInvariant();
        if (lower.StartsWith(progFiles))
        {
            string rel = path[progFiles.Length..].TrimStart('\\', '/');
            return $"%ProgramFiles%\\{rel}";
        }

        string progFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).ToLowerInvariant();
        if (lower.StartsWith(progFilesX86))
        {
            string rel = path[progFilesX86.Length..].TrimStart('\\', '/');
            return $"%ProgramFiles(x86)%\\{rel}";
        }

        return path;
    }
}

public static class FileForensicsService
{
    /// <summary>
    /// Performs deep, non-destructive local forensics on a file to prepare metadata for AI/online analysis.
    /// </summary>
    public static FileForensicProfile AnalyzeFile(string filePath)
    {
        var profile = new FileForensicProfile
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Extension = Path.GetExtension(filePath).ToLowerInvariant(),
            DirectoryPath = Path.GetDirectoryName(filePath) ?? ""
        };

        try
        {
            var fi = new FileInfo(filePath);
            if (fi.Exists)
            {
                profile.SizeBytes = fi.Length;
                profile.LastModified = fi.LastWriteTimeUtc;
            }
        }
        catch { }

        // 1. Inspect Ecosystem Context from Directory Path
        DetectEcosystem(profile);

        // 2. Inspect Magic Bytes / Header
        DetectMagicHeader(profile);

        // 3. Inspect Windows PE Header & Authenticode (for binaries, DLLs, installers)
        DetectPeAndSignature(profile);

        return profile;
    }

    private static void DetectEcosystem(FileForensicProfile profile)
    {
        string dirLower = profile.DirectoryPath.ToLowerInvariant();
        string nameLower = profile.FileName.ToLowerInvariant();

        // Steam Games
        int steamIdx = dirLower.IndexOf(@"\steamapps\common\", StringComparison.OrdinalIgnoreCase);
        if (steamIdx >= 0)
        {
            profile.DetectedEcosystem = "Steam Game Asset";
            string afterCommon = profile.DirectoryPath[(steamIdx + @"\steamapps\common\".Length)..];
            string[] parts = afterCommon.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) profile.EcosystemItemName = parts[0];
            return;
        }

        // Epic Games
        int epicIdx = dirLower.IndexOf(@"\epic games\", StringComparison.OrdinalIgnoreCase);
        if (epicIdx >= 0)
        {
            profile.DetectedEcosystem = "Epic Games Store Title";
            string afterEpic = profile.DirectoryPath[(epicIdx + @"\epic games\".Length)..];
            string[] parts = afterEpic.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) profile.EcosystemItemName = parts[0];
            return;
        }

        // Riot Games
        if (dirLower.Contains(@"\riot games\"))
        {
            profile.DetectedEcosystem = "Riot Games Installation";
            profile.EcosystemItemName = "League of Legends / Valorant";
            return;
        }

        // AI Models / Local LLM
        if (dirLower.Contains(@"\.cache\huggingface\hub") || dirLower.Contains(@"\.ollama\models") ||
            dirLower.Contains(@"\lm-studio\models") || dirLower.Contains(@"\text-generation-webui\models") ||
            profile.Extension is ".gguf" or ".safetensors" or ".ckpt" or ".onnx")
        {
            profile.DetectedEcosystem = "AI / Machine Learning Model Weights";
            if (dirLower.Contains("huggingface")) profile.EcosystemItemName = "HuggingFace Hub Cache";
            else if (dirLower.Contains("ollama")) profile.EcosystemItemName = "Ollama Local Model Library";
            else if (dirLower.Contains("lm-studio")) profile.EcosystemItemName = "LM Studio Model Cache";
            return;
        }

        // Virtual Machines & WSL
        if (dirLower.Contains(@"\virtualbox vms\") || dirLower.Contains(@"\vmware\") ||
            dirLower.Contains(@"\hyper-v\") || dirLower.Contains(@"\wsl\") ||
            profile.Extension is ".vmdk" or ".vhd" or ".vhdx" or ".vdi" or ".qcow2")
        {
            profile.DetectedEcosystem = "Virtual Machine Disk Image";
            if (dirLower.Contains("virtualbox")) profile.EcosystemItemName = "Oracle VirtualBox VM";
            else if (dirLower.Contains("vmware")) profile.EcosystemItemName = "VMware Workstation VM";
            else if (dirLower.Contains("wsl") || nameLower.Contains("ext4.vhdx")) profile.EcosystemItemName = "Windows Subsystem for Linux (WSL2)";
            else if (dirLower.Contains("docker")) profile.EcosystemItemName = "Docker Desktop VM Image";
            return;
        }

        // Android SDK / Emulators
        if (dirLower.Contains(@"\android\sdk\") || dirLower.Contains(@"\.android\avd\"))
        {
            profile.DetectedEcosystem = "Android Developer SDK / Emulator";
            profile.EcosystemItemName = "Android Virtual Device Image";
            return;
        }

        // Unreal Engine / Unity
        if (dirLower.Contains(@"\unrealengine\") || dirLower.Contains(@"\deriveddatacache\") || profile.Extension is ".uasset" or ".umap")
        {
            profile.DetectedEcosystem = "Unreal Engine Cache / Project Asset";
            return;
        }
        if (dirLower.Contains(@"\library\artifacts\") || dirLower.Contains(@"\library\packagecache\") || profile.Extension is ".unitypackage")
        {
            profile.DetectedEcosystem = "Unity Engine Artifact Cache";
            return;
        }

        // Adobe Media Cache
        if (dirLower.Contains(@"\adobe\common\media cache") || dirLower.Contains(@"\adobe\common\media cache files"))
        {
            profile.DetectedEcosystem = "Adobe Premiere / After Effects Media Cache";
            return;
        }

        // ISO / Disc Images
        if (profile.Extension is ".iso" or ".img" or ".bin")
        {
            profile.DetectedEcosystem = "Operating System / Disc Image";
            return;
        }

        // Windows System / WinSxS
        if (dirLower.Contains(@"\windows\winsxs\") || dirLower.Contains(@"\windows\system32\"))
        {
            profile.DetectedEcosystem = "Core Windows OS System Component";
            return;
        }

        // Downloads / Staging
        if (dirLower.Contains(@"\downloads\"))
        {
            profile.DetectedEcosystem = "User Downloads Folder";
            return;
        }
    }

    private static void DetectMagicHeader(FileForensicProfile profile)
    {
        if (!File.Exists(profile.FilePath) || profile.SizeBytes < 4) return;

        try
        {
            using var fs = new FileStream(profile.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            byte[] header = new byte[Math.Min(32, (int)fs.Length)];
            int read = fs.Read(header, 0, header.Length);
            if (read < 4) return;

            // Magic byte tests
            if (header[0] == 0x50 && header[1] == 0x4B && (header[2] == 0x03 || header[2] == 0x05))
            {
                profile.DetectedMagicType = "ZIP Archive / Open Container";
            }
            else if (header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC && header[3] == 0xAF)
            {
                profile.DetectedMagicType = "7-Zip Compressed Archive";
            }
            else if (header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 && header[3] == 0x21)
            {
                profile.DetectedMagicType = "RAR Archive";
            }
            else if (header[0] == 0x1F && header[1] == 0x8B)
            {
                profile.DetectedMagicType = "GZIP Compressed Archive";
            }
            else if (header[0] == 0x4D && header[1] == 0x5A)
            {
                profile.DetectedMagicType = "Windows PE Executable / DLL";
            }
            else if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46)
            {
                profile.DetectedMagicType = "PDF Document";
            }
            else if (header[0] == 0x53 && header[1] == 0x51 && header[2] == 0x4C && header[3] == 0x69)
            {
                profile.DetectedMagicType = "SQLite 3 Database File";
            }
            else if (header[0] == 0x47 && header[1] == 0x47 && header[2] == 0x55 && header[3] == 0x46)
            {
                profile.DetectedMagicType = "GGUF Quantized AI Model Weights";
            }
            else if (header[0] == 0x4B && header[1] == 0x44 && header[2] == 0x4D && header[3] == 0x56)
            {
                profile.DetectedMagicType = "VMDK Virtual Machine Disk";
            }
            else if (read >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70)
            {
                profile.DetectedMagicType = "MP4 / ISO Media Video Container";
            }
            else if (header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
            {
                profile.DetectedMagicType = "Matroska (MKV) Media Container";
            }
            else if (header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0)
            {
                profile.DetectedMagicType = "Microsoft Compound Document / MSI Package";
            }
        }
        catch { }
    }

    private static void DetectPeAndSignature(FileForensicProfile profile)
    {
        if (profile.Extension is not (".exe" or ".dll" or ".sys" or ".msi")) return;
        if (!File.Exists(profile.FilePath)) return;

        try
        {
            // Windows FileVersionInfo
            var vi = FileVersionInfo.GetVersionInfo(profile.FilePath);
            if (!string.IsNullOrWhiteSpace(vi.ProductName)) profile.ProductName = vi.ProductName.Trim();
            if (!string.IsNullOrWhiteSpace(vi.CompanyName)) profile.CompanyName = vi.CompanyName.Trim();
            if (!string.IsNullOrWhiteSpace(vi.FileDescription)) profile.FileDescription = vi.FileDescription.Trim();
        }
        catch { }

        try
        {
            // Authenticode Digital Certificate check
#pragma warning disable SYSLIB0057
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(profile.FilePath));
#pragma warning restore SYSLIB0057
            string subject = cert.Subject;
            // Parse CN=Company Name from subject
            string signer = ExtractCommonName(subject);
            if (!string.IsNullOrWhiteSpace(signer))
            {
                profile.DigitalSigner = signer;
                if (string.IsNullOrWhiteSpace(profile.CompanyName))
                    profile.CompanyName = signer;
            }
        }
        catch { }
    }

    private static string ExtractCommonName(string distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName)) return "";
        string[] parts = distinguishedName.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            string p = part.Trim();
            if (p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return p[3..].Trim().Trim('"');
            }
        }
        return distinguishedName;
    }
}
