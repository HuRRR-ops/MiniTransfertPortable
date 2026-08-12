using System.Runtime.InteropServices;

namespace MiniTransfertPortable;

internal static class ClipboardService
{
    public static bool TrySetText(string text, out string? error)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        Clipboard.SetText(text);
                        failure = null;
                        return;
                    }
                    catch (ExternalException exception)
                    {
                        failure = exception;
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        })
        {
            IsBackground = true,
            Name = "MiniTransfert-Clipboard"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!thread.Join(TimeSpan.FromSeconds(3)))
        {
            error = "Le presse-papiers ne répond pas.";
            return false;
        }

        error = failure?.Message;
        return failure is null;
    }
}
