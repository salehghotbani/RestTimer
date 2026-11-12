using System;
using System.Drawing;
using System.Windows.Forms;

namespace RestTimer
{
    internal sealed class UnsavedChangesDialog : Form
    {
        private readonly Timer themeTimer;
        private string paletteKey;
        public UnsavedChangesDialog(Icon icon)
        {
            Text = I18n.T("unsaved_title");
            Icon = icon;
            Name = "UnsavedChangesDialog";
            Font = Theme.BodyFont;
            RightToLeft = I18n.Flow;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(520, 220);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.Controls.Add(new Label { Text = I18n.T("unsaved_title"), Font = Theme.TitleFont, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            layout.Controls.Add(new Label { Text = I18n.T("unsaved_message"), Dock = DockStyle.Fill, Tag = "muted",
                TextAlign = ContentAlignment.TopLeft }, 0, 1);
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var save = new RoundedButton(primary: true) { Text = I18n.T("save_changes"), DialogResult = DialogResult.Yes, Dock = DockStyle.Fill, Margin = new Padding(4, 0, 4, 0) };
            var discard = new RoundedButton { Text = I18n.T("discard_changes"), DialogResult = DialogResult.No, Dock = DockStyle.Fill, Margin = new Padding(4, 0, 4, 0) };
            actions.Controls.Add(save, 0, 0);
            actions.Controls.Add(discard, 1, 0);
            layout.Controls.Add(actions, 0, 2);
            Controls.Add(layout);
            AcceptButton = save;
            Theme.Apply(this);
            paletteKey = Theme.PaletteKey;
            themeTimer = new Timer { Interval = 750 };
            themeTimer.Tick += (_, __) => { Theme.Refresh(); if (paletteKey != Theme.PaletteKey) { Theme.Apply(this); paletteKey = Theme.PaletteKey; } };
            themeTimer.Start();
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.ApplyWindowFrame(Handle); }
        protected override void Dispose(bool disposing) { if (disposing) themeTimer?.Dispose(); base.Dispose(disposing); }
    }
}
