namespace MiniTransfertPortable;

internal static class FilePickerService
{
    public static Task<string?> ChooseFileAsync()
    {
        return RunOnStaThread(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Choisir un fichier à partager",
                CheckFileExists = true,
                CheckPathExists = true,
                DereferenceLinks = true,
                RestoreDirectory = true,
                AutoUpgradeEnabled = false,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };
            return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
        });
    }

    public static Task<string?> ChooseFolderAsync()
    {
        return RunOnStaThread(() =>
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choisir un dossier à partager",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
                AutoUpgradeEnabled = false,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };
            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
        });
    }

    private static Task<string?> RunOnStaThread(Func<string?> action)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(action());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "MiniTransfert-FilePicker"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
