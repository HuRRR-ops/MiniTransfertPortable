using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace MiniTransfertPortable;

internal static class SelfTest
{
    public static async Task<int> RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "MiniTransfertPortable-SelfTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await TestSingleFileAndResumeAsync(root, 55851);
            await TestProtectedFolderAsync(root, 55852);
            await TestRestartAndExpirationAsync(root, 55853);
            TestPortConfiguration(root);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // A self-test cleanup failure does not invalidate transfer behavior.
            }
        }
    }

    private static async Task TestSingleFileAndResumeAsync(string root, int port)
    {
        var bytes = new byte[1024 * 1024 + 321];
        Random.Shared.NextBytes(bytes);
        var path = Path.Combine(root, "gros fichier.bin");
        await File.WriteAllBytesAsync(path, bytes);
        var share = ShareDescriptor.Create(path, isFolder: false, password: null, TimeSpan.FromMinutes(5));
        await using var server = new ShareServer(port);
        server.Log += Console.Error.WriteLine;
        await server.StartAsync(share);
        Require(!PortAvailability.IsAvailable(port, out _), "Le test de disponibilité ne détecte pas un port occupé.");
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

        using var landingResponse = await client.GetAsync($"/d/{share.Token}");
        var landing = await landingResponse.Content.ReadAsStringAsync();
        Require(landingResponse.IsSuccessStatusCode, $"La page du fichier retourne {(int)landingResponse.StatusCode}: {landing}");
        Require(landing.Contains("gros fichier.bin", StringComparison.Ordinal), "La page du fichier ne contient pas son nom.");

        using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, $"/d/{share.Token}/download");
        rangeRequest.Headers.Range = new RangeHeaderValue(1234, 54321);
        using var rangeResponse = await client.SendAsync(rangeRequest);
        Require(rangeResponse.StatusCode == HttpStatusCode.PartialContent, "La reprise HTTP n'a pas retourné 206.");
        var received = await rangeResponse.Content.ReadAsByteArrayAsync();
        var expected = bytes[1234..54322];
        Require(received.SequenceEqual(expected), "Les octets repris ne correspondent pas à la source.");

        using var headRequest = new HttpRequestMessage(HttpMethod.Head, $"/d/{share.Token}/download");
        using var headResponse = await client.SendAsync(headRequest);
        Require(headResponse.StatusCode == HttpStatusCode.OK, "HEAD n'a pas retourné 200.");
        Require(headResponse.Content.Headers.ContentLength == bytes.Length, "HEAD annonce une mauvaise taille.");

        using var missing = await client.GetAsync("/d/jeton-invalide");
        Require(missing.StatusCode == HttpStatusCode.NotFound, "Un jeton invalide n'est pas refusé en 404.");
    }

    private static async Task TestProtectedFolderAsync(string root, int port)
    {
        var folder = Path.Combine(root, "Dossier privé");
        var child = Path.Combine(folder, "Sous-dossier");
        Directory.CreateDirectory(child);
        var content = Encoding.UTF8.GetBytes("MiniTransfert protégé");
        await File.WriteAllBytesAsync(Path.Combine(child, "note.txt"), content);
        var share = ShareDescriptor.Create(folder, isFolder: true, password: "secret-test", TimeSpan.FromMinutes(5));
        await using var server = new ShareServer(port);
        server.Log += Console.Error.WriteLine;
        await server.StartAsync(share);

        var cookies = new CookieContainer();
        using var handler = new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var protectedPage = await client.GetStringAsync($"/d/{share.Token}");
        Require(protectedPage.Contains("Mot de passe", StringComparison.Ordinal), "Le formulaire de mot de passe est absent.");

        using var unauthenticatedDownload = await client.GetAsync($"/d/{share.Token}/file/1");
        Require(unauthenticatedDownload.StatusCode == HttpStatusCode.Redirect, "Un fichier protégé est accessible sans mot de passe.");

        using var wrongForm = new FormUrlEncodedContent(new Dictionary<string, string> { ["password"] = "incorrect" });
        using var wrongAuthentication = await client.PostAsync($"/d/{share.Token}/auth", wrongForm);
        var wrongPasswordPage = await wrongAuthentication.Content.ReadAsStringAsync();
        Require(wrongAuthentication.StatusCode == HttpStatusCode.OK, "Le refus d'un mauvais mot de passe n'a pas réaffiché le formulaire.");
        Require(wrongPasswordPage.Contains("incorrect", StringComparison.OrdinalIgnoreCase), "Le formulaire ne signale pas le mauvais mot de passe.");
        using var stillProtected = await client.GetAsync($"/d/{share.Token}/file/1");
        Require(stillProtected.StatusCode == HttpStatusCode.Redirect, "Un mauvais mot de passe a autorisé le téléchargement.");

        using var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["password"] = "secret-test" });
        using var authentication = await client.PostAsync($"/d/{share.Token}/auth", form);
        Require(authentication.StatusCode == HttpStatusCode.Redirect, "L'authentification correcte n'a pas redirigé.");

        using var download = await client.GetAsync($"/d/{share.Token}/file/1");
        Require(download.StatusCode == HttpStatusCode.OK, "Le fichier protégé reste inaccessible après authentification.");
        Require((await download.Content.ReadAsByteArrayAsync()).SequenceEqual(content), "Le fichier du dossier a été altéré.");
    }

    private static async Task TestRestartAndExpirationAsync(string root, int port)
    {
        var path = Path.Combine(root, "été à Montréal — fichier avec espaces.txt");
        var content = Encoding.UTF8.GetBytes("accents, espaces et redémarrage");
        await File.WriteAllBytesAsync(path, content);
        var share = ShareDescriptor.Create(path, isFolder: false, password: string.Empty, lifetime: null);
        Require(!share.RequiresPassword, "Un mot de passe vide ne doit pas protéger le partage.");
        Require(!share.IsExpired, "Un partage sans expiration est indiqué comme expiré.");

        await DownloadAndVerifyAsync(share, port, content);
        await DownloadAndVerifyAsync(share, port, content);

        var expired = ShareDescriptor.Create(path, isFolder: false, password: null, lifetime: TimeSpan.FromSeconds(-1));
        await using var server = new ShareServer(port);
        server.Log += Console.Error.WriteLine;
        await server.StartAsync(expired);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        using var response = await client.GetAsync($"/d/{expired.Token}");
        Require(response.StatusCode == HttpStatusCode.Gone, "Un partage expiré n'est pas refusé en 410.");
    }

    private static async Task DownloadAndVerifyAsync(ShareDescriptor share, int port, byte[] expected)
    {
        await using var server = new ShareServer(port);
        server.Log += Console.Error.WriteLine;
        await server.StartAsync(share);
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        using var response = await client.GetAsync($"/d/{share.Token}/download");
        Require(response.StatusCode == HttpStatusCode.OK, "Le téléchargement après démarrage/redémarrage a échoué.");
        Require((await response.Content.ReadAsByteArrayAsync()).SequenceEqual(expected), "Le fichier accentué a été altéré.");
    }

    private static void TestPortConfiguration(string root)
    {
        Require(PortableSettings.IsValidPort(PortableSettings.MinimumPort), "Le port minimal valide est refusé.");
        Require(PortableSettings.IsValidPort(PortableSettings.MaximumPort), "Le port maximal valide est refusé.");
        Require(!PortableSettings.IsValidPort(PortableSettings.MinimumPort - 1), "Un port trop petit est accepté.");
        Require(!PortableSettings.IsValidPort(PortableSettings.MaximumPort + 1), "Un port trop grand est accepté.");
        Require(PortAvailability.IsAvailable(55854, out _), "Le test de disponibilité refuse un port libre.");

        var settingsPath = Path.Combine(root, "MiniTransfertPortable.ini");
        var saved = new PortableConfiguration(61234, "DirectFiles.DDNS.net.");
        Require(PortableSettings.TrySave(saved, out var error, settingsPath), $"La sauvegarde de la configuration a échoué : {error}");
        var loaded = PortableSettings.Load(settingsPath);
        Require(loaded.Port == 61234, "Le port sauvegardé n'est pas rechargé.");
        Require(loaded.PublicHost == "directfiles.ddns.net", "Le nom d'hôte DDNS n'est pas normalisé ou rechargé.");

        Require(PortableSettings.TryNormalizePublicHost("directfiles.ddns.net", out var validHost)
                && validHost == "directfiles.ddns.net",
            "Un nom d'hôte DDNS valide est refusé.");
        Require(!PortableSettings.TryNormalizePublicHost("http://directfiles.ddns.net:55750/test", out _),
            "Une URL complète est acceptée comme nom d'hôte.");

        File.WriteAllText(settingsPath, "Port=70000\r\nPublicHost=http://invalide/");
        loaded = PortableSettings.Load(settingsPath);
        Require(loaded.Port == PortableSettings.DefaultPort, "Un port invalide n'est pas remplacé par le port par défaut.");
        Require(loaded.PublicHost is null, "Un nom d'hôte invalide n'est pas ignoré.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
