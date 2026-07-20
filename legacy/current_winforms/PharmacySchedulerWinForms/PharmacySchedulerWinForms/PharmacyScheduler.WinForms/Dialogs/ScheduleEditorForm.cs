using PharmacyScheduler.Core.Models;

namespace PharmacyScheduler.WinForms.Dialogs;

public sealed class ScheduleEditorForm : Form
{
    private readonly TextBox _txtName = new() { Dock = DockStyle.Fill };
    private readonly DateTimePicker _dtStart = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Left, Width = 120 };
    private readonly DateTimePicker _dtEnd = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Left, Width = 120 };

    public ScheduleEditorForm(SchedulePlan? schedule = null)
    {
        Text = schedule is null ? "Új beosztás" : "Beosztás szerkesztése";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 420;
        Height = 220;

        Model = schedule is null
            ? new SchedulePlan()
            : new SchedulePlan
            {
                Id = schedule.Id,
                Name = schedule.Name,
                PeriodStart = schedule.PeriodStart,
                PeriodEnd = schedule.PeriodEnd,
                Status = schedule.Status,
                CreatedAt = schedule.CreatedAt,
                CreatedBy = schedule.CreatedBy,
                ApprovedAt = schedule.ApprovedAt,
                ApprovedBy = schedule.ApprovedBy,
                Entries = schedule.Entries
            };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(12)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Név:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_txtName, 1, 0);
        layout.Controls.Add(new Label { Text = "Kezdete:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_dtStart, 1, 1);
        layout.Controls.Add(new Label { Text = "Vége:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_dtEnd, 1, 2);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 100 };
        var cancel = new Button { Text = "Mégse", DialogResult = DialogResult.Cancel, Width = 100 };
        ok.Click += (_, _) => SaveBack();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 1, 3);

        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;

        _txtName.Text = Model.Name;
        _dtStart.Value = Model.PeriodStart.ToDateTime(TimeOnly.MinValue);
        _dtEnd.Value = Model.PeriodEnd.ToDateTime(TimeOnly.MinValue);
    }

    public SchedulePlan Model { get; }

    private void SaveBack()
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show(this, "A beosztás neve kötelező.", "Hiányzó adat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (_dtStart.Value.Date > _dtEnd.Value.Date)
        {
            MessageBox.Show(this, "A beosztás vége nem lehet korábban a kezdetnél.", "Hibás időszak", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Model.Name = _txtName.Text.Trim();
        Model.PeriodStart = DateOnly.FromDateTime(_dtStart.Value.Date);
        Model.PeriodEnd = DateOnly.FromDateTime(_dtEnd.Value.Date);
    }
}
