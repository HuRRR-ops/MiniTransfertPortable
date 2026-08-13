using System.Reflection;

namespace MiniTransfertPortable;

internal sealed class AboutDialog : RetroChromeForm
{
    public AboutDialog()
        : base("À propos de MiniTransfert", allowMaximize: false, allowResize: false)
    {
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        MinimumSize = Size.Empty;
        ClientSize = new Size(390, 245);
        BackColor = RetroTheme.Face;

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = RetroTheme.Face,
            Padding = new Padding(12, 12, 12, 10)
        };
        ContentPanel.Controls.Add(body);

        var icon = new PictureBox
        {
            Image = RetroIcons.CreateApplicationBitmap(32),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Location = new Point(14, 17),
            Size = new Size(40, 40)
        };
        var title = new Label
        {
            Text = "MiniTransfert Portable",
            Font = RetroTheme.BoldFont,
            AutoSize = true,
            Location = new Point(65, 16),
            BackColor = RetroTheme.Face
        };
        var version = typeof(AboutDialog).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);
        var versionLabel = new Label
        {
            Text = $"Version {version.Major}.{version.Minor}.{version.Build}",
            AutoSize = true,
            Location = new Point(65, 37),
            BackColor = RetroTheme.Face
        };
        var description = new Label
        {
            Text = "Petit utilitaire portable de partage de fichiers.\r\n\r\n" +
                   "Les fichiers sont transmis directement depuis cet ordinateur\r\n" +
                   "vers le navigateur du destinataire.",
            AutoSize = true,
            Location = new Point(17, 79),
            BackColor = RetroTheme.Face
        };
        var separator = new RetroSeparator
        {
            Location = new Point(12, 163),
            Size = new Size(360, 2),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        var okButton = new RetroButton
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(78, 25),
            Location = new Point(294, 178),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom
        };
        body.Controls.AddRange([icon, title, versionLabel, description, separator, okButton]);
        AcceptButton = okButton;
        CancelButton = okButton;
    }
}

internal sealed class RetroSeparator : Control
{
    public RetroSeparator()
    {
        Height = 2;
        TabStop = false;
        BackColor = RetroTheme.Face;
    }

    protected override void OnPaint(PaintEventArgs paintEvent)
    {
        RetroTheme.DrawEtchedHorizontal(paintEvent.Graphics, 0, Width, 0);
    }
}
