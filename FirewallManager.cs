using System.Diagnostics;

namespace MiniTransfertPortable;

internal static class FirewallManager
{
    public static bool EnsureInboundRule(IWin32Window owner, int port)
    {
        var ruleName = RuleName(port);
        if (RuleExists(ruleName))
        {
            return true;
        }

        var answer = RetroMessageBox.Show(
            owner,
            $"Windows doit autoriser les connexions entrantes sur le port TCP {port}.\n\n" +
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
                Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port} profile=any",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            process?.WaitForExit();
            return process?.ExitCode == 0 && RuleExists(ruleName);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static string RuleName(int port) => $"MiniTransfert Portable (TCP {port})";

    private static bool RuleExists(string ruleName)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"if (Get-NetFirewallRule -DisplayName '{ruleName}' -ErrorAction SilentlyContinue) {{ exit 0 }} else {{ exit 1 }}\"",
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
