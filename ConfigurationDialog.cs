using System.Net;
using System.Net.Sockets;

namespace MiniTransfertPortable;

internal sealed class ConfigurationDialog : RetroChromeForm
{
    private readonly NumericUpDown _portBox = new();
    private readonly Label _portTestResult = new();
    private readonly CheckBox _useDdnsBox = new();
    private readonly TextBox _publicHostBox = new();
    private readonly RetroButton _testDdnsButton = new();
    private readonly Label _ddnsTestResult = new();
    private string? _selectedPublicHost;
    private CancellationTokenSource? _ddnsTestCancellation;

    public ConfigurationDialog(int currentPort, string? currentPublicHost)
        : base("Configuration", allowMaximize: false, allowResize: false)
    {
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        MinimumSize = Size.Empty;
        ClientSize = new Size(430, 365);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = RetroTheme.Face,
            Padding = new Padding(10, 8, 10, 8)
        };
        ContentPanel.Controls.Add(body);

        var portGroup = new RetroGroupBox
        {
            Text = "Connexion entrante",
            Location = new Point(8, 7),
            Size = new Size(408, 116),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = RetroTheme.Face
        };
        body.Controls.Add(portGroup);

        var portLabel = new Label
        {
            Text = "&Port TCP :",
            UseMnemonic = true,
            AutoSize = true,
            Location = new Point(14, 27),
            BackColor = RetroTheme.Face
        };
        _portBox.Minimum = PortableSettings.MinimumPort;
        _portBox.Maximum = PortableSettings.MaximumPort;
        _portBox.Value = currentPort;
        _portBox.Location = new Point(91, 23);
        _portBox.Size = new Size(96, 22);
        _portBox.TextAlign = HorizontalAlignment.Right;
        _portBox.ThousandsSeparator = false;
        _portBox.ValueChanged += (_, _) => _portTestResult.Text = string.Empty;
        portLabel.Click += (_, _) => _portBox.Focus();

        var testPortButton = new RetroButton
        {
            Text = "&Tester le port",
            Location = new Point(198, 22),
            Size = new Size(112, 25)
        };
        testPortButton.Click += (_, _) => TestSelectedPort();

        var defaultPortButton = new RetroButton
        {
            Text = "Port par &défaut",
            Location = new Point(91, 52),
            Size = new Size(112, 25)
        };
        defaultPortButton.Click += (_, _) => _portBox.Value = PortableSettings.DefaultPort;

        _portTestResult.Location = new Point(14, 82);
        _portTestResult.Size = new Size(378, 24);
        _portTestResult.BackColor = RetroTheme.Face;
        _portTestResult.AutoEllipsis = true;
        portGroup.Controls.AddRange([portLabel, _portBox, testPortButton, defaultPortButton, _portTestResult]);

        var ddnsGroup = new RetroGroupBox
        {
            Text = "Adresse publique (DDNS)",
            Location = new Point(8, 130),
            Size = new Size(408, 121),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = RetroTheme.Face
        };
        body.Controls.Add(ddnsGroup);

        _useDdnsBox.Text = "&Utiliser un nom d’hôte DDNS dans les liens";
        _useDdnsBox.Location = new Point(14, 22);
        _useDdnsBox.Size = new Size(300, 21);
        _useDdnsBox.BackColor = RetroTheme.Face;
        _useDdnsBox.Checked = !string.IsNullOrWhiteSpace(currentPublicHost);
        _useDdnsBox.CheckedChanged += (_, _) => UpdateDdnsControls();

        var hostLabel = new Label
        {
            Text = "&Nom d’hôte :",
            UseMnemonic = true,
            AutoSize = true,
            Location = new Point(14, 53),
            BackColor = RetroTheme.Face
        };
        _publicHostBox.Text = currentPublicHost ?? string.Empty;
        _publicHostBox.Location = new Point(96, 49);
        _publicHostBox.Size = new Size(196, 22);
        _publicHostBox.BorderStyle = BorderStyle.Fixed3D;
        _publicHostBox.TextChanged += (_, _) => _ddnsTestResult.Text = string.Empty;
        hostLabel.Click += (_, _) => _publicHostBox.Focus();

        _testDdnsButton.Text = "Tester &DNS";
        _testDdnsButton.Location = new Point(300, 48);
        _testDdnsButton.Size = new Size(92, 25);
        _testDdnsButton.Click += async (_, _) => await TestDdnsAsync();

        _ddnsTestResult.Location = new Point(14, 82);
        _ddnsTestResult.Size = new Size(378, 28);
        _ddnsTestResult.BackColor = RetroTheme.Face;
        _ddnsTestResult.AutoEllipsis = true;
        ddnsGroup.Controls.AddRange([_useDdnsBox, hostLabel, _publicHostBox, _testDdnsButton, _ddnsTestResult]);

