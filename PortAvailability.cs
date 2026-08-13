using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MiniTransfertPortable;

internal static class PortAvailability
{
    public static bool IsAvailable(int port, out string message)
    {
        try
        {
            if (IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Any(endpoint => endpoint.Port == port))
            {
                message = $"Le port TCP {port} est déjà utilisé ou réservé.";
                return false;
            }
        }
        catch (NetworkInformationException)
        {
            // Le test de réservation ci-dessous reste une seconde vérification fiable.
        }

        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(IPAddress.Any, port)
            {
                ExclusiveAddressUse = true
            };
            listener.Start();
            message = $"Le port TCP {port} est disponible sur cet ordinateur.";
            return true;
        }
        catch (SocketException)
        {
            message = $"Le port TCP {port} est déjà utilisé ou réservé.";
            return false;
        }
        finally
        {
            listener?.Stop();
        }
    }
}
