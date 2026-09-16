using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SimbaFlow.Infrastructure.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Stored files must stay inside the storage root.
///
/// The containment check compared a prefix without a separator, which let through any sibling
/// directory whose name merely started with the base: a path under "/app/storage-backup" satisfied
/// a check meant to confine it to "/app/storage".
/// </summary>
public class FileStorageContainmentTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileStorageService _storage;

    public FileStorageContainmentTests()
    {
        // A sibling that shares the root's name as a prefix — the case that used to slip through.
        _root = Path.Combine(Path.GetTempPath(), $"simbaflow-{Guid.NewGuid():N}", "storage");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(_root + "-backup");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:BasePath"] = _root })
            .Build();

        _storage = new LocalFileStorageService(configuration, NullLogger<LocalFileStorageService>.Instance);
    }

    [Fact]
    public async Task APathInsideTheRootIsServed()
    {
        var relative = Path.Combine("branding", "logo.png");
        Directory.CreateDirectory(Path.Combine(_root, "branding"));
        await File.WriteAllTextAsync(Path.Combine(_root, relative), "x");

        (await _storage.ExistsAsync(relative)).Should().BeTrue();
    }

    [Fact]
    public async Task ASiblingDirectorySharingTheRootsNameIsRefused()
    {
        await File.WriteAllTextAsync(Path.Combine(_root + "-backup", "secret.txt"), "x");

        // "../storage-backup/secret.txt" normalises to a path that starts with the root's text but
        // is not inside it.
        var escape = Path.Combine("..", $"{Path.GetFileName(_root)}-backup", "secret.txt");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _storage.ExistsAsync(escape));
    }

    [Fact]
    public async Task ClimbingOutOfTheRootIsRefused()
    {
        var escape = Path.Combine("..", "..", "..", "etc", "passwd");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _storage.ExistsAsync(escape));
    }

    public void Dispose()
    {
        var parent = Directory.GetParent(_root)?.FullName;
        if (parent is not null && Directory.Exists(parent))
            Directory.Delete(parent, recursive: true);
        GC.SuppressFinalize(this);
    }
}
