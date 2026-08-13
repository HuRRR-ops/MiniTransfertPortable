namespace MiniTransfertPortable;

internal static class RetroMessageBox
{
    public static DialogResult Show(
        IWin32Window? owner,
        string text,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon)
    {
        using var dialog = new RetroMessageDialog(text, caption, buttons, icon);
        return owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
    }
}

internal sealed class RetroMessageDialog : RetroChromeForm
{
    public RetroMessageDialog(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        : base(caption, allowMaximize: false, allowResize: false)
    {
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        MinimumSize = Size.Empty;

        var messageLabel = new Label
        {
            Text = message,
            AutoSize = true,
            MaximumSize = new Size(430, 0),
            BackColor = RetroTheme.Face,
            ForeColor = RetroTheme.Text,
            Font = RetroTheme.UiFont,
            Location = new Point(58, 17)
        };
        var measured = TextRenderer.MeasureText(
            message,
            messageLabel.Font,
            new Size(430, 1000),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        messageLabel.Size = new Size(Math.Max(180, measured.Width), measured.Height + 2);

        var contentWidth = Math.Clamp(messageLabel.Right + 18, 310, 520);
        var buttonsTop = Math.Max(72, messageLabel.Bottom + 18);
        var buttonCount = buttons == MessageBoxButtons.OKCancel ? 2 : 1;
        var buttonAreaWidth = (buttonCount * 80) + ((buttonCount - 1) * 8);
        var buttonLeft = Math.Max(12, (contentWidth - buttonAreaWidth) / 2);
        var contentHeight = buttonsTop + 39;
        ClientSize = new Size(contentWidth + 6, contentHeight + 28);

        var body = new Panel { Dock = DockStyle.Fill, BackColor = RetroTheme.Face };
        ContentPanel.Controls.Add(body);
        var picture = new PictureBox
        {
            Image = RetroIcons.CreateDialogBitmap(icon),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Location = new Point(14, 14),
            Size = new Size(34, 34),
            TabStop = false
        };
        body.Controls.Add(picture);
        body.Controls.Add(messageLabel);

        var ok = new RetroButton
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(80, 25),
            Location = new Point(buttonLeft, buttonsTop)
        };
        body.Controls.Add(ok);
        AcceptButton = ok;

        if (buttons == MessageBoxButtons.OKCancel)
        {
            var cancel = new RetroButton
            {
                Text = "Annuler",
                DialogResult = DialogResult.Cancel,
                Size = new Size(80, 25),
                Location = new Point(buttonLeft + 88, buttonsTop)
            };
            body.Controls.Add(cancel);
            CancelButton = cancel;
        }
        else
        {
            CancelButton = ok;
        }
    }
}
