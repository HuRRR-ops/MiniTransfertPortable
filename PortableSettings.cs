using System.Globalization;
using System.Text;

namespace MiniTransfertPortable;

internal sealed record PortableConfiguration(int Port, string? PublicHost);

internal static class PortableSettings
{
    public const int DefaultPort = 55750;
    public const int MinimumPort = 1024;
    public const int MaximumPort = 65535;
    private const string FileName = "MiniTransfertPortable.ini";

    public static string SettingsPath => Path.Combine(AppContext.BaseDirectory, FileName);

    public static PortableConfiguration Load(string? settingsPath = null)
    {
        settingsPath ??= SettingsPath;
        var port = DefaultPort;
        string? publicHost = null;

        try
        {
            if (!File.Exists(settingsPath))
            {
                return new PortableConfiguration(port, publicHost);
            }

            foreach (var rawLine in File.ReadLines(settingsPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator < 0)
                {
                    continue;
                }

                var name = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                if (name.Equals("Port", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var configuredPort)
                    && IsValidPort(configuredPort))
                {
                    port = configuredPort;
                }
                else if (name.Equals("PublicHost", StringComparison.OrdinalIgnoreCase)
                         && TryNormalizePublicHost(value, out var configuredHost))
                {
                    publicHost = configuredHost;
                }
            }
        }
        catch (IOException)
        {
            // Une configuration illisible ne doit jamais empêcher le démarrage.
        }
        catch (UnauthorizedAccessException)
        {
            // La valeur par défaut reste utilisable dans un dossier protégé.
        }

        return new PortableConfiguration(port, publicHost);
    }

    public static bool TrySave(PortableConfiguration configuration, out string? error, string? settingsPath = null)
    {
        if (!IsValidPort(configuration.Port))
        {
            error = $"Le port doit être compris entre {MinimumPort} et {MaximumPort}.";
            return false;
        }
        if (!TryNormalizePublicHost(configuration.PublicHost, out var publicHost))
        {
            error = "Le nom d’hôte DDNS n’est pas valide.";
            return false;
        }

        try
        {
            settingsPath ??= SettingsPath;
            var contents = "; MiniTransfert Portable\r\n" +
                           "; Configuration locale portable\r\n" +
                           $"Port={configuration.Port.ToString(CultureInfo.InvariantCulture)}\r\n" +
                           $"PublicHost={publicHost}\r\n";
            File.WriteAllText(settingsPath, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            error = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = "Impossible d’enregistrer la configuration à côté de l’application.\r\n\r\n" + exception.Message;
            return false;
        }
    }

    public static bool TryNormalizePublicHost(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var candidate = value.Trim().TrimEnd('.');
        if (candidate.Length is 0 or > 253
            || candidate.Contains("://", StringComparison.Ordinal)
            || candidate.IndexOfAny(['/', '\\', ':', '?', '#']) >= 0)
        {
            return false;
        }

        try
        {
            candidate = new IdnMapping().GetAscii(candidate).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (Uri.CheckHostName(candidate) != UriHostNameType.Dns)
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    public static bool IsValidPort(int port) => port is >= MinimumPort and <= MaximumPort;
}
