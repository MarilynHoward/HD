using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace RestaurantPosWpf;

/// <summary>
/// Remote-host sync for staff documents (ID PDFs, profile images). Reads the destination root
/// from <c>App.config</c> key <c>StaffDocumentsRemoteRoot</c> and mirrors the same relative layout
/// used under <see cref="StaffAccessUserDetails.StaffDocumentsRepositoryRoot"/>
/// (<c>docs\user_docs\*</c>, <c>images\user_images\*</c>).
/// <para>
/// Two destination kinds are supported:
/// <list type="bullet">
/// <item><description><b>Local / UNC folder</b> — any path that does not start with <c>sftp://</c>,
///   e.g. <c>D:\Dev\Cursor\HD\Remote_Documents</c> or <c>\\server\share\staff</c>. The file is copied
///   via asynchronous <see cref="FileStream"/> I/O; missing directories are created on the fly.</description></item>
/// <item><description><b>SFTP URI</b> — <c>sftp://host[:port]/remote/root/path</c>. Credentials live
///   in App.config keys <c>StaffDocumentsRemoteSftpUser</c> and <c>StaffDocumentsRemoteSftpPassword</c>
///   and MUST be stored as <see cref="Crypt"/>-encrypted ciphertext — they are decrypted at runtime via
///   <see cref="App"/><c>.aps.crypt.DoDecrypt</c>. Uses SSH.NET (<see cref="SftpClient"/>) to upload the
///   file; missing remote directories are created one level at a time.</description></item>
/// </list>
/// </para>
/// <para>
/// All methods are safe to call from a background thread. The UI layer kicks sync off as
/// fire-and-forget so the user is never blocked on network I/O.
/// </para>
/// </summary>
public static class StaffDocumentsRemoteSync
{
    /// <summary>App.config key for the remote destination (local/UNC path or an <c>sftp://</c> URI without credentials).</summary>
    public const string ConfigKeyRemoteRoot = "StaffDocumentsRemoteRoot";

    /// <summary>App.config key for the Crypt-encrypted SFTP username.</summary>
    public const string ConfigKeySftpUser = "StaffDocumentsRemoteSftpUser";

    /// <summary>App.config key for the Crypt-encrypted SFTP password.</summary>
    public const string ConfigKeySftpPassword = "StaffDocumentsRemoteSftpPassword";

    /// <summary>Raw configuration value for the remote root, or null/empty when not configured.</summary>
    public static string? RemoteRoot => ConfigurationManager.AppSettings[ConfigKeyRemoteRoot];

    /// <summary>True when a remote destination is configured in App.config.</summary>
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(RemoteRoot);

    /// <summary>
    /// Produce the ciphertext that should be pasted into <see cref="ConfigKeySftpUser"/> or
    /// <see cref="ConfigKeySftpPassword"/>. Wraps <see cref="Crypt.DoEncrypt"/> so the developer
    /// never has to reach into <c>App.aps.crypt</c> directly. Call once (e.g. from an Immediate
    /// Window or a one-off tool) and copy the result into App.config.
    /// </summary>
    internal static string EncryptForConfig(string plain) => App.aps.crypt.DoEncrypt(plain ?? string.Empty);

    /// <summary>Destination type resolved from the configured root.</summary>
    public enum DestinationKind
    {
        None,
        LocalOrUnc,
        Sftp
    }

