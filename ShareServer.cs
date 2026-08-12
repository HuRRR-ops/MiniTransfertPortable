using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace MiniTransfertPortable;

internal sealed class ShareServer : IAsyncDisposable
{
    private const string AuthCookieName = "mtp_auth";
    private readonly int _port;
    private WebApplication? _application;
    private ShareDescriptor? _share;
    private long _bytesSent;
    private int _activeDownloads;
    private int _completedDownloads;
    private long _lastActivityTicks;

    public ShareServer(int port = 55750)
    {
        _port = port;
    }

    public event Action<string>? Log;

    public TransferSnapshot Snapshot => new(
        Interlocked.Read(ref _bytesSent),
        Volatile.Read(ref _activeDownloads),
        Volatile.Read(ref _completedDownloads),
        Interlocked.Read(ref _lastActivityTicks) == 0
            ? null
            : new DateTimeOffset(Interlocked.Read(ref _lastActivityTicks), TimeSpan.Zero));

    public async Task StartAsync(ShareDescriptor share, CancellationToken cancellationToken = default)
    {
        if (_application is not null)
        {
            throw new InvalidOperationException("Le serveur est déjà démarré.");
        }

        _share = share;
        Interlocked.Exchange(ref _bytesSent, 0);
        Interlocked.Exchange(ref _activeDownloads, 0);
        Interlocked.Exchange(ref _completedDownloads, 0);
        Interlocked.Exchange(ref _lastActivityTicks, 0);

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ShareServer).Assembly.FullName,
            Args = []
        });
        builder.Logging.ClearProviders();
        if (Environment.GetEnvironmentVariable("MTP_DEBUG") == "1")
        {
            builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
        }
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = 8 * 1024;
            options.ListenAnyIP(_port);
        });

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception exception)
            {
                WriteLog($"Erreur serveur : {exception.GetType().Name} — {exception.Message}");
                if (!context.Response.HasStarted)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "text/plain; charset=utf-8";
                    await context.Response.WriteAsync("Erreur interne du serveur.");
                }
            }
        });
        app.Use(async (context, next) =>
        {
            context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers[HeaderNames.XFrameOptions] = "DENY";
            context.Response.Headers[HeaderNames.CacheControl] = "no-store";
            await next();
        });

        app.MapGet("/", context =>
        {
            context.Response.Redirect($"/d/{share.Token}");
            return Task.CompletedTask;
        });
        app.MapMethods("/d/{token}", [HttpMethods.Get, HttpMethods.Head], LandingAsync);
        app.MapPost("/d/{token}/auth", AuthenticateAsync);
        app.MapMethods("/d/{token}/download", [HttpMethods.Get, HttpMethods.Head], DownloadSingleAsync);
        app.MapMethods("/d/{token}/file/{id:int}", [HttpMethods.Get, HttpMethods.Head], DownloadFolderFileAsync);
        app.MapFallback(NotFoundAsync);

        _application = app;
        try
        {
            await app.StartAsync(cancellationToken);
        }
        catch
        {
            _application = null;
            await app.DisposeAsync();
            throw;
        }

        WriteLog($"Serveur actif sur le port {_port}.");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var app = Interlocked.Exchange(ref _application, null);
        if (app is null)
        {
            return;
        }

        try
        {
            await app.StopAsync(cancellationToken);
        }
        finally
        {
            await app.DisposeAsync();
            WriteLog("Partage arrêté.");
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task LandingAsync(HttpContext context)
    {
        var share = ValidateShare(context);
        if (share is null)
        {
            await WriteUnavailableShareAsync(context);
            return;
        }

        if (!IsAuthenticated(context, share))
        {
            await WritePasswordPageAsync(context, share, invalid: false);
            return;
        }

        var title = Html(share.DisplayName);
        var expiry = share.ExpiresAt is null
            ? "Sans expiration"
            : $"Disponible jusqu’au {share.ExpiresAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}";

        var body = new StringBuilder();
        body.Append("<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<title>").Append(title).Append(" — MiniTransfert</title>")
            .Append("<style>")
            .Append("body{font-family:Arial,sans-serif;background:#ddd;margin:0;color:#111}")
            .Append("main{max-width:760px;margin:40px auto;background:#f4f4f4;border:1px solid #777;box-shadow:2px 2px 8px #777}")
            .Append("h1{font-size:21px;margin:0;padding:12px 16px;background:#064b94;color:#fff}")
            .Append("section{padding:18px} .meta{color:#555;margin:6px 0 18px}")
            .Append("a.button,button{display:inline-block;background:#eee;color:#111;border:2px outset #ddd;padding:9px 18px;text-decoration:none;font-weight:bold}")
            .Append("a.button:active,button:active{border-style:inset}")
            .Append("table{width:100%;border-collapse:collapse;background:white}th,td{text-align:left;padding:8px;border:1px solid #aaa}th{background:#ddd}")
            .Append("td.size{white-space:nowrap;text-align:right}small{display:block;color:#666;margin-top:18px}")
            .Append("</style></head><body><main><h1>MiniTransfert Portable</h1><section>")
            .Append("<h2>").Append(title).Append("</h2><div class=\"meta\">")
            .Append(share.IsFolder ? $"{share.Files.Count:N0} fichier(s) — {FormatBytes(share.TotalBytes)}" : FormatBytes(share.TotalBytes))
            .Append("<br>").Append(Html(expiry)).Append("</div>");

        if (share.IsFolder)
        {
            if (share.Files.Count == 0)
            {
                body.Append("<p>Ce dossier ne contient aucun fichier accessible.</p>");
            }
            else
            {
                body.Append("<table><thead><tr><th>Nom</th><th>Taille</th><th></th></tr></thead><tbody>");
                foreach (var file in share.Files)
                {
                    body.Append("<tr><td>").Append(Html(file.DisplayName)).Append("</td><td class=\"size\">")
                        .Append(FormatBytes(file.Length)).Append("</td><td><a href=\"/d/")
                        .Append(share.Token).Append("/file/").Append(file.Id.ToString(CultureInfo.InvariantCulture))
                        .Append("\">Télécharger</a></td></tr>");
                }
                body.Append("</tbody></table>");
            }
        }
        else
        {
            body.Append("<a class=\"button\" href=\"/d/").Append(share.Token)
                .Append("/download\">Télécharger le fichier</a>");
        }

        body.Append("<small>Le transfert peut être repris si la connexion est interrompue.</small>")
            .Append("</section></main></body></html>");
        await WriteHtmlAsync(context, body.ToString());
    }

    private async Task AuthenticateAsync(HttpContext context)
    {
        var share = ValidateShare(context);
        if (share is null)
        {
            await WriteUnavailableShareAsync(context);
            return;
        }

        if (!share.RequiresPassword)
        {
            context.Response.Redirect($"/d/{share.Token}");
            return;
        }

        if (context.Request.ContentLength is > 8192)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var password = form["password"].ToString();
        if (!share.VerifyPassword(password))
        {
            WriteLog($"Mot de passe refusé — {RemoteAddress(context)}.");
            await WritePasswordPageAsync(context, share, invalid: true);
            return;
        }

        context.Response.Cookies.Append(AuthCookieName, share.AuthenticationCookie, new CookieOptions
        {
            HttpOnly = true,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
            Path = $"/d/{share.Token}",
            Expires = share.ExpiresAt,
            IsEssential = true
        });
        WriteLog($"Accès autorisé — {RemoteAddress(context)}.");
        context.Response.Redirect($"/d/{share.Token}");
    }

    private Task DownloadSingleAsync(HttpContext context)
    {
        var share = ValidateShare(context);
        if (share is null)
        {
            return WriteUnavailableShareAsync(context);
        }
        if (!RequireAuthentication(context, share))
        {
            return Task.CompletedTask;
        }

        if (share.IsFolder || share.Files.Count != 1)
        {
            return NotFoundAsync(context);
        }

        return SendFileAsync(context, share.Files[0]);
    }

    private Task DownloadFolderFileAsync(HttpContext context)
    {
        var share = ValidateShare(context);
        if (share is null)
        {
            return WriteUnavailableShareAsync(context);
        }
        if (!RequireAuthentication(context, share))
        {
            return Task.CompletedTask;
        }

        if (!share.IsFolder || !int.TryParse(context.Request.RouteValues["id"]?.ToString(), out var id))
        {
            return NotFoundAsync(context);
        }

        var file = share.Files.FirstOrDefault(candidate => candidate.Id == id);
        return file is null ? NotFoundAsync(context) : SendFileAsync(context, file);
    }

    private async Task SendFileAsync(HttpContext context, SharedFile sharedFile)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(sharedFile.FullPath, new FileStreamOptions
            {
                Access = FileAccess.Read,
                Mode = FileMode.Open,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                BufferSize = 128 * 1024
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("Le fichier n'est plus accessible.");
            WriteLog($"Fichier inaccessible : {sharedFile.DisplayName}");
            return;
        }

        await using (stream)
        {
            var length = stream.Length;
            if (!TryParseRange(context.Request.Headers.Range, length, out var start, out var end))
            {
                context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                context.Response.Headers[HeaderNames.ContentRange] = $"bytes */{length}";
                return;
            }

            var isPartial = length > 0 && (start != 0 || end != length - 1);
            var count = length == 0 ? 0 : end - start + 1;
            context.Response.StatusCode = isPartial ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK;
            context.Response.ContentType = ContentTypeFor(sharedFile.DisplayName);
            context.Response.ContentLength = count;
            context.Response.Headers[HeaderNames.AcceptRanges] = "bytes";
            context.Response.Headers[HeaderNames.ContentDisposition] = ContentDisposition(sharedFile.DisplayName);
            if (isPartial)
            {
                context.Response.Headers[HeaderNames.ContentRange] = $"bytes {start}-{end}/{length}";
            }

            if (HttpMethods.IsHead(context.Request.Method) || count == 0)
            {
                return;
            }

            stream.Position = start;
            Interlocked.Increment(ref _activeDownloads);
            TouchActivity();
            WriteLog($"Téléchargement : {sharedFile.DisplayName} — {RemoteAddress(context)}{(isPartial ? " (reprise)" : string.Empty)}");
            var remaining = count;
            var buffer = new byte[128 * 1024];
            var completed = false;

            try
            {
                while (remaining > 0)
                {
                    var wanted = (int)Math.Min(buffer.Length, remaining);
                    var read = await stream.ReadAsync(buffer.AsMemory(0, wanted), context.RequestAborted);
                    if (read == 0)
                    {
                        break;
                    }

                    await context.Response.Body.WriteAsync(buffer.AsMemory(0, read), context.RequestAborted);
                    remaining -= read;
                    Interlocked.Add(ref _bytesSent, read);
                    TouchActivity();
                }
                completed = remaining == 0;
            }
            catch (OperationCanceledException)
            {
                // Browser disconnects and paused downloads arrive here normally.
            }
            catch (IOException)
            {
                // A network interruption is expected to be resumable by the browser.
            }
            finally
            {
                Interlocked.Decrement(ref _activeDownloads);
                if (completed)
                {
                    Interlocked.Increment(ref _completedDownloads);
                    WriteLog($"Terminé : {sharedFile.DisplayName}.");
                }
                else
                {
                    WriteLog($"Interrompu : {sharedFile.DisplayName} — reprise possible.");
                }
            }
        }
    }

    private ShareDescriptor? ValidateShare(HttpContext context)
    {
        var share = _share;
        var token = context.Request.RouteValues["token"]?.ToString();
        if (share is null || !string.Equals(token, share.Token, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return null;
        }

        if (share.IsExpired)
        {
            context.Response.StatusCode = StatusCodes.Status410Gone;
            return null;
        }

        return share;
    }

    private static bool IsAuthenticated(HttpContext context, ShareDescriptor share)
    {
        if (!share.RequiresPassword)
        {
            return true;
        }

        return context.Request.Cookies.TryGetValue(AuthCookieName, out var cookie)
            && string.Equals(cookie, share.AuthenticationCookie, StringComparison.Ordinal);
    }

    private static bool RequireAuthentication(HttpContext context, ShareDescriptor share)
    {
        if (IsAuthenticated(context, share))
        {
            return true;
        }

        context.Response.Redirect($"/d/{share.Token}");
        return false;
    }

    private static async Task WritePasswordPageAsync(HttpContext context, ShareDescriptor share, bool invalid)
    {
        var message = invalid ? "<p style=\"color:#b00020\"><b>Mot de passe incorrect.</b></p>" : string.Empty;
        var html = "<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"
            + "<title>Accès protégé — MiniTransfert</title><style>body{font-family:Arial,sans-serif;background:#ddd}main{max-width:430px;margin:60px auto;background:#eee;border:1px solid #777;padding:20px}"
            + "input{box-sizing:border-box;width:100%;padding:8px;margin:8px 0 14px}button{padding:8px 18px}</style></head><body><main>"
            + $"<h2>{Html(share.DisplayName)}</h2><p>Ce partage est protégé.</p>{message}"
            + $"<form method=\"post\" action=\"/d/{share.Token}/auth\"><label>Mot de passe</label><input type=\"password\" name=\"password\" maxlength=\"256\" autofocus required><button type=\"submit\">Continuer</button></form>"
            + "</main></body></html>";
        await WriteHtmlAsync(context, html);
    }

    private static async Task WriteHtmlAsync(HttpContext context, string html)
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers[HeaderNames.ContentSecurityPolicy] = "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
        if (!HttpMethods.IsHead(context.Request.Method))
        {
            await context.Response.WriteAsync(html, Encoding.UTF8);
        }
    }

    private static Task NotFoundAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync("Lien introuvable.");
    }

    private static Task WriteUnavailableShareAsync(HttpContext context)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(
            context.Response.StatusCode == StatusCodes.Status410Gone
                ? "Ce lien a expiré."
                : "Lien introuvable.");
    }

    private static bool TryParseRange(string? header, long length, out long start, out long end)
    {
        start = 0;
        end = Math.Max(0, length - 1);
        if (string.IsNullOrWhiteSpace(header) || length == 0)
        {
            return true;
        }

        if (!header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase) || header.Contains(','))
        {
            return false;
        }

        var parts = header[6..].Split('-', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        if (parts[0].Length == 0)
        {
            if (!long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var suffixLength) || suffixLength <= 0)
            {
                return false;
            }
            suffixLength = Math.Min(suffixLength, length);
            start = length - suffixLength;
            end = length - 1;
            return true;
        }

        if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out start) || start < 0 || start >= length)
        {
            return false;
        }

        if (parts[1].Length == 0)
        {
            end = length - 1;
            return true;
        }

        if (!long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out end) || end < start)
        {
            return false;
        }

        end = Math.Min(end, length - 1);
        return true;
    }

    private static string ContentDisposition(string name)
    {
        var leaf = Path.GetFileName(name);
        var safeAscii = new string(leaf.Select(character =>
            character is >= ' ' and <= '~' && character is not '"' and not '\\' ? character : '_').ToArray());
        return $"attachment; filename=\"{safeAscii}\"; filename*=UTF-8''{Uri.EscapeDataString(leaf)}";
    }

    private static string ContentTypeFor(string name) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".txt" or ".log" => "text/plain; charset=utf-8",
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".zip" => "application/zip",
        ".7z" => "application/x-7z-compressed",
        _ => "application/octet-stream"
    };

    internal static string FormatBytes(long bytes)
    {
        string[] units = ["o", "Ko", "Mo", "Go", "To"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{value:N0} {units[unit]}" : $"{value:N1} {units[unit]}";
    }

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string RemoteAddress(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "adresse inconnue";

    private void TouchActivity() => Interlocked.Exchange(ref _lastActivityTicks, DateTimeOffset.UtcNow.Ticks);

    private void WriteLog(string message) => Log?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
}
