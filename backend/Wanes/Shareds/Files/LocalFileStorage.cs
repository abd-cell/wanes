using Microsoft.Extensions.Options;
using Wanes.Shareds.Models.Config;

namespace Wanes.Shareds.Files;

/// <summary>
/// <see cref="IFileStorage"/> over the local disk, rooted at
/// <see cref="StorageSettings.Root"/>.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string root;

    public LocalFileStorage(IOptions<StorageSettings> options, IHostEnvironment environment)
    {
        var configured = options.Value.Root;
        root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
        Directory.CreateDirectory(root);
    }

    public async Task<string> SaveAsync(string folder, string extension, Stream content, CancellationToken ct = default)
    {
        var safeFolder = Sanitize(folder);
        Directory.CreateDirectory(Path.Combine(root, safeFolder));

        var key = $"{safeFolder}/{Guid.NewGuid():N}{extension}";
        await using var file = File.Create(Resolve(key)!);
        await content.CopyToAsync(file, ct);
        return key;
    }

    public Stream? OpenRead(string key)
    {
        var path = Resolve(key);
        return path != null && File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
    }

    public Task DeleteAsync(string key)
    {
        var path = Resolve(key);
        if (path != null && File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Turns a storage key into an absolute path, refusing anything that would
    /// land outside the root. Keys are ours, but a row read back from the
    /// database is still input, and a stray <c>..</c> must not become a read of
    /// the server's own files.
    /// </summary>
    private string? Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        var full = Path.GetFullPath(Path.Combine(root, key));
        var bounds = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        return full.StartsWith(bounds, StringComparison.Ordinal) ? full : null;
    }

    private static string Sanitize(string folder) =>
        string.Concat(folder.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '/' ? c : '-'));
}
