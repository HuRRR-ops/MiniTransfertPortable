using System.Security.Cryptography;
using System.Text;

namespace MiniTransfertPortable;

internal sealed record SharedFile(int Id, string FullPath, string DisplayName, long Length);

internal sealed class ShareDescriptor
{
    private ShareDescriptor(
        string sourcePath,
        bool isFolder,
        string token,
        DateTimeOffset? expiresAt,
        byte[]? passwordSalt,
        byte[]? passwordHash,
        string authenticationCookie,
        IReadOnlyList<SharedFile> files)
    {
        SourcePath = sourcePath;
        IsFolder = isFolder;
        Token = token;
        ExpiresAt = expiresAt;
        PasswordSalt = passwordSalt;
        PasswordHash = passwordHash;
        AuthenticationCookie = authenticationCookie;
        Files = files;
        TotalBytes = files.Sum(file => file.Length);
    }

    public string SourcePath { get; }
    public bool IsFolder { get; }
    public string Token { get; }
    public DateTimeOffset? ExpiresAt { get; }
    public byte[]? PasswordSalt { get; }
    public byte[]? PasswordHash { get; }
    public string AuthenticationCookie { get; }
    public IReadOnlyList<SharedFile> Files { get; }
    public long TotalBytes { get; }
    public bool RequiresPassword => PasswordHash is not null;
    public string DisplayName => Path.GetFileName(SourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public bool IsExpired => ExpiresAt is not null && DateTimeOffset.UtcNow >= ExpiresAt.Value;

    public static ShareDescriptor Create(string sourcePath, bool isFolder, string? password, TimeSpan? lifetime)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        var files = new List<SharedFile>();

        if (isFolder)
        {
            if (!Directory.Exists(sourcePath))
            {
                throw new DirectoryNotFoundException("Le dossier sélectionné n'existe plus.");
            }

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            var id = 1;
            foreach (var fullPath in Directory.EnumerateFiles(sourcePath, "*", options))
            {
                try
                {
                    var info = new FileInfo(fullPath);
                    files.Add(new SharedFile(id++, info.FullName, Path.GetRelativePath(sourcePath, info.FullName), info.Length));
                }
                catch (IOException)
                {
                    // A file that disappears while the folder is scanned is simply omitted.
                }
                catch (UnauthorizedAccessException)
                {
                    // Inaccessible files are omitted rather than breaking the whole share.
                }
            }
        }
        else
        {
            var info = new FileInfo(sourcePath);
            if (!info.Exists)
            {
                throw new FileNotFoundException("Le fichier sélectionné n'existe plus.", sourcePath);
            }

            files.Add(new SharedFile(1, info.FullName, info.Name, info.Length));
        }

        var token = Base64Url(RandomNumberGenerator.GetBytes(24));
        var cookie = Base64Url(RandomNumberGenerator.GetBytes(24));
        byte[]? salt = null;
        byte[]? hash = null;

        if (!string.IsNullOrEmpty(password))
        {
            salt = RandomNumberGenerator.GetBytes(16);
            hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 150_000, HashAlgorithmName.SHA256, 32);
        }

        return new ShareDescriptor(
            sourcePath,
            isFolder,
            token,
            lifetime is null ? null : DateTimeOffset.UtcNow.Add(lifetime.Value),
            salt,
            hash,
            cookie,
            files);
    }

    public bool VerifyPassword(string candidate)
    {
        if (PasswordHash is null || PasswordSalt is null)
        {
            return true;
        }

        var candidateHash = Rfc2898DeriveBytes.Pbkdf2(candidate, PasswordSalt, 150_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(candidateHash, PasswordHash);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
