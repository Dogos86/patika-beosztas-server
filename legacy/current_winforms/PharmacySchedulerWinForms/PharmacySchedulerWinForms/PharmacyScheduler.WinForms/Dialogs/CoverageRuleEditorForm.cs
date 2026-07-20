using PharmacyScheduler.Core;
using PharmacyScheduler.Core.Models;

namespace PharmacyScheduler.WinForms.Dialogs;

public sealed class CoverageRuleEditorForm : Form
{
    private readonly ComboBox _cmbLocation = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cmbDay = new() { Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly DateTimePicker _dtStart = new() { Dock = DockStyle.Left, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 120 };
    private readonly DateTimePicker _dtEnd = new() { Dock = DockStyle.Left, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 120 };
    private readonly ComboBox _cmbRole = new() { Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly ComboBox _cmbSeverity = new() { Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly NumericUpDown _numCount = new() { Dock = DockStyle.Left, Width = 120, Minimum = 1, Maximum = 50 };

    public CoverageRuleEditorForm(IReadOnlyList<Location> locations, CoverageRule? rule = null)
    {
        Text = rule is null ? "Új lefedettségi szabály" : "Lefedettségi szabály szerkesztése";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        Width = 520;
        Height = 330;
        MaximizeBox = false;
        MinimizeBox = false;

        Model = rule is null
            ? new CoverageRule()
            : new CoverageRule
            {
                Id = rule.Id,
                LocationId = rule.LocationId,
                DayOfWeek = rule.DayOfWeek,
                Start = rule.Start,
                End = rule.End,
                Role = rule.Role,
                RequiredCount = rule.RequiredCount,
                Severity = rule.Severity
            };

        _cmbLocation.DataSource = locations.ToList();
        _cmbLocation.DisplayMember = "Name";

        _cmbDay.DataSource = Enum.GetValues<DayOfWeek>();
        _cmbDay.Format += (_, e) =>
        {
            if (e.ListItem is DayOfWeek day) e.Value = day.ToHungarianDayName();
        };

        _cmbRole.DataSource = Enum.GetValues<EmployeeRole>();
        _cmbRole.Format += (_, e) =>
        {
            if (e.ListItem is EmployeeRole role) e.Value = role.ToDisplayText();
        };

        _cmbSeverity.DataSource = Enum.GetValues<Severity>();
        _cmbSeverity.Format += (_, e) =>
        {
            if (e.ListItem is Severity severity) e.Value = severity.ToDisplayText();
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 8,
            Padding = new Padding(12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Telephely:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_cmbLocation, 1, 0);
        layout.Controls.Add(new Label { Text = "Nap:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_cmbDay, 1, 1);
        layout.Controls.Add(new Label { Text = "Kezdés:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_dtStart, 1, 2);
        layout.Controls.Add(new Label { Text = "Vége:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        layout.Controls.Add(_dtEnd, 1, 3);
        layout.Controls.Add(new Label { Text = "Szerepkör:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        layout.Controls.Add(_cmbRole, 1, 4);
        layout.Controls.Add(new Label { Text = "Minimum létszám:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        layout.Controls.Add(_numCount, 1, 5);
        layout.Controls.Add(new Label { Text = "Súlyosság:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        layout.Controls.Add(_cmbSeverity, 1, 6);

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

        if (locations.Count > 0)
        {
            _cmbLocation.SelectedItem = locations.FirstOrDefault(x => x.Id == Model.LocationId) ?? locations[0];
        }

        _cmbDay.SelectedItem = Model.DayOfWeek;
        _dtStart.Value = DateTime.Today.Add(Model.Start.ToTimeSpan());
        _dtEnd.Value = DateTime.Today.Add(Model.End.ToTimeSpan());
        _cmbRole.SelectedItem = Model.Role;
        _cmbSeverity.SelectedItem = Model.Severity;
        _numCount.Value = Model.RequiredCount;
    }

    public CoverageRule Model { get; }

    private void SaveBack()
    {
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
            MessageBox.Show(this, "A szabály vége legyen később, mint a kezdete.", "Hibás időintervallum", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Model.LocationId = location.Id;
        Model.DayOfWeek = (DayOfWeek)_cmbDay.SelectedItem!;
        Model.Start = start;
        Model.End = end;
        Model.Role = (EmployeeRole)_cmbRole.SelectedItem!;
        Model.RequiredCount = (int)_numCount.Value;
        Model.Severity = (Severity)_cmbSeverity.SelectedItem!;
    }
}
