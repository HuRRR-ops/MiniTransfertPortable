namespace MiniTransfertPortable;

internal sealed class ConfigurationDialog : RetroChromeForm
{
    private readonly NumericUpDown _portBox = new();
    private readonly Label _testResult = new();

    public ConfigurationDialog(int currentPort)
        : base("Configuration", allowMaximize: false, allowResize: false)
    {
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        MinimumSize = Size.Empty;
        ClientSize = new Size(430, 245);

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = RetroTheme.Face,
            Padding = new Padding(10, 8, 10, 8)
        };
        ContentPanel.Controls.Add(body);

        var group = new RetroGroupBox
        {
            Text = "Connexion entrante",
            Location = new Point(8, 7),
            Size = new Size(408, 132),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = RetroTheme.Face
        };
        body.Controls.Add(group);

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
        _portBox.ValueChanged += (_, _) => _testResult.Text = string.Empty;
        portLabel.Click += (_, _) => _portBox.Focus();

        var testButton = new RetroButton
        {
            Text = "&Tester le port",
            Location = new Point(198, 22),
            Size = new Size(112, 25)
        };
        testButton.Click += (_, _) => TestSelectedPort();

        var defaultButton = new RetroButton
        {
            Text = "Port par &défaut",
            Location = new Point(91, 52),
            Size = new Size(112, 25)
        };
        defaultButton.Click += (_, _) =>
        {
            _portBox.Value = PortableSettings.DefaultPort;
            _testResult.Text = string.Empty;
        };

        _testResult.Location = new Point(14, 84);
        _testResult.Size = new Size(378, 36);
        _testResult.BackColor = RetroTheme.Face;
        _testResult.AutoEllipsis = true;

        group.Controls.AddRange([portLabel, _portBox, testButton, defaultButton, _testResult]);

        var information = new Label
        {
            Text = "Le même port doit être autorisé dans le pare-feu Windows\r\n" +
                   "et redirigé dans le routeur.",
            Location = new Point(11, 147),
            Size = new Size(400, 36),
            BackColor = RetroTheme.Face
        };
        body.Controls.Add(information);

        var okButton = new RetroButton
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(248, 183),
            Size = new Size(78, 25),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        var cancelButton = new RetroButton
        {
            Text = "Annuler",
            DialogResult = DialogResult.Cancel,
            Location = new Point(334, 183),
            Size = new Size(78, 25),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        body.Controls.AddRange([okButton, cancelButton]);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        Shown += (_, _) => _portBox.Focus();
    }

    public int SelectedPort => Decimal.ToInt32(_portBox.Value);

    private void TestSelectedPort()
    {
        var available = PortAvailability.IsAvailable(SelectedPort, out var message);
        _testResult.ForeColor = available ? Color.FromArgb(0, 96, 0) : Color.FromArgb(160, 0, 0);
        _testResult.Text = message;
    }
}
