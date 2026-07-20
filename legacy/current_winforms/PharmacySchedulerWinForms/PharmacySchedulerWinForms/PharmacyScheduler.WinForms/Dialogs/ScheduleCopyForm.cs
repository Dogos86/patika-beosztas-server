namespace PharmacyScheduler.WinForms.Dialogs;

public sealed class ScheduleCopyForm : Form
{
    private readonly TextBox _txtName = new() { Dock = DockStyle.Fill };
    private readonly DateTimePicker _dtStart = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Left, Width = 120 };

    public ScheduleCopyForm(DateOnly sourceStart, string sourceName)
    {
        Text = "Beosztás másolása";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 420;
        Height = 180;

        SourceStart = sourceStart;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(12)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Új név:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_txtName, 1, 0);
        layout.Controls.Add(new Label { Text = "Új kezdőnap:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_dtStart, 1, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 100 };
        var cancel = new Button { Text = "Mégse", DialogResult = DialogResult.Cancel, Width = 100 };
        ok.Click += (_, _) => SaveBack();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 1, 2);

        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;

        _txtName.Text = $"{sourceName} - másolat";
        _dtStart.Value = sourceStart.ToDateTime(TimeOnly.MinValue).AddDays(7);
    }

    public DateOnly SourceStart { get; }

    public string NewName { get; private set; } = string.Empty;

    public DateOnly NewStartDate { get; private set; }

    private void SaveBack()
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show(this, "Adj meg nevet az új beosztásnak.", "Hiányzó adat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        NewName = _txtName.Text.Trim();
        NewStartDate = DateOnly.FromDateTime(_dtStart.Value.Date);
    }
}
