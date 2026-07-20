using PharmacyScheduler.Core;
using PharmacyScheduler.Core.Models;

namespace PharmacyScheduler.WinForms.Dialogs;

public sealed class ShiftEditorForm : Form
{
    private readonly ComboBox _cmbEmployee = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbLocation = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbTimeType = new() { Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly DateTimePicker _dtStart = new() { Dock = DockStyle.Left, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 120 };
    private readonly DateTimePicker _dtEnd = new() { Dock = DockStyle.Left, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 120 };
    private readonly DateTimePicker _dtDate = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Left, Width = 120 };
    private readonly TextBox _txtNote = new() { Dock = DockStyle.Fill, Multiline = true, Height = 70 };

    public ShiftEditorForm(IReadOnlyList<Employee> employees, IReadOnlyList<Location> locations, ShiftEntry? entry = null)
    {
        Text = entry is null ? "Új bejegyzés" : "Bejegyzés szerkesztése";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        Width = 560;
        Height = 380;
        MaximizeBox = false;
        MinimizeBox = false;

        Model = entry is null
            ? new ShiftEntry()
            : new ShiftEntry
            {
                Id = entry.Id,
                ScheduleId = entry.ScheduleId,
                EmployeeId = entry.EmployeeId,
                LocationId = entry.LocationId,
                Date = entry.Date,
                Start = entry.Start,
                End = entry.End,
                TimeType = entry.TimeType,
                Note = entry.Note
            };

        _cmbEmployee.DataSource = employees.ToList();
        _cmbEmployee.DisplayMember = nameof(Employee.DisplayName);

        _cmbLocation.DataSource = locations.ToList();
        _cmbLocation.DisplayMember = "Name";

        _cmbTimeType.DataSource = Enum.GetValues<TimeType>();
        _cmbTimeType.Format += (_, e) =>
        {
            if (e.ListItem is TimeType tt) e.Value = tt.ToDisplayText();
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(12)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Dolgozó:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_cmbEmployee, 1, 0);
        layout.Controls.Add(new Label { Text = "Telephely:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_cmbLocation, 1, 1);
        layout.Controls.Add(new Label { Text = "Dátum:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_dtDate, 1, 2);
        layout.Controls.Add(new Label { Text = "Kezdés:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        layout.Controls.Add(_dtStart, 1, 3);
        layout.Controls.Add(new Label { Text = "Vége:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        layout.Controls.Add(_dtEnd, 1, 4);
        layout.Controls.Add(new Label { Text = "Időtípus:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        layout.Controls.Add(_cmbTimeType, 1, 5);
        layout.Controls.Add(new Label { Text = "Megjegyzés:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        layout.Controls.Add(_txtNote, 1, 6);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 40 };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 100 };
        var cancel = new Button { Text = "Mégse", DialogResult = DialogResult.Cancel, Width = 100 };
        ok.Click += (_, _) => SaveBack();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        Controls.Add(layout);
        Controls.Add(buttons);
        AcceptButton = ok;
        CancelButton = cancel;

        if (employees.Count > 0)
        {
            _cmbEmployee.SelectedItem = employees.FirstOrDefault(x => x.Id == Model.EmployeeId) ?? employees[0];
        }

        if (locations.Count > 0)
        {
            _cmbLocation.SelectedItem = locations.FirstOrDefault(x => x.Id == Model.LocationId) ?? locations[0];
        }

        _dtDate.Value = Model.Date.ToDateTime(TimeOnly.MinValue);
        _dtStart.Value = DateTime.Today.Add(Model.Start.ToTimeSpan());
        _dtEnd.Value = DateTime.Today.Add(Model.End.ToTimeSpan());
        _cmbTimeType.SelectedItem = Model.TimeType;
        _txtNote.Text = Model.Note;
    }

    public ShiftEntry Model { get; }

    private void SaveBack()
    {
        if (_cmbEmployee.SelectedItem is not Employee employee)
        {
            MessageBox.Show(this, "Válassz dolgozót.", "Hiányzó adat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (_cmbLocation.SelectedItem is not Location location)
        {
            MessageBox.Show(this, "Válassz telephelyet.", "Hiányzó adat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        var start = TimeOnly.FromDateTime(_dtStart.Value);
        var end = TimeOnly.FromDateTime(_dtEnd.Value);

        if (start >= end)
        {
            MessageBox.Show(this, "A befejezés legyen később, mint a kezdés.", "Hibás időintervallum", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Model.EmployeeId = employee.Id;
        Model.LocationId = location.Id;
        Model.Date = DateOnly.FromDateTime(_dtDate.Value.Date);
        Model.Start = start;
        Model.End = end;
        Model.TimeType = (TimeType)_cmbTimeType.SelectedItem!;
        Model.Note = _txtNote.Text.Trim();
    }
}
