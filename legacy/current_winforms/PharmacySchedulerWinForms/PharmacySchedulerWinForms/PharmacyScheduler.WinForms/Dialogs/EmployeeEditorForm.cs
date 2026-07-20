using PharmacyScheduler.Core;
using PharmacyScheduler.Core.Models;

namespace PharmacyScheduler.WinForms.Dialogs;

public sealed class EmployeeEditorForm : Form
{
    private readonly TextBox _txtFullName = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtDisplayName = new() { Dock = DockStyle.Fill };
    private readonly DateTimePicker _dtBirth = new() { Format = DateTimePickerFormat.Short, Dock = DockStyle.Left, Width = 120 };
 private readonly ComboBox _cmbRole = new() { Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly NumericUpDown _numMonthly = new() { Dock = DockStyle.Left, Width = 120, DecimalPlaces = 1, Minimum = 0, Maximum = 744, Increment = 0.5m };
    private readonly NumericUpDown _numDaily = new() { Dock = DockStyle.Left, Width = 120, DecimalPlaces = 1, Minimum = 0, Maximum = 24, Increment = 0.5m };
    private readonly CheckBox _chkActive = new() { Text = "Aktív, beosztható", AutoSize = true, Checked = true };
    private readonly CheckedListBox _lstTimeTypes = new() { CheckOnClick = true, Height = 120 };
    private readonly CheckedListBox _lstLocations = new() { CheckOnClick = true, Height = 120 };

    // Preferált idősáv - időválasztók
    private readonly DateTimePicker _dtPreferredStart = new() { Dock = DockStyle.Left, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 90 };
    private readonly DateTimePicker _dtPreferredEnd = new() { Dock = DockStyle.Left, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 90 };
    private readonly ListBox _lstPreferred = new() { Height = 60, Width = 300 };
  private readonly Button _btnAddPreferred = new() { Text = "Hozzáad", Width = 75, Height = 25 };
    private readonly Button _btnRemovePreferred = new() { Text = "Eltávolít", Width = 75, Height = 25 };

    // Tiltott idősáv - időválasztók
    private readonly DateTimePicker _dtForbiddenStart = new() { Dock = DockStyle.Left, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 90 };
    private readonly DateTimePicker _dtForbiddenEnd = new() { Dock = DockStyle.Left, Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Width = 90 };
    private readonly ListBox _lstForbidden = new() { Height = 60, Width = 300 };
    private readonly Button _btnAddForbidden = new() { Text = "Hozzáad", Width = 75, Height = 25 };
    private readonly Button _btnRemoveForbidden = new() { Text = "Eltávolít", Width = 75, Height = 25 };

    // Gyógyszertárvezető beoszthatósága
    private readonly CheckBox _chkIncludeInAutoSchedule = new() { Text = "Részt vesz az automatikus beosztásban", AutoSize = true, Checked = true };
    private readonly ComboBox _cmbAutoScheduleRole = new() { Dock = DockStyle.Left, DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly Label _lblAutoScheduleRole = new() { Text = "Generálási szerepkör:", AutoSize = true, Anchor = AnchorStyles.Left };

    private bool _initializing = true;

    public EmployeeEditorForm(IReadOnlyList<Location> locations, Employee? employee = null)
    {
        Text = employee is null ? "Új dolgozó" : "Dolgozó szerkesztése";
     FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 720;
        Height = 820;
        AutoScroll = true;

  Model = employee is null
         ? new Employee()
      : new Employee
   {
                Id = employee.Id,
         FullName = employee.FullName,
   DisplayName = employee.DisplayName,
     BirthDate = employee.BirthDate,
  Role = employee.Role,
      MonthlyHoursLimit = employee.MonthlyHoursLimit,
       MaxDailyHours = employee.MaxDailyHours,
       PreferredWindows = employee.PreferredWindows,
            ForbiddenWindows = employee.ForbiddenWindows,
   IsActive = employee.IsActive,
            AllowedLocationIds = employee.AllowedLocationIds.ToList(),
        AllowedTimeTypes = employee.AllowedTimeTypes.ToList(),
              IncludeInAutoSchedule = employee.IncludeInAutoSchedule,
   AutoScheduleRoleOverride = employee.AutoScheduleRoleOverride
            };

     // --- Szerepkör ComboBox ---
        _cmbRole.DataSource = Enum.GetValues<EmployeeRole>();
        _cmbRole.Format += (_, e) =>
        {
        if (e.ListItem is EmployeeRole role) e.Value = role.ToDisplayText();
      };

     // --- Auto-schedule role ComboBox ---
        _cmbAutoScheduleRole.Items.Add("Saját szerepkör");
        foreach (var r in Enum.GetValues<EmployeeRole>())
        {
            _cmbAutoScheduleRole.Items.Add(r);
        }
        _cmbAutoScheduleRole.Format += (_, e) =>
        {
          if (e.ListItem is EmployeeRole role) e.Value = role.ToDisplayText();
        };

        // --- Engedélyezett időtípusok ---
        foreach (var tt in Enum.GetValues<TimeType>())
        {
  _lstTimeTypes.Items.Add(tt, Model.AllowedTimeTypes.Contains(tt));
        }
 _lstTimeTypes.Format += (_, e) =>
        {
     if (e.ListItem is TimeType tt) e.Value = tt.ToDisplayText();
        };

        // --- Telephelyek ---
        foreach (var location in locations)
        {
     var idx = _lstLocations.Items.Add(location);
      _lstLocations.SetItemChecked(idx, Model.AllowedLocationIds.Count == 0 || Model.AllowedLocationIds.Contains(location.Id));
        }

        // --- Preferált idősávok betöltése ---
        LoadTimeWindows(Model.PreferredWindows, _lstPreferred);
        _btnAddPreferred.Click += (_, _) => AddTimeWindow(_dtPreferredStart, _dtPreferredEnd, _lstPreferred);
        _btnRemovePreferred.Click += (_, _) => RemoveSelectedTimeWindow(_lstPreferred);

        // --- Tiltott idősávok betöltése ---
   LoadTimeWindows(Model.ForbiddenWindows, _lstForbidden);
    _btnAddForbidden.Click += (_, _) => AddTimeWindow(_dtForbiddenStart, _dtForbiddenEnd, _lstForbidden);
        _btnRemoveForbidden.Click += (_, _) => RemoveSelectedTimeWindow(_lstForbidden);

        // --- Szerepkör változásra frissítjük a vezető-specifikus mezők láthatóságát ---
   _cmbRole.SelectedIndexChanged += (_, _) =>
        {
    if (!_initializing) UpdateManagerFieldsVisibility();
  };

        // --- Layout ---
        var layout = new TableLayoutPanel
        {
      Dock = DockStyle.Fill,
ColumnCount = 2,
     RowCount = 15,
        Padding = new Padding(12),
            AutoSize = true
      };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Teljes név:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_txtFullName, 1, 0);
    layout.Controls.Add(new Label { Text = "Megjelenítési név:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_txtDisplayName, 1, 1);
        layout.Controls.Add(new Label { Text = "Születési idő:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(_dtBirth, 1, 2);
 layout.Controls.Add(new Label { Text = "Szerepkör:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
  layout.Controls.Add(_cmbRole, 1, 3);
        layout.Controls.Add(new Label { Text = "Havi keret (óra):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
  layout.Controls.Add(_numMonthly, 1, 4);
        layout.Controls.Add(new Label { Text = "Max. napi óra:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
   layout.Controls.Add(_numDaily, 1, 5);

  // Preferált idősáv
  layout.Controls.Add(new Label { Text = "Preferált idősávok:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        var preferredPanel = BuildTimeWindowPanel(_dtPreferredStart, _dtPreferredEnd, _btnAddPreferred, _btnRemovePreferred, _lstPreferred);
        layout.Controls.Add(preferredPanel, 1, 6);

        // Tiltott idősáv
     layout.Controls.Add(new Label { Text = "Tiltott idősávok:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        var forbiddenPanel = BuildTimeWindowPanel(_dtForbiddenStart, _dtForbiddenEnd, _btnAddForbidden, _btnRemoveForbidden, _lstForbidden);
     layout.Controls.Add(forbiddenPanel, 1, 7);

        layout.Controls.Add(new Label { Text = "Engedélyezett időtípusok:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 8);
layout.Controls.Add(_lstTimeTypes, 1, 8);
        layout.Controls.Add(new Label { Text = "Telephelyek:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 9);
        layout.Controls.Add(_lstLocations, 1, 9);

   // Gyógyszertárvezető beoszthatósági beállítások
 layout.Controls.Add(_chkIncludeInAutoSchedule, 1, 10);
 layout.Controls.Add(_lblAutoScheduleRole, 0, 11);
        layout.Controls.Add(_cmbAutoScheduleRole, 1, 11);

     var bottomPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 100 };
        var cancel = new Button { Text = "Mégse", DialogResult = DialogResult.Cancel, Width = 100 };
 bottomPanel.Controls.Add(ok);
  bottomPanel.Controls.Add(cancel);
        bottomPanel.Controls.Add(_chkActive);
        ok.Click += (_, _) => SaveBack();
   layout.Controls.Add(bottomPanel, 1, 12);

 Controls.Add(layout);
    AcceptButton = ok;
     CancelButton = cancel;

 // --- Értékek betöltése ---
        _txtFullName.Text = Model.FullName;
      _txtDisplayName.Text = Model.DisplayName;

        // BirthDate biztonsági ellenőrzés a DateTimePicker érvényes tartományára
        var birthDate = Model.BirthDate;
        if (birthDate < _dtBirth.MinDate) birthDate = _dtBirth.MinDate;
      if (birthDate > _dtBirth.MaxDate) birthDate = _dtBirth.MaxDate;
    _dtBirth.Value = birthDate;

        _cmbRole.SelectedItem = Model.Role;
        _numMonthly.Value = Model.MonthlyHoursLimit;
        _numDaily.Value = Model.MaxDailyHours;
      _chkActive.Checked = Model.IsActive;
      _chkIncludeInAutoSchedule.Checked = Model.IncludeInAutoSchedule;

        if (Model.AutoScheduleRoleOverride.HasValue)
        {
      // Keressük meg az enum értéket az Items-ben
        for (var i = 0; i < _cmbAutoScheduleRole.Items.Count; i++)
     {
     if (_cmbAutoScheduleRole.Items[i] is EmployeeRole r && r == Model.AutoScheduleRoleOverride.Value)
    {
             _cmbAutoScheduleRole.SelectedIndex = i;
       break;
       }
        }
        }
        else
        {
_cmbAutoScheduleRole.SelectedIndex = 0; // "Saját szerepkör"
        }

        // Preferált/Tiltott idősáv DateTimePicker alapértékek
        _dtPreferredStart.Value = DateTime.Today.AddHours(8);
        _dtPreferredEnd.Value = DateTime.Today.AddHours(16);
        _dtForbiddenStart.Value = DateTime.Today.AddHours(20);
        _dtForbiddenEnd.Value = DateTime.Today.AddHours(23);

        _initializing = false;
        UpdateManagerFieldsVisibility();
    }

    public Employee Model { get; }

    private void UpdateManagerFieldsVisibility()
    {
        var isManager = _cmbRole.SelectedItem is EmployeeRole role && role == EmployeeRole.PharmacyManager;
        _chkIncludeInAutoSchedule.Visible = isManager;
 _lblAutoScheduleRole.Visible = isManager;
    _cmbAutoScheduleRole.Visible = isManager;

    if (!isManager)
        {
            _chkIncludeInAutoSchedule.Checked = true;
        }
    }

 private static Panel BuildTimeWindowPanel(DateTimePicker dtStart, DateTimePicker dtEnd, Button btnAdd, Button btnRemove, ListBox list)
    {
        var panel = new Panel { Height = 120, Dock = DockStyle.Fill };

   var topRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, FlowDirection = FlowDirection.LeftToRight };
        topRow.Controls.Add(dtStart);
        topRow.Controls.Add(new Label { Text = "–", AutoSize = true, Padding = new Padding(2, 4, 2, 0) });
        topRow.Controls.Add(dtEnd);
        topRow.Controls.Add(btnAdd);
        topRow.Controls.Add(btnRemove);

 list.Dock = DockStyle.Fill;

        panel.Controls.Add(list);
  panel.Controls.Add(topRow);
        return panel;
    }

    private static void LoadTimeWindows(string windowsText, ListBox list)
    {
 list.Items.Clear();
    if (string.IsNullOrWhiteSpace(windowsText)) return;

     var parts = windowsText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
  {
   if (!string.IsNullOrWhiteSpace(part))
  {
            list.Items.Add(part.Trim());
    }
        }
    }

    private static void AddTimeWindow(DateTimePicker dtStart, DateTimePicker dtEnd, ListBox list)
  {
        var start = TimeOnly.FromDateTime(dtStart.Value);
        var end = TimeOnly.FromDateTime(dtEnd.Value);

   if (start >= end)
        {
 MessageBox.Show("A záró időpont legyen későbbi, mint a kezdő.", "Hibás idősáv", MessageBoxButtons.OK, MessageBoxIcon.Warning);
 return;
        }

        var windowText = $"{start:HH:mm}-{end:HH:mm}";
        if (!list.Items.Contains(windowText))
        {
   list.Items.Add(windowText);
        }
    }

    private static void RemoveSelectedTimeWindow(ListBox list)
    {
     if (list.SelectedIndex >= 0)
        {
    list.Items.RemoveAt(list.SelectedIndex);
        }
    }

    private static string BuildTimeWindowsString(ListBox list)
    {
        var items = new List<string>();
        foreach (var item in list.Items)
        {
            items.Add(item.ToString()!);
        }
      return string.Join("; ", items);
    }

    private void SaveBack()
    {
        if (string.IsNullOrWhiteSpace(_txtFullName.Text))
        {
      MessageBox.Show(this, "A teljes név kötelező.", "Hiányzó adat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
         DialogResult = DialogResult.None;
          return;
        }

        if (string.IsNullOrWhiteSpace(_txtDisplayName.Text))
    {
    MessageBox.Show(this, "A megjelenítési név kötelező.", "Hiányzó adat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
      return;
        }

        Model.FullName = _txtFullName.Text.Trim();
        Model.DisplayName = _txtDisplayName.Text.Trim();
Model.BirthDate = _dtBirth.Value.Date;
        Model.Role = (EmployeeRole)_cmbRole.SelectedItem!;
        Model.MonthlyHoursLimit = _numMonthly.Value;
    Model.MaxDailyHours = _numDaily.Value;
  Model.PreferredWindows = BuildTimeWindowsString(_lstPreferred);
        Model.ForbiddenWindows = BuildTimeWindowsString(_lstForbidden);
        Model.IsActive = _chkActive.Checked;

        Model.AllowedTimeTypes = _lstTimeTypes.CheckedItems.Cast<TimeType>().ToList();
   Model.AllowedLocationIds = _lstLocations.CheckedItems.Cast<Location>().Select(x => x.Id).ToList();

        // Gyógyszertárvezető beoszthatósági beállítások
   if (Model.Role == EmployeeRole.PharmacyManager)
        {
  Model.IncludeInAutoSchedule = _chkIncludeInAutoSchedule.Checked;
   if (_cmbAutoScheduleRole.SelectedIndex <= 0) // "Saját szerepkör" vagy nincs kiválasztva
          {
     Model.AutoScheduleRoleOverride = null;
            }
       else if (_cmbAutoScheduleRole.SelectedItem is EmployeeRole selectedRole)
      {
                Model.AutoScheduleRoleOverride = selectedRole;
     }
}
        else
        {
            Model.IncludeInAutoSchedule = true;
            Model.AutoScheduleRoleOverride = null;
     }
    }
}