        var information = new Label
        {
            Text = "Le DUC No-IP doit rester actif. Le port doit aussi être autorisé\r\n" +
                   "dans le pare-feu et redirigé dans le routeur.",
            Location = new Point(11, 259),
            Size = new Size(400, 38),
            BackColor = RetroTheme.Face
        };
        body.Controls.Add(information);

        var okButton = new RetroButton
        {
            Text = "OK",
            Location = new Point(248, 305),
            Size = new Size(78, 25),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        okButton.Click += (_, _) => AcceptConfiguration();
        var cancelButton = new RetroButton
        {
            Text = "Annuler",
            DialogResult = DialogResult.Cancel,
            Location = new Point(334, 305),
            Size = new Size(78, 25),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        body.Controls.AddRange([okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        UpdateDdnsControls();
        Shown += (_, _) => _portBox.Focus();
        FormClosed += (_, _) => _ddnsTestCancellation?.Cancel();
    }

    public int SelectedPort => Decimal.ToInt32(_portBox.Value);
    public string? SelectedPublicHost => _selectedPublicHost;

    private void UpdateDdnsControls()
    {
        _publicHostBox.Enabled = _useDdnsBox.Checked;
        _testDdnsButton.Enabled = _useDdnsBox.Checked;
        _ddnsTestResult.Text = string.Empty;
    }

    private void TestSelectedPort()
    {
        var available = PortAvailability.IsAvailable(SelectedPort, out var message);
        _portTestResult.ForeColor = available ? Color.FromArgb(0, 96, 0) : Color.FromArgb(160, 0, 0);
        _portTestResult.Text = message;
    }

    private async Task TestDdnsAsync()
    {
        if (!PortableSettings.TryNormalizePublicHost(_publicHostBox.Text, out var host) || host is null)
        {
            ShowDdnsResult("Entre un nom d’hôte valide, sans http:// ni port.", success: false);
            return;
        }

        _testDdnsButton.Enabled = false;
        _ddnsTestResult.ForeColor = RetroTheme.Text;
        _ddnsTestResult.Text = "Vérification DNS en cours...";
        _ddnsTestCancellation?.Cancel();
        _ddnsTestCancellation?.Dispose();
        var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        _ddnsTestCancellation = timeout;
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host, timeout.Token);
            var publicAddress = await NetworkAddress.GetPublicIpAsync(timeout.Token);
            var publicIpv4 = publicAddress?.AddressFamily == AddressFamily.InterNetwork ? publicAddress : null;
            if (publicIpv4 is not null && addresses.Contains(publicIpv4))
            {
                ShowDdnsResult($"DDNS correct : {host} correspond à votre IP.", success: true);
            }
            else if (publicIpv4 is null && addresses.Length > 0)
            {
                ShowDdnsResult($"DNS résolu : {host} pointe vers {addresses[0]}.", success: true);
            }
            else if (addresses.Length > 0)
            {
                ShowDdnsResult($"Résolu vers {addresses[0]}, mais pas vers l’IP publique actuelle.", success: false);
            }
            else
            {
                ShowDdnsResult("Le nom d’hôte ne retourne aucune adresse.", success: false);
            }
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            ShowDdnsResult("Impossible de résoudre ce nom d’hôte pour le moment.", success: false);
        }
        finally
        {
            if (ReferenceEquals(_ddnsTestCancellation, timeout))
            {
                _ddnsTestCancellation = null;
            }
            timeout.Dispose();
            if (!IsDisposed)
            {
                _testDdnsButton.Enabled = _useDdnsBox.Checked;
            }
        }
    }

    private void ShowDdnsResult(string message, bool success)
    {
        if (IsDisposed)
        {
            return;
        }
        _ddnsTestResult.ForeColor = success ? Color.FromArgb(0, 96, 0) : Color.FromArgb(160, 0, 0);
        _ddnsTestResult.Text = message;
    }

    private void AcceptConfiguration()
    {
        if (_useDdnsBox.Checked)
        {
            if (!PortableSettings.TryNormalizePublicHost(_publicHostBox.Text, out _selectedPublicHost)
                || _selectedPublicHost is null)
            {
                RetroMessageBox.Show(
                    this,
                    "Entre un nom d’hôte valide, par exemple directfiles.ddns.net.\r\n\r\n" +
                    "N’ajoute pas http://, de port ou de chemin.",
                    "Configuration DDNS",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                _publicHostBox.Focus();
                return;
            }
        }
        else
        {
            _selectedPublicHost = null;
        }

        DialogResult = DialogResult.OK;
    }
}