    /// <summary>Outcome of a push attempt. <see cref="Success"/> is true only when the file landed at the remote.</summary>
    public sealed class Result
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
        public string? RemoteFullPath { get; init; }
    }

    public static DestinationKind GetKind()
    {
        var root = RemoteRoot;
        if (string.IsNullOrWhiteSpace(root))
            return DestinationKind.None;
        return root!.TrimStart().StartsWith("sftp://", StringComparison.OrdinalIgnoreCase)
            ? DestinationKind.Sftp
            : DestinationKind.LocalOrUnc;
    }

    /// <summary>
    /// Push <paramref name="localFullPath"/> to the configured remote root, keeping the same
    /// <paramref name="relativePath"/> beneath the root. Returns a populated <see cref="Result"/>;
    /// never throws (all exceptions are captured into <see cref="Result.Error"/>).
    /// </summary>
    public static async Task<Result> PushFileAsync(string localFullPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(localFullPath) || !File.Exists(localFullPath))
            return new Result { Success = false, Error = "Local file not found: " + (localFullPath ?? "<null>") };
        if (string.IsNullOrWhiteSpace(relativePath))
            return new Result { Success = false, Error = "Relative path is empty; cannot determine remote location." };

        var root = RemoteRoot;
        if (string.IsNullOrWhiteSpace(root))
            return new Result { Success = false, Error = "StaffDocumentsRemoteRoot is not configured in App.config." };

        try
        {
            return GetKind() switch
            {
                DestinationKind.Sftp => await PushViaSftpAsync(root!, localFullPath, relativePath).ConfigureAwait(false),
                DestinationKind.LocalOrUnc => await PushToLocalOrUncAsync(root!, localFullPath, relativePath).ConfigureAwait(false),
                _ => new Result { Success = false, Error = "Remote destination kind could not be resolved." }
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[StaffDocumentsRemoteSync] PushFileAsync failed: " + ex);
            return new Result { Success = false, Error = ex.Message };
        }
    }

    private static async Task<Result> PushToLocalOrUncAsync(string root, string localFullPath, string relativePath)
    {
        var normalised = NormaliseRelativeForLocal(relativePath);
        var destFull = Path.Combine(root, normalised);
        var destDir = Path.GetDirectoryName(destFull);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        const int bufferSize = 81920;
        await using (var src = new FileStream(localFullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true))
        await using (var dst = new FileStream(destFull, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, useAsync: true))
        {
            await src.CopyToAsync(dst).ConfigureAwait(false);
            await dst.FlushAsync().ConfigureAwait(false);
        }

        return new Result { Success = true, RemoteFullPath = destFull };
    }

    private static Task<Result> PushViaSftpAsync(string root, string localFullPath, string relativePath)
    {
        // SSH.NET is synchronous in its connect/upload primitives; run the whole thing on a worker thread
        // so the caller's async/await contract is preserved and the UI thread is never blocked.
        return Task.Run(() =>
        {
            if (!TryParseSftpEndpoint(root, out var host, out var port, out var remoteBasePath, out var parseError))
                return new Result { Success = false, Error = parseError };

            if (!TryReadSftpCredentials(out var user, out var password, out var credError))
                return new Result { Success = false, Error = credError };

            var remoteRelative = NormaliseRelativeForPosix(relativePath);
            var remoteFull = JoinPosix(remoteBasePath, remoteRelative);

            try
            {
                using var client = new SftpClient(host!, port, user!, password ?? string.Empty);
                client.Connect();
                EnsureRemoteDirectoryExists(client, GetPosixDirectory(remoteFull));

                using var fs = new FileStream(localFullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                client.UploadFile(fs, remoteFull, canOverride: true);
                client.Disconnect();

                return new Result { Success = true, RemoteFullPath = remoteFull };
            }
            catch (SshException ex)
            {
                return new Result { Success = false, Error = "SFTP error: " + ex.Message };
            }
            catch (Exception ex)
            {
                return new Result { Success = false, Error = ex.Message };
            }
        });
    }

    /// <summary>
    /// Parse the SFTP URI into host, port and remote base path. Credentials are NOT read from the
    /// URI — they live in separate, encrypted App.config keys (see <see cref="TryReadSftpCredentials"/>).
    /// If the URI includes a <c>user[:password]@</c> segment it is ignored with a debug warning.
    /// </summary>
    private static bool TryParseSftpEndpoint(
        string root,
        out string? host,
        out int port,
        out string remoteBasePath,
        out string? error)
    {
        host = null;
        port = 22;
        remoteBasePath = "/";
        error = null;

        try
        {
            if (!Uri.TryCreate(root.Trim(), UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, "sftp", StringComparison.OrdinalIgnoreCase))
            {
                error = "StaffDocumentsRemoteRoot is not a valid sftp:// URI.";
                return false;
            }

            host = uri.Host;
            if (string.IsNullOrEmpty(host))
            {
                error = "SFTP URI is missing a host.";
                return false;
            }

            port = uri.IsDefaultPort ? 22 : uri.Port;

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                Debug.WriteLine("[StaffDocumentsRemoteSync] Ignoring credentials embedded in " +
                                "StaffDocumentsRemoteRoot; use the encrypted " + ConfigKeySftpUser +
                                " / " + ConfigKeySftpPassword + " keys instead.");
            }

            remoteBasePath = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;
            return true;
        }
        catch (Exception ex)
        {
            error = "Failed to parse SFTP URI: " + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Read the encrypted SFTP credentials from App.config and decrypt them via
    /// <see cref="App"/><c>.aps.crypt</c>. Returns <c>false</c> with a descriptive <paramref name="error"/>
    /// when either key is missing or decryption produces an empty string.
    /// </summary>
    private static bool TryReadSftpCredentials(out string? user, out string? password, out string? error)
    {
        user = null;
        password = null;
        error = null;

        var encryptedUser = ConfigurationManager.AppSettings[ConfigKeySftpUser];
        var encryptedPassword = ConfigurationManager.AppSettings[ConfigKeySftpPassword];

        if (string.IsNullOrWhiteSpace(encryptedUser) || string.IsNullOrWhiteSpace(encryptedPassword))
        {
            error = "SFTP credentials are not configured. Set encrypted values for " +
                    ConfigKeySftpUser + " and " + ConfigKeySftpPassword + " in App.config " +
                    "(use StaffDocumentsRemoteSync.EncryptForConfig to generate the ciphertext).";
            return false;
        }

        try
        {
            user = App.aps.crypt.DoDecrypt(encryptedUser!);
            password = App.aps.crypt.DoDecrypt(encryptedPassword!);
        }
        catch (Exception ex)
        {
            error = "Failed to decrypt SFTP credentials: " + ex.Message;
            return false;
        }

        if (string.IsNullOrEmpty(user))
        {
            error = "Decrypted SFTP username is empty — the value in " + ConfigKeySftpUser + " is not valid ciphertext.";
            return false;
        }

        return true;
    }

    private static void EnsureRemoteDirectoryExists(SftpClient client, string remoteDir)
    {
        if (string.IsNullOrEmpty(remoteDir) || remoteDir == "/" || remoteDir == ".")
            return;

        // Walk top-down so every parent exists before the leaf is created.
        var parts = remoteDir.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var cumulative = remoteDir.StartsWith('/') ? string.Empty : ".";
        foreach (var part in parts)
        {
            cumulative = cumulative + "/" + part;
            try
            {
                if (!client.Exists(cumulative))
                    client.CreateDirectory(cumulative);
            }
            catch (SftpPathNotFoundException)
            {
                client.CreateDirectory(cumulative);
            }
        }
    }

    private static string NormaliseRelativeForLocal(string relativePath) =>
        relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);

    private static string NormaliseRelativeForPosix(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');

    private static string JoinPosix(string basePath, string relative)
    {
        var b = (basePath ?? "/").TrimEnd('/');
        if (string.IsNullOrEmpty(b))
            b = "/";
        return b + "/" + relative;
    }

    private static string GetPosixDirectory(string fullPosixPath)
    {
        var ix = fullPosixPath.LastIndexOf('/');
        return ix <= 0 ? "/" : fullPosixPath.Substring(0, ix);
    }
}
