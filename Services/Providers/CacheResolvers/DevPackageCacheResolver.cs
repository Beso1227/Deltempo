using System.IO;

namespace WinTempCleaner.Services.Providers.CacheResolvers;

public static class DevPackageCacheResolver
{
    public static List<string> Resolve()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return new List<string>
        {
            Path.Combine(localAppData, "pip", "cache"),
            Path.Combine(localAppData, "npm-cache"),
            Path.Combine(roamingAppData, "npm-cache"),
            Path.Combine(localAppData, "Yarn", "Cache"),
            Path.Combine(localAppData, "pnpm", "store", "v3"),
            Path.Combine(localAppData, "pnpm-cache"),
            Path.Combine(localAppData, "NuGet", "v3-cache"),
            Path.Combine(localAppData, "NuGet", "plugins-cache"),
            Path.Combine(userProfile, ".cache"),
            Path.Combine(userProfile, ".gradle", "caches"),
            Path.Combine(userProfile, ".cargo", "registry", "cache"),
            Path.Combine(userProfile, ".cargo", "git", "db"),
            Path.Combine(userProfile, ".rustup", "downloads"),
            Path.Combine(userProfile, ".rustup", "tmp"),
            Path.Combine(userProfile, ".bun", "install", "cache"),
            Path.Combine(localAppData, "deno", "deps"),
            Path.Combine(localAppData, "go-build"),
            Path.Combine(localAppData, "Microsoft", "dotnet"),
            Path.Combine(localAppData, "Temp", ".net"),
            Path.Combine(userProfile, ".m2", "repository", ".cache"),
            Path.Combine(userProfile, ".m2", "temp"),
            Path.Combine(userProfile, ".nuget", "packages", "temp")
        };
    }
}
