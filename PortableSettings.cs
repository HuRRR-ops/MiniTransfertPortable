using System.Globalization;
using System.Text;

namespace MiniTransfertPortable;

internal static class PortableSettings
{
    public const int DefaultPort = 55750;
    public const int MinimumPort = 1024;
    public const int MaximumPort = 65535;
    private const string FileName = "MiniTransfertPortable.ini";

    public static string SettingsPath => Path.Combine(AppContext.BaseDirectory, FileName);

    public static int LoadPort(string? settingsPath = null)
    {
        settingsPath ??= SettingsPath;
        try
        {
            if (!File.Exists(settingsPath))
            {
                return DefaultPort;
            }

            foreach (var rawLine in File.ReadLines(settingsPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
                {
                    continue;
                }

                var separator = line.IndexOf('=');
                if (separator < 0 || !line[..separator].Trim().Equals("Port", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (int.TryParse(line[(separator + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var port)
                    && IsValidPort(port))
                {
                    return port;
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

        return DefaultPort;
    }

    public static bool TrySavePort(int port, out string? error, string? settingsPath = null)
    {
        if (!IsValidPort(port))
        {
            error = $"Le port doit être compris entre {MinimumPort} et {MaximumPort}.";
            return false;
        }

        try
        {
            settingsPath ??= SettingsPath;
            var contents = "; MiniTransfert Portable\r\n" +
                           "; Configuration locale portable\r\n" +
                           $"Port={port.ToString(CultureInfo.InvariantCulture)}\r\n";
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

    public static bool IsValidPort(int port) => port is >= MinimumPort and <= MaximumPort;
}
