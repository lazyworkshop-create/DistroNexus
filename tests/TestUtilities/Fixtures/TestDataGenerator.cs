using System.Text.Json;

namespace DistroNexus.TestUtilities.Fixtures;

/// <summary>
/// Generates test data for WSL instances, distros, and configurations
/// </summary>
public static class TestDataGenerator
{
    /// <summary>
    /// Generate mock WSL instance list JSON
    /// </summary>
    public static string GenerateWslInstancesJson(int count = 3)
    {
        var instances = new List<object>();
        
        for (int i = 0; i < count; i++)
        {
            instances.Add(new
            {
                Name = $"Ubuntu-{20 + i * 2}.04",
                State = i % 2 == 0 ? "Running" : "Stopped",
                Version = "2",
                BasePath = $"C:\\WSL\\Ubuntu-{20 + i * 2}",
                IsDefault = i == 0
            });
        }
        
        return JsonSerializer.Serialize(instances, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }
    
    /// <summary>
    /// Generate mock distro catalog JSON
    /// </summary>
    public static string GenerateDistrosCatalogJson(int count = 5, bool includeReleaseVersions = true)
    {
        var distros = new List<object>
        {
            new
            {
                Name = "Ubuntu 22.04 LTS",
                DistroId = "ubuntu-22.04",
                Family = "Ubuntu",
                Publisher = "Canonical",
                Version = "22.04",
                Architecture = "x64",
                PackageUrl = "https://cloud-images.ubuntu.com/releases/jammy/release/ubuntu-22.04-wsl-amd64-wsl.rootfs.tar.gz",
                PackageFormat = ".tar.gz",
                ReleaseDate = "2022-04-21",
                IsRelease = true
            },
            new
            {
                Name = "Ubuntu 20.04 LTS",
                DistroId = "ubuntu-20.04",
                Family = "Ubuntu",
                Publisher = "Canonical",
                Version = "20.04",
                Architecture = "x64",
                PackageUrl = "https://cloud-images.ubuntu.com/releases/focal/release/ubuntu-20.04-wsl-amd64-wsl.rootfs.tar.gz",
                PackageFormat = ".tar.gz",
                ReleaseDate = "2020-04-23",
                IsRelease = true
            },
            new
            {
                Name = "Debian 12 (Bookworm)",
                DistroId = "debian-12",
                Family = "Debian",
                Publisher = "Debian",
                Version = "12",
                Architecture = "x64",
                PackageUrl = "https://example.com/debian-12.tar.gz",
                PackageFormat = ".tar.gz",
                ReleaseDate = "2023-06-10",
                IsRelease = true
            },
            new
            {
                Name = "Kali Linux Rolling",
                DistroId = "kali-linux-rolling",
                Family = "Kali",
                Publisher = "Kali",
                Version = "rolling",
                Architecture = "x64",
                PackageUrl = "https://example.com/kali-rolling.tar.gz",
                PackageFormat = ".tar.gz",
                ReleaseDate = "2024-01-15",
                IsRelease = false
            },
            new
            {
                Name = "Arch Linux",
                DistroId = "arch-linux",
                Family = "Arch",
                Publisher = "Arch",
                Version = "current",
                Architecture = "x64",
                PackageUrl = "https://example.com/arch.tar.gz",
                PackageFormat = ".tar.gz",
                ReleaseDate = "2024-01-01",
                IsRelease = true
            }
        };
        
        var selectedDistros = distros.Take(Math.Min(count, distros.Count)).ToList();
        
        if (!includeReleaseVersions)
        {
            selectedDistros = selectedDistros
                .Select(d => 
                {
                    var dict = (d as dynamic);
                    dict.IsRelease = false;
                    return dict;
                })
                .ToList();
        }
        
        var catalog = new
        {
            LastUpdated = DateTime.UtcNow.ToString("o"),
            Version = "2.0",
            Distros = selectedDistros
        };
        
        return JsonSerializer.Serialize(catalog, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }
    
    /// <summary>
    /// Generate mock cache data JSON
    /// </summary>
    public static string GenerateCacheDataJson(int minutesOld = 0, int instanceCount = 2)
    {
        var instances = new List<object>();
        
        for (int i = 0; i < instanceCount; i++)
        {
            instances.Add(new
            {
                Name = $"Ubuntu-{i}",
                State = i % 2 == 0 ? "Running" : "Stopped",
                Version = "2",
                BasePath = $"C:\\WSL\\Ubuntu-{i}",
                IsDefault = i == 0
            });
        }
        
        var cacheData = new
        {
            CachedAt = DateTime.UtcNow.AddMinutes(-minutesOld).ToString("o"),
            Instances = instances,
            CacheVersion = "1.0"
        };
        
        return JsonSerializer.Serialize(cacheData, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }
    
    /// <summary>
    /// Generate mock WSL.exe --list output
    /// </summary>
    public static string GenerateWslListOutput(int count = 3, bool includeDefault = true)
    {
        var instances = new[]
        {
            "Ubuntu-22.04",
            "Debian",
            "Ubuntu-20.04",
            "Kali-Linux",
            "openSUSE-Leap-15.3"
        };
        
        var lines = new List<string>();
        
        for (int i = 0; i < Math.Min(count, instances.Length); i++)
        {
            var line = instances[i];
            if (i == 0 && includeDefault)
            {
                line += " (Default)";
            }
            lines.Add(line);
        }
        
        return string.Join(Environment.NewLine, lines);
    }
    
    /// <summary>
    /// Create a temporary test directory with mock files
    /// </summary>
    public static string CreateTestDirectory(string basePath = "")
    {
        if (string.IsNullOrEmpty(basePath))
        {
            basePath = Path.Combine(Path.GetTempPath(), "DistroNexusTests", Guid.NewGuid().ToString());
        }
        
        Directory.CreateDirectory(basePath);
        
        // Create subdirectories
        Directory.CreateDirectory(Path.Combine(basePath, "cache"));
        Directory.CreateDirectory(Path.Combine(basePath, "data"));
        Directory.CreateDirectory(Path.Combine(basePath, "packages"));
        
        return basePath;
    }
    
    /// <summary>
    /// Cleanup test directory
    /// </summary>
    public static void CleanupTestDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
