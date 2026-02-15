using System.Text.Json;

namespace DistroNexus.TestUtilities.Fixtures;

/// <summary>
/// Provides mock test data files (catalog.json, instances.json, etc.)
/// </summary>
public static class TestDataFiles
{
    /// <summary>
    /// Create a mock catalog.json file
    /// </summary>
    public static string CreateMockCatalogFile(string directory, int distroCount = 5)
    {
        var filePath = Path.Combine(directory, "catalog.json");
        var content = TestDataGenerator.GenerateDistrosCatalogJson(distroCount);
        File.WriteAllText(filePath, content);
        return filePath;
    }
    
    /// <summary>
    /// Create a mock instances cache file
    /// </summary>
    public static string CreateMockCacheFile(string directory, int minutesOld = 0, int instanceCount = 2)
    {
        var cachePath = Path.Combine(directory, "cache");
        Directory.CreateDirectory(cachePath);
        
        var filePath = Path.Combine(cachePath, "instances.json");
        var content = TestDataGenerator.GenerateCacheDataJson(minutesOld, instanceCount);
        File.WriteAllText(filePath, content);
        return filePath;
    }
    
    /// <summary>
    /// Create a mock package file for testing
    /// </summary>
    public static string CreateMockPackageFile(string directory, string format = ".tar.gz", int sizeKB = 100)
    {
        var fileName = $"test-distro{format}";
        var filePath = Path.Combine(directory, fileName);
        
        // Create a mock file with random content
        var content = new byte[sizeKB * 1024];
        new Random().NextBytes(content);
        File.WriteAllBytes(filePath, content);
        
        return filePath;
    }
    
    /// <summary>
    /// Setup a complete test environment with all necessary files
    /// </summary>
    public static TestEnvironment SetupTestEnvironment()
    {
        var basePath = TestDataGenerator.CreateTestDirectory();
        
        var catalogPath = CreateMockCatalogFile(Path.Combine(basePath, "data"));
        var cachePath = Path.Combine(basePath, "cache");
        var cacheFile = CreateMockCacheFile(basePath, minutesOld: 0, instanceCount: 3);
        var packagesPath = Path.Combine(basePath, "packages");
        Directory.CreateDirectory(packagesPath);
        
        return new TestEnvironment
        {
            BasePath = basePath,
            DataPath = Path.Combine(basePath, "data"),
            CachePath = cachePath,
            CatalogPath = catalogPath,
            CacheFile = cacheFile,
            PackagesPath = packagesPath
        };
    }
}

/// <summary>
/// Represents a test environment with all necessary directories and files
/// </summary>
public class TestEnvironment : IDisposable
{
    public string BasePath { get; init; } = string.Empty;
    public string DataPath { get; init; } = string.Empty;
    public string CachePath { get; init; } = string.Empty;
    public string CatalogPath { get; init; } = string.Empty;
    public string CacheFile { get; init; } = string.Empty;
    public string PackagesPath { get; init; } = string.Empty;
    
    public void Dispose()
    {
        TestDataGenerator.CleanupTestDirectory(BasePath);
        GC.SuppressFinalize(this);
    }
}
