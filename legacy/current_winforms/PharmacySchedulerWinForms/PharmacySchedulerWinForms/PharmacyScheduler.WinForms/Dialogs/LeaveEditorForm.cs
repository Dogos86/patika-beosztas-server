using PharmacyScheduler.Core;
using PharmacyScheduler.Core.Models;

namespace PharmacyScheduler.WinForms.Dialogs;

public sealed class LeaveEditorForm : Form
{
    private readonly ComboBox _cmbEmployee = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbType = new() { Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly DateTimePicker _dtStart = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Left, Width = 120 };
    private readonly DateTimePicker _dtEnd = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Left, Width = 120 };
    private readonly TextBox _txtNote = new() { Dock = DockStyle.Fill, Multiline = true, Height = 60 };

    public LeaveEditorForm(IReadOnlyList<Employee> employees, LeaveEntry? leave = null)
    {
        Text = leave is null ? "Új távollét" : "Távollét szerkesztése";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 500;
        Height = 320;

        Model = leave is null
            ? new LeaveEntry()
            : new LeaveEntry
            {
                Id = leave.Id,
                EmployeeId = leave.EmployeeId,
                StartDate = leave.StartDate,
                EndDate = leave.EndDate,
                LeaveType = leave.LeaveType,
                Note = leave.Note
            };

        _cmbEmployee.DataSource = employees.ToList();
        _cmbEmployee.DisplayMember = nameof(Employee.DisplayName);
        _cmbType.DataSource = new[] { TimeType.Vacation, TimeType.SickLeave, TimeType.UnpaidLeave, TimeType.MaternityLeave };
        _cmbType.Format += (_, e) =>
        {
            if (e.ListItem is TimeType tt) e.Value = tt.ToDisplayText();
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(12)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Dolgozó:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_cmbEmployee, 1, 0);
        layout.Controls.Add(new Label { Text = "Távollét típusa:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_cmbType, 1, 1);
        layout.Controls.Add(new Label { Text = "Kezdete:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_dtStart, 1, 2);
        layout.Controls.Add(new Label { Text = "Vége:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        layout.Controls.Add(_dtEnd, 1, 3);
        layout.Controls.Add(new Label { Text = "Megjegyzés:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        layout.Controls.Add(_txtNote, 1, 4);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 100 };
        var cancel = new Button { Text = "Mégse", DialogResult = DialogResult.Cancel, Width = 100 };
        ok.Click += (_, _) => SaveBack();
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons, 1, 5);

        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;

        if (employees.Count > 0)
        {
            _cmbEmployee.SelectedItem = employees.FirstOrDefault(x => x.Id == Model.EmployeeId) ?? employees[0];
        }

        _cmbType.SelectedItem = Model.LeaveType;
        _dtStart.Value = Model.StartDate.ToDateTime(TimeOnly.MinValue);
        _dtEnd.Value = Model.EndDate.ToDateTime(TimeOnly.MinValue);
        _txtNote.Text = Model.Note;
    }

    public LeaveEntry Model { get; }

    private void SaveBack()
    {
        if (_cmbEmployee.SelectedItem is not Employee employee)
        {
            MessageBox.Show(this, "Válassz dolgozót.", "Hiányzó adat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        if (_dtStart.Value.Date > _dtEnd.Value.Date)
        {
            MessageBox.Show(this, "A befejezés nem lehet korábban a kezdetnél.", "Hibás időszak", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Model.EmployeeId = employee.Id;
        Model.LeaveType = (TimeType)_cmbType.SelectedItem!;
        Model.StartDate = DateOnly.FromDateTime(_dtStart.Value.Date);
        Model.EndDate = DateOnly.FromDateTime(_dtEnd.Value.Date);
        Model.Note = _txtNote.Text.Trim();
    }
}
