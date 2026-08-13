using System.Diagnostics;
using System.Drawing;

namespace MiniTransfertPortable;

internal sealed class MainForm : RetroChromeForm
{
    private const int Port = 55750;
    private readonly TextBox _sourceBox = new();
    private readonly TextBox _passwordBox = new();
    private readonly ComboBox _expirationBox = new();
    private readonly TextBox _linkBox = new();
    private readonly ListView _filesView = new();
    private readonly RetroProgressBar _progress = new();
    private readonly Label _percentLabel = new();
    private readonly Label _statsLabel = new();
    private readonly TextBox _logBox = new();
    private readonly RetroButton _fileButton = new();
    private readonly RetroButton _folderButton = new();
    private readonly RetroButton _startButton = new();
    private readonly RetroButton _stopButton = new();
    private readonly RetroButton _copyButton = new();
    private readonly RetroStatusBar _statusLabel = new();
    private readonly ImageList _fileIcons = RetroIcons.CreateSmallImageList();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    private ShareServer? _server;
    private ShareDescriptor? _share;
    private string? _sourcePath;
    private bool _sourceIsFolder;
    private long _previousBytes;
    private DateTime _previousSample = DateTime.UtcNow;
    private double _smoothedBytesPerSecond;
    private bool _closing;

    public MainForm()
        : base("MiniTransfert Portable")
    {
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 500);
        ClientSize = new Size(800, 560);
        BuildInterface();
        _timer.Tick += UpdateTransferDisplay;
        _timer.Start();
        FormClosing += MainForm_FormClosing;
    }

    private void BuildInterface()
    {
        var menu = new MenuStrip
        {
            Dock = DockStyle.Top,
            BackColor = RetroTheme.Face,
            ForeColor = Color.Black,
            Font = Font,
            GripStyle = ToolStripGripStyle.Hidden,
            Renderer = new RetroMenuRenderer(),
            Padding = new Padding(2, 0, 0, 0),
            AutoSize = false,
            Height = 22,
            ShowItemToolTips = false
        };
        var fileMenu = new ToolStripMenuItem("&Fichier");
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Choisir un &fichier...", null, (_, _) => _ = ChooseFileAsync())
        {
            ShortcutKeys = Keys.Control | Keys.O
        });
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("Choisir un &dossier...", null, (_, _) => _ = ChooseFolderAsync())
        {
            ShortcutKeys = Keys.Control | Keys.Shift | Keys.O
        });
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Quitter", null, (_, _) => Close())
        {
            ShortcutKeys = Keys.Alt | Keys.F4
        });
        var toolsMenu = new ToolStripMenuItem("&Outils");
        toolsMenu.DropDownItems.Add(new ToolStripMenuItem("&Copier le lien", null, (_, _) => CopyLink())
        {
            ShortcutKeys = Keys.Control | Keys.C
        });
        toolsMenu.DropDownItems.Add("Ouvrir le lien &local", null, (_, _) => OpenLocalLink());
        var helpMenu = new ToolStripMenuItem("&Aide");
        helpMenu.DropDownItems.Add("À &propos de MiniTransfert...", null, (_, _) =>
        {
            using var dialog = new AboutDialog();
            dialog.ShowDialog(this);
        });
        foreach (var dropDown in new[] { fileMenu, toolsMenu, helpMenu })
        {
            dropDown.DropDown.BackColor = RetroTheme.Face;
            dropDown.DropDown.ForeColor = RetroTheme.Text;
            dropDown.DropDown.Font = Font;
            dropDown.DropDown.Renderer = menu.Renderer;
        }
        menu.Items.AddRange([fileMenu, toolsMenu, helpMenu]);
        MainMenuStrip = menu;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = RetroTheme.Face,
            Padding = new Padding(5, 4, 5, 3),
            Margin = Padding.Empty,
            ColumnCount = 1,
            RowCount = 6
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 43));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 57));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        root.Controls.Add(BuildSelectionGroup(), 0, 0);
        root.Controls.Add(BuildLinkGroup(), 0, 1);
        root.Controls.Add(BuildFileList(), 0, 2);
        root.Controls.Add(BuildProgressGroup(), 0, 3);
        root.Controls.Add(BuildLogGroup(), 0, 4);
        root.Controls.Add(BuildButtons(), 0, 5);

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = RetroTheme.Face,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = 1,
            RowCount = 3
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        menu.Dock = DockStyle.Fill;
        _statusLabel.Dock = DockStyle.Fill;
        root.Dock = DockStyle.Fill;
        shell.Controls.Add(menu, 0, 0);
        shell.Controls.Add(root, 0, 1);
        shell.Controls.Add(_statusLabel, 0, 2);
        ContentPanel.Controls.Add(shell);
        AcceptButton = _startButton;
        Shown += (_, _) => _sourceBox.Focus();
    }

    private Control BuildSelectionGroup()
    {
        var group = CreateGroup("Élément à partager");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = RetroTheme.Face,
            ColumnCount = 4,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        layout.Controls.Add(CreateLabel("&Source :"), 0, 0);
        _sourceBox.ReadOnly = true;
        _sourceBox.Dock = DockStyle.Fill;
        _sourceBox.BackColor = RetroTheme.Window;
        _sourceBox.BorderStyle = BorderStyle.Fixed3D;
        _sourceBox.Margin = new Padding(3, 1, 3, 1);
        layout.Controls.Add(_sourceBox, 1, 0);
        _fileButton.Text = "&Fichier...";
        _fileButton.Size = new Size(76, 23);
        _fileButton.Margin = new Padding(2, 0, 2, 1);
        _fileButton.Click += (_, _) => _ = ChooseFileAsync();
        layout.Controls.Add(_fileButton, 2, 0);
        _folderButton.Text = "&Dossier...";
        _folderButton.Size = new Size(76, 23);
        _folderButton.Margin = new Padding(2, 0, 0, 1);
        _folderButton.Click += (_, _) => _ = ChooseFolderAsync();
        layout.Controls.Add(_folderButton, 3, 0);

        layout.Controls.Add(CreateLabel("&Mot de passe :"), 0, 1);
        _passwordBox.UseSystemPasswordChar = true;
        _passwordBox.Dock = DockStyle.Fill;
        _passwordBox.BackColor = RetroTheme.Window;
        _passwordBox.BorderStyle = BorderStyle.Fixed3D;
        _passwordBox.Margin = new Padding(3, 1, 3, 1);
        layout.Controls.Add(_passwordBox, 1, 1);
        var expirationLabel = CreateLabel("&Expiration :");
        expirationLabel.Anchor = AnchorStyles.Right;
        layout.Controls.Add(expirationLabel, 2, 1);
        _expirationBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _expirationBox.FlatStyle = FlatStyle.Standard;
        _expirationBox.BackColor = RetroTheme.Window;
        _expirationBox.Items.AddRange([
            new ExpirationChoice("1 heure", TimeSpan.FromHours(1)),
            new ExpirationChoice("24 heures", TimeSpan.FromHours(24)),
            new ExpirationChoice("7 jours", TimeSpan.FromDays(7)),
            new ExpirationChoice("Sans expiration", null)
        ]);
        _expirationBox.SelectedIndex = 1;
        _expirationBox.Width = 124;
        _expirationBox.Margin = new Padding(2, 1, 0, 1);
        layout.Controls.Add(_expirationBox, 3, 1);
        group.Controls.Add(layout);
        return group;
    }

    private Control BuildLinkGroup()
    {
        var group = CreateGroup("Lien de partage");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = RetroTheme.Face,
            ColumnCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.Controls.Add(CreateLabel("&Lien :"), 0, 0);
        _linkBox.ReadOnly = true;
        _linkBox.Dock = DockStyle.Fill;
        _linkBox.BackColor = RetroTheme.Window;
        _linkBox.BorderStyle = BorderStyle.Fixed3D;
        _linkBox.Margin = new Padding(3, 1, 3, 1);
        layout.Controls.Add(_linkBox, 1, 0);
        _copyButton.Text = "&Copier";
        _copyButton.Enabled = false;
        _copyButton.Size = new Size(70, 23);
        _copyButton.Margin = new Padding(2, 0, 0, 1);
        _copyButton.Click += (_, _) => CopyLink();
        layout.Controls.Add(_copyButton, 2, 0);
        group.Controls.Add(layout);
        return group;
    }

    private Control BuildFileList()
    {
        _filesView.Dock = DockStyle.Fill;
        _filesView.View = View.Details;
        _filesView.FullRowSelect = true;
        _filesView.GridLines = true;
        _filesView.HideSelection = false;
        _filesView.BorderStyle = BorderStyle.Fixed3D;
        _filesView.BackColor = RetroTheme.Window;
        _filesView.ForeColor = RetroTheme.Text;
        _filesView.SmallImageList = _fileIcons;
        _filesView.LabelWrap = false;
        _filesView.MultiSelect = false;
        _filesView.Margin = new Padding(0, 2, 0, 2);
        _filesView.Columns.Add("Nom", 470);
        _filesView.Columns.Add("Taille", 110, HorizontalAlignment.Right);
        _filesView.Columns.Add("État", 145);
        _filesView.Resize += (_, _) => ResizeFileColumns();
        return _filesView;
    }

    private Control BuildProgressGroup()
    {
        var group = CreateGroup("Progression");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = RetroTheme.Face,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _progress.Dock = DockStyle.Fill;
        _progress.Maximum = 1000;
        _progress.Height = 20;
        _progress.Margin = new Padding(0, 1, 4, 1);
        layout.Controls.Add(_progress, 0, 0);
        _percentLabel.Text = "0 %";
        _percentLabel.Font = RetroTheme.BoldFont;
        _percentLabel.BackColor = RetroTheme.Face;
        _percentLabel.AutoSize = true;
        _percentLabel.Anchor = AnchorStyles.Right;
        _percentLabel.Margin = new Padding(2, 1, 0, 1);
        layout.Controls.Add(_percentLabel, 1, 0);
        _statsLabel.Text = "En attente d’un téléchargement.";
        _statsLabel.BackColor = RetroTheme.Face;
        _statsLabel.AutoSize = true;
        _statsLabel.Margin = new Padding(0, 2, 0, 0);
        layout.SetColumnSpan(_statsLabel, 2);
        layout.Controls.Add(_statsLabel, 0, 1);
        group.Controls.Add(layout);
        return group;
    }

    private Control BuildLogGroup()
    {
        var group = CreateGroup("Journal");
        group.Dock = DockStyle.Fill;
        _logBox.Dock = DockStyle.Fill;
        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Font = RetroTheme.LogFont;
        _logBox.BackColor = RetroTheme.Window;
        _logBox.BorderStyle = BorderStyle.Fixed3D;
        group.Controls.Add(_logBox);
        return group;
    }

    private Control BuildButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = RetroTheme.Face,
            Padding = new Padding(0, 2, 0, 0),
            Margin = Padding.Empty,
            MinimumSize = new Size(0, 29),
            WrapContents = false
        };
        _startButton.Text = "&Démarrer le partage";
        _startButton.Size = new Size(150, 25);
        _startButton.Margin = new Padding(0, 0, 6, 0);
        _startButton.Click += StartButton_Click;
        _stopButton.Text = "&Arrêter";
        _stopButton.Size = new Size(86, 25);
        _stopButton.Margin = new Padding(0, 0, 6, 0);
        _stopButton.Enabled = false;
        _stopButton.Click += async (_, _) => await StopSharingAsync();
        var closeButton = new RetroButton { Text = "&Fermer", Size = new Size(86, 25), Margin = Padding.Empty };
        closeButton.Click += (_, _) => Close();
        panel.Controls.AddRange([_startButton, _stopButton, closeButton]);
        return panel;
    }

    private static RetroGroupBox CreateGroup(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Top,
        AutoSize = true,
        BackColor = RetroTheme.Face,
        ForeColor = Color.Black,
        Padding = new Padding(6, 14, 6, 5),
        Margin = new Padding(0, 1, 0, 1)
    };

    private static Label CreateLabel(string text) => new()
    {
        Text = text,
        UseMnemonic = true,
        Anchor = AnchorStyles.Left,
        AutoSize = true,
        BackColor = RetroTheme.Face,
        ForeColor = Color.Black,
        Margin = new Padding(0, 3, 4, 2)
    };

    private void ResizeFileColumns()
    {
        if (_filesView.ClientSize.Width < 200 || _filesView.Columns.Count != 3)
        {
            return;
        }
        var available = Math.Max(250, _filesView.ClientSize.Width - 4);
        var sizeWidth = Math.Max(90, available * 15 / 100);
        var stateWidth = Math.Max(125, available * 20 / 100);
        _filesView.Columns[0].Width = Math.Max(100, available - sizeWidth - stateWidth);
        _filesView.Columns[1].Width = sizeWidth;
        _filesView.Columns[2].Width = stateWidth;
    }

    private async Task ChooseFileAsync()
    {
        SetPickerBusy(true);
        try
        {
            var path = await FilePickerService.ChooseFileAsync();
            if (!IsDisposed && !string.IsNullOrWhiteSpace(path))
            {
                SetSource(path, isFolder: false);
            }
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                RetroMessageBox.Show(this, exception.Message, "Sélection du fichier", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                SetPickerBusy(false);
            }
        }
    }

    private async Task ChooseFolderAsync()
    {
        SetPickerBusy(true);
        try
        {
            var path = await FilePickerService.ChooseFolderAsync();
            if (!IsDisposed && !string.IsNullOrWhiteSpace(path))
            {
                SetSource(path, isFolder: true);
            }
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                RetroMessageBox.Show(this, exception.Message, "Sélection du dossier", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                SetPickerBusy(false);
            }
        }
    }

    private void SetPickerBusy(bool busy)
    {
        _fileButton.Enabled = !busy;
        _folderButton.Enabled = !busy;
        _startButton.Enabled = !busy && _server is null;
        _statusLabel.Text = busy ? "Sélecteur Windows ouvert..." : (_sourcePath is null ? "Prêt" : "Source sélectionnée");
    }

    private void SetSource(string path, bool isFolder)
    {
        _sourcePath = path;
        _sourceIsFolder = isFolder;
        _sourceBox.Text = path;
        _filesView.Items.Clear();
        var item = new ListViewItem(
            Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)),
            isFolder ? RetroIcons.Folder : RetroIcons.File);
        if (isFolder)
        {
            item.SubItems.Add("Dossier");
        }
        else
        {
            item.SubItems.Add(ShareServer.FormatBytes(new FileInfo(path).Length));
        }
        item.SubItems.Add("Prêt");
        _filesView.Items.Add(item);
        _statusLabel.Text = "Source sélectionnée";
        _statusLabel.ItemText = isFolder ? "1 dossier" : "1 fichier";
        _statusLabel.ServerText = "Serveur arrêté";
    }

    private async void StartButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_sourcePath))
        {
            RetroMessageBox.Show(this, "Choisis d’abord un fichier ou un dossier.", "Source manquante", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!FirewallManager.EnsureInboundRule(this))
        {
            RetroMessageBox.Show(this, "L’autorisation du pare-feu est nécessaire pour recevoir une connexion Internet.", "Pare-feu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetBusy(true);
        try
        {
            _statusLabel.Text = _sourceIsFolder ? "Analyse du dossier..." : "Préparation...";
            var expiration = (_expirationBox.SelectedItem as ExpirationChoice)?.Lifetime;
            var password = _passwordBox.Text;
            var path = _sourcePath;
            var isFolder = _sourceIsFolder;
            _share = await Task.Run(() => ShareDescriptor.Create(path, isFolder, password, expiration));

            _filesView.Items.Clear();
            if (_share.IsFolder)
            {
                foreach (var file in _share.Files.Take(5000))
                {
                    var item = new ListViewItem(file.DisplayName, RetroIcons.File);
                    item.SubItems.Add(ShareServer.FormatBytes(file.Length));
                    item.SubItems.Add("En attente");
                    _filesView.Items.Add(item);
                }
                if (_share.Files.Count > 5000)
                {
                    var more = new ListViewItem($"… et {_share.Files.Count - 5000:N0} autre(s)", RetroIcons.Folder);
                    more.SubItems.Add(string.Empty);
                    more.SubItems.Add("Inclus");
                    _filesView.Items.Add(more);
                }
            }
            else
            {
                var file = _share.Files[0];
                var item = new ListViewItem(file.DisplayName, RetroIcons.File);
                item.SubItems.Add(ShareServer.FormatBytes(file.Length));
                item.SubItems.Add("En attente");
                _filesView.Items.Add(item);
            }

            _server = new ShareServer(Port);
            _server.Log += AppendLog;
            await _server.StartAsync(_share);

            _linkBox.Text = "Détection de l’adresse Internet...";
            var publicAddress = await NetworkAddress.GetPublicIpAsync();
            var address = publicAddress ?? NetworkAddress.GetLocalIp();
            if (address is null)
            {
                throw new InvalidOperationException("Impossible de déterminer une adresse réseau.");
            }

            _linkBox.Text = $"http://{NetworkAddress.UrlHost(address)}:{Port}/d/{_share.Token}";
            _copyButton.Enabled = true;
            _stopButton.Enabled = true;
            _startButton.Enabled = false;
            _fileButton.Enabled = false;
            _folderButton.Enabled = false;
            _passwordBox.Enabled = false;
            _expirationBox.Enabled = false;
            _previousBytes = 0;
            _previousSample = DateTime.UtcNow;
            _smoothedBytesPerSecond = 0;
            _statusLabel.Text = publicAddress is null ? "Partage actif — adresse locale seulement" : "Partage actif — lien prêt";
            _statusLabel.ItemText = _share.IsFolder ? $"{_share.Files.Count:N0} fichier(s)" : "1 fichier";
            _statusLabel.ServerText = "Serveur actif";
            AppendLog(publicAddress is null
                ? $"[{DateTime.Now:HH:mm:ss}] Adresse publique indisponible; lien local affiché."
                : $"[{DateTime.Now:HH:mm:ss}] Lien Internet prêt et copié.");
            CopyLink(silent: true);
        }
        catch (Exception exception)
        {
            if (_server is not null)
            {
                await _server.DisposeAsync();
                _server = null;
            }
            _share = null;
            _statusLabel.Text = "Erreur";
            _statusLabel.ServerText = "Serveur arrêté";
            foreach (ListViewItem item in _filesView.Items)
            {
                item.ImageKey = RetroIcons.Error;
            }
            RetroMessageBox.Show(this, exception.Message, "Impossible de démarrer le partage", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task StopSharingAsync()
    {
        _stopButton.Enabled = false;
        if (_server is not null)
        {
            _server.Log -= AppendLog;
            await _server.DisposeAsync();
            _server = null;
        }
        _share = null;
        _linkBox.Clear();
        _copyButton.Enabled = false;
        _startButton.Enabled = true;
        _fileButton.Enabled = true;
        _folderButton.Enabled = true;
        _passwordBox.Enabled = true;
        _expirationBox.Enabled = true;
        _progress.Value = 0;
        _percentLabel.Text = "0 %";
        _statsLabel.Text = "En attente d’un téléchargement.";
        foreach (ListViewItem item in _filesView.Items)
        {
            if (item.SubItems.Count > 2)
            {
                item.SubItems[2].Text = "Arrêté";
                item.ImageKey = _sourceIsFolder ? RetroIcons.Folder : RetroIcons.File;
            }
        }
        _statusLabel.Text = "Prêt";
        _statusLabel.ServerText = "Serveur arrêté";
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Partage arrêté.");
    }

    private void SetBusy(bool busy)
    {
        UseWaitCursor = busy;
        if (busy)
        {
            _startButton.Enabled = false;
            _fileButton.Enabled = false;
            _folderButton.Enabled = false;
        }
        else if (_server is null)
        {
            _startButton.Enabled = true;
            _fileButton.Enabled = true;
            _folderButton.Enabled = true;
        }
    }

    private void UpdateTransferDisplay(object? sender, EventArgs e)
    {
        if (_server is null || _share is null)
        {
            return;
        }

        var snapshot = _server.Snapshot;
        var now = DateTime.UtcNow;
        var elapsed = Math.Max(0.001, (now - _previousSample).TotalSeconds);
        var instantaneous = Math.Max(0, snapshot.BytesSent - _previousBytes) / elapsed;
        _smoothedBytesPerSecond = _smoothedBytesPerSecond == 0 ? instantaneous : (_smoothedBytesPerSecond * 0.7) + (instantaneous * 0.3);
        _previousBytes = snapshot.BytesSent;
        _previousSample = now;

        var total = Math.Max(1, _share.TotalBytes);
        var ratio = Math.Clamp((double)snapshot.BytesSent / total, 0, 1);
        _progress.Value = (int)Math.Round(ratio * _progress.Maximum);
        _percentLabel.Text = $"{ratio * 100:N0} %";
        var speed = ShareServer.FormatBytes((long)_smoothedBytesPerSecond) + "/s";
        var remainingBytes = Math.Max(0, _share.TotalBytes - Math.Min(snapshot.BytesSent, _share.TotalBytes));
        var remaining = _smoothedBytesPerSecond > 1
            ? FormatDuration(TimeSpan.FromSeconds(remainingBytes / _smoothedBytesPerSecond))
            : "—";
        _statsLabel.Text = $"{speed} — {ShareServer.FormatBytes(snapshot.BytesSent)} / {ShareServer.FormatBytes(_share.TotalBytes)} — temps restant : {remaining} — connexion(s) : {snapshot.ActiveDownloads}";

        var state = snapshot.ActiveDownloads > 0 ? "Transfert en cours" : snapshot.CompletedDownloads > 0 ? "Téléchargé" : "En attente";
        var imageKey = snapshot.ActiveDownloads > 0
            ? RetroIcons.Transfer
            : snapshot.CompletedDownloads > 0
                ? RetroIcons.Complete
                : RetroIcons.File;
        foreach (ListViewItem item in _filesView.Items)
        {
            if (item.SubItems.Count > 2)
            {
                item.SubItems[2].Text = state;
                item.ImageKey = imageKey;
            }
        }
        _statusLabel.ServerText = snapshot.ActiveDownloads > 0
            ? $"{speed} | {snapshot.ActiveDownloads} connexion(s)"
            : "Serveur actif";
    }

    private void CopyLink(bool silent = false)
    {
        if (string.IsNullOrWhiteSpace(_linkBox.Text) || !_linkBox.Text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            if (!silent)
            {
                RetroMessageBox.Show(this, "Aucun lien actif.", "Copier le lien", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return;
        }
        if (!ClipboardService.TrySetText(_linkBox.Text, out var error))
        {
            _statusLabel.Text = "Lien prêt — copie automatique indisponible";
            AppendLog($"[{DateTime.Now:HH:mm:ss}] Presse-papiers indisponible : {error}");
            if (!silent)
            {
                RetroMessageBox.Show(
                    this,
                    "Le lien est prêt, mais Windows n’a pas permis de le copier automatiquement.\n\n" +
                    "Sélectionne le texte du champ Lien et utilise Ctrl+C.",
                    "Presse-papiers indisponible",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            return;
        }
        if (!silent)
        {
            _statusLabel.Text = "Lien copié dans le presse-papiers";
        }
    }

    private void OpenLocalLink()
    {
        if (_share is null)
        {
            RetroMessageBox.Show(this, "Démarre d’abord un partage.", "Lien local", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = $"http://127.0.0.1:{Port}/d/{_share.Token}",
            UseShellExecute = true
        });
    }

    private void AppendLog(string line)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendLog(line));
            return;
        }
        _logBox.AppendText(line + Environment.NewLine);
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closing)
        {
            return;
        }
        _closing = true;
        _timer.Stop();
        if (_server is not null)
        {
            try
            {
                _server.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Windows will release the listener when the process exits.
            }
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays} j {duration.Hours:00}:{duration.Minutes:00}";
        }
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private sealed record ExpirationChoice(string Label, TimeSpan? Lifetime)
    {
        public override string ToString() => Label;
    }
}
