namespace MiniTransfertPortable;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        if (args.Length == 1 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = await SelfTest.RunAsync();
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
