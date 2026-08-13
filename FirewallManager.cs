using System.Diagnostics;

namespace MiniTransfertPortable;

internal static class FirewallManager
{
    private const string RuleName = "MiniTransfert Portable (TCP 55750)";

    public static bool EnsureInboundRule(IWin32Window owner)
    {
        if (RuleExists())
        {
            return true;
        }

        var answer = RetroMessageBox.Show(
            owner,
            "Windows doit autoriser les connexions entrantes sur le port TCP 55750.\n\n" +
            "Une confirmation administrateur sera demandée une seule fois.",
            "Autorisation du pare-feu",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Information);
        if (answer != DialogResult.OK)
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "netsh.exe"),
                Arguments = $"advfirewall firewall add rule name=\"{RuleName}\" dir=in action=allow protocol=TCP localport=55750 profile=any",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            process?.WaitForExit();
            return process?.ExitCode == 0 && RuleExists();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static bool RuleExists()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"if (Get-NetFirewallRule -DisplayName '{RuleName}' -ErrorAction SilentlyContinue) {{ exit 0 }} else {{ exit 1 }}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit(5000);
            return process?.HasExited == true && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
