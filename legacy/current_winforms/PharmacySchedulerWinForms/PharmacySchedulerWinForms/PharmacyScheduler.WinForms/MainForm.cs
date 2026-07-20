using PharmacyScheduler.Core;
using PharmacyScheduler.Core.Models;
using PharmacyScheduler.Core.Services;
using PharmacyScheduler.WinForms.Dialogs;
using PharmacyScheduler.WinForms.Infrastructure;
using PharmacyScheduler.WinForms.ViewModels;

namespace PharmacyScheduler.WinForms;

public sealed class MainForm : Form
{
    private readonly AppDataFileStore _store;
    private readonly ScheduleValidationService _validationService = new();
    private readonly AutoSchedulerService _autoSchedulerService = new();
    private readonly ScheduleQueryService _queryService = new();
    private readonly ExcelExportService _excelExportService = new();
    private readonly PdfExportService _pdfExportService = new();

    private AppData _data;
    private ValidationReport _currentReport = new();

    private readonly DataGridView _locationsGrid = CreateReadOnlyGrid();
    private readonly DataGridView _employeesGrid = CreateReadOnlyGrid();
    private readonly DataGridView _leavesGrid = CreateReadOnlyGrid();
    private readonly DataGridView _coverageGrid = CreateReadOnlyGrid();
    private readonly DataGridView _shiftGrid = CreateReadOnlyGrid();

    private readonly ComboBox _cmbSchedules = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
    private readonly Label _lblScheduleStatus = new() { AutoSize = true, Padding = new Padding(8, 8, 8, 8) };
    private readonly Label _lblDataFile = new() { AutoSize = true };
    private readonly ListView _issuesList = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
    private readonly TextBox _summaryText = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    private readonly ComboBox _cmbDailySeverity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox _cmbMonthlySeverity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox _cmbPreferredSeverity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox _cmbForbiddenSeverity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox _cmbAllowedTimeTypeSeverity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox _cmbAllowedLocationSeverity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly ComboBox _cmbLeaveConflictSeverity = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };

    public MainForm(AppDataFileStore store)
    {
        _store = store;
        _data = store.Load();

        Text = "Gyógyszertári Beosztás Készítő – WinForms prototípus";
        Width = 1500;
        Height = 920;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        RefreshAllViews();
    }

    private void BuildUi()
    {
        Controls.Add(BuildTabControl());
        Controls.Add(BuildMenu());
    }

    private Control BuildMenu()
    {
        var menu = new MenuStrip();

        var file = new ToolStripMenuItem("Fájl");
        file.DropDownItems.Add("Mentés", null, (_, _) => SaveData());
        file.DropDownItems.Add("Újratöltés", null, (_, _) => ReloadData());
        file.DropDownItems.Add("Mintaadat visszaállítása", null, (_, _) => ResetToSampleData());
        file.DropDownItems.Add("Kilépés", null, (_, _) => Close());

        menu.Items.Add(file);
        MainMenuStrip = menu;
        return menu;
    }

    private Control BuildTabControl()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        tabs.TabPages.Add(BuildCrudTab("Telephelyek", _locationsGrid,
            ("Új", (_, _) => AddLocation()),
            ("Szerkesztés", (_, _) => EditLocation()),
            ("Törlés", (_, _) => DeleteLocation())));

        tabs.TabPages.Add(BuildCrudTab("Dolgozók", _employeesGrid,
            ("Új", (_, _) => AddEmployee()),
            ("Szerkesztés", (_, _) => EditEmployee()),
            ("Törlés", (_, _) => DeleteEmployee())));

        tabs.TabPages.Add(BuildCrudTab("Távollétek", _leavesGrid,
            ("Új", (_, _) => AddLeave()),
            ("Szerkesztés", (_, _) => EditLeave()),
            ("Törlés", (_, _) => DeleteLeave())));

        tabs.TabPages.Add(BuildCrudTab("Lefedettségi szabályok", _coverageGrid,
            ("Új", (_, _) => AddCoverageRule()),
            ("Szerkesztés", (_, _) => EditCoverageRule()),
            ("Törlés", (_, _) => DeleteCoverageRule())));

        tabs.TabPages.Add(BuildScheduleTab());
        tabs.TabPages.Add(BuildSettingsTab());

        return tabs;
    }

    private static TabPage BuildCrudTab(string title, DataGridView grid, params (string Text, EventHandler Handler)[] buttons)
    {
        var page = new TabPage(title);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(8)
        };

        foreach (var (text, handler) in buttons)
        {
            var button = new Button { Text = text, Width = 110, Height = 28 };
            button.Click += handler;
            buttonPanel.Controls.Add(button);
        }

        page.Controls.Add(grid);
        page.Controls.Add(buttonPanel);
        return page;
    }

    private TabPage BuildScheduleTab()
    {
        _issuesList.Columns.Add("Súlyosság", 120);
        _issuesList.Columns.Add("Kód", 180);
        _issuesList.Columns.Add("Üzenet", 1000);

        var page = new TabPage("Beosztás");

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 78,
            Padding = new Padding(8),
            AutoScroll = true
        };

        toolbar.Controls.Add(new Label { Text = "Aktuális beosztás:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        _cmbSchedules.SelectedIndexChanged += (_, _) => RefreshScheduleDetails();
        toolbar.Controls.Add(_cmbSchedules);
        toolbar.Controls.Add(CreateButton("Új beosztás", (_, _) => AddSchedule()));
        toolbar.Controls.Add(CreateButton("Időszak szerk.", (_, _) => EditSchedule()));
        toolbar.Controls.Add(CreateButton("Másolat", (_, _) => CopySchedule()));
        toolbar.Controls.Add(CreateButton("Beosztás törlése", (_, _) => DeleteSchedule(), 130));
        toolbar.Controls.Add(new Label { Text = "|", AutoSize = true, Padding = new Padding(8, 8, 8, 0) });
        toolbar.Controls.Add(CreateButton("Új bejegyzés", (_, _) => AddShift()));
        toolbar.Controls.Add(CreateButton("Szerkesztés", (_, _) => EditShift()));
        toolbar.Controls.Add(CreateButton("Bejegyzés törlése", (_, _) => DeleteShift(), 130));
        toolbar.Controls.Add(CreateButton("Aut. kitöltés", (_, _) => AutoFillSchedule()));
        toolbar.Controls.Add(CreateButton("Ellenőrzés", (_, _) => RefreshScheduleDetails()));
        toolbar.Controls.Add(CreateButton("Jóváhagyás", (_, _) => ApproveSchedule()));
        toolbar.Controls.Add(CreateButton("Excel export", (_, _) => ExportExcel()));
        toolbar.Controls.Add(CreateButton("PDF export", (_, _) => ExportPdf()));
        toolbar.Controls.Add(_lblScheduleStatus);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 420
        };

        split.Panel1.Controls.Add(_shiftGrid);

        var bottomSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 1100
        };

        bottomSplit.Panel1.Controls.Add(_issuesList);
        bottomSplit.Panel2.Controls.Add(_summaryText);
        split.Panel2.Controls.Add(bottomSplit);

        page.Controls.Add(split);
        page.Controls.Add(toolbar);

        return page;
    }

    private TabPage BuildSettingsTab()
    {
        BindSeverityCombo(_cmbDailySeverity);
        BindSeverityCombo(_cmbMonthlySeverity);
        BindSeverityCombo(_cmbPreferredSeverity);
        BindSeverityCombo(_cmbForbiddenSeverity);
        BindSeverityCombo(_cmbAllowedTimeTypeSeverity);
        BindSeverityCombo(_cmbAllowedLocationSeverity);
        BindSeverityCombo(_cmbLeaveConflictSeverity);

        var page = new TabPage("Beállítások");
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 9,
            Padding = new Padding(16),
            AutoSize = true
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var row = 0;
        AddSettingsRow(panel, ref row, "Napi órakeret súlyosság:", _cmbDailySeverity);
        AddSettingsRow(panel, ref row, "Havi keret súlyosság:", _cmbMonthlySeverity);
        AddSettingsRow(panel, ref row, "Preferált idősáv súlyosság:", _cmbPreferredSeverity);
        AddSettingsRow(panel, ref row, "Tiltott idősáv súlyosság:", _cmbForbiddenSeverity);
        AddSettingsRow(panel, ref row, "Nem engedélyezett időtípus:", _cmbAllowedTimeTypeSeverity);
        AddSettingsRow(panel, ref row, "Nem engedélyezett telephely:", _cmbAllowedLocationSeverity);
        AddSettingsRow(panel, ref row, "Távolléttel ütközés:", _cmbLeaveConflictSeverity);

        var saveButton = CreateButton("Beállítások mentése", (_, _) => SaveSettings());
        panel.Controls.Add(saveButton, 1, row++);

        _lblDataFile.Text = $"Adatfájl: {_store.FilePath}";
        panel.Controls.Add(_lblDataFile, 1, row);

        page.Controls.Add(panel);
        return page;
    }

    private static void AddSettingsRow(TableLayoutPanel panel, ref int row, string labelText, Control control)
    {
        panel.Controls.Add(new Label { Text = labelText, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        panel.Controls.Add(control, 1, row);
        row++;
    }

    private void RefreshAllViews()
    {
        EnsureRoleConstraints();
        RefreshLocationGrid();
        RefreshEmployeeGrid();
        RefreshLeaveGrid();
        RefreshCoverageGrid();
        RefreshScheduleCombo();
        LoadSettingsIntoControls();
        RefreshScheduleDetails();
    }

    private void EnsureRoleConstraints()
    {
        var managers = _data.Employees.Where(x => x.Role == EmployeeRole.PharmacyManager).ToList();
        if (managers.Count <= 1)
        {
            return;
        }

        foreach (var extra in managers.Skip(1))
        {
            extra.Role = EmployeeRole.Pharmacist;
        }
    }

    private void RefreshLocationGrid()
    {
        _locationsGrid.DataSource = _data.Locations
            .OrderBy(x => x.Name)
            .Select(x => new LocationGridRow
            {
                Id = x.Id,
                Név = x.Name,
                Cím = x.Address,
                Aktív = x.IsActive
            })
            .ToList();

        HideIdColumn(_locationsGrid);
    }

    private void RefreshEmployeeGrid()
    {
        var locationLookup = _data.Locations.ToDictionary(x => x.Id, x => x.Name);

        _employeesGrid.DataSource = _data.Employees
            .OrderBy(x => x.DisplayName)
            .Select(x => new EmployeeGridRow
            {
                Id = x.Id,
                TeljesNév = x.FullName,
                MegjelenítésiNév = x.DisplayName,
                Szerepkör = x.Role.ToDisplayText(),
                Telephelyek = x.AllowedLocationIds.Count == 0
                    ? "Összes"
                    : string.Join(", ", x.AllowedLocationIds.Select(id => locationLookup.TryGetValue(id, out var name) ? name : "Ismeretlen")),
                HaviKeret = x.MonthlyHoursLimit,
                MaxNapiÓra = x.MaxDailyHours,
                PreferáltIdősávok = x.PreferredWindows,
                Aktív = x.IsActive
            })
            .ToList();

        HideIdColumn(_employeesGrid);
    }

    private void RefreshLeaveGrid()
    {
        var employeeLookup = _data.Employees.ToDictionary(x => x.Id, x => x.DisplayName);

        _leavesGrid.DataSource = _data.Leaves
            .OrderBy(x => x.StartDate)
            .Select(x => new LeaveGridRow
            {
                Id = x.Id,
                Dolgozó = employeeLookup.TryGetValue(x.EmployeeId, out var name) ? name : "Ismeretlen dolgozó",
                Kezdete = x.StartDate.ToString("yyyy-MM-dd"),
                Vége = x.EndDate.ToString("yyyy-MM-dd"),
                Típus = x.LeaveType.ToDisplayText(),
                Megjegyzés = x.Note
            })
            .ToList();

        HideIdColumn(_leavesGrid);
    }

    private void RefreshCoverageGrid()
    {
        var locationLookup = _data.Locations.ToDictionary(x => x.Id, x => x.Name);

        _coverageGrid.DataSource = _data.CoverageRules
            .OrderBy(x => x.LocationId)
            .ThenBy(x => x.DayOfWeek)
            .ThenBy(x => x.Start)
            .Select(x => new CoverageGridRow
            {
                Id = x.Id,
                Telephely = locationLookup.TryGetValue(x.LocationId, out var location) ? location : "Ismeretlen telephely",
                Nap = x.DayOfWeek.ToHungarianDayName(),
                Kezdés = x.Start.ToString("HH:mm"),
                Vége = x.End.ToString("HH:mm"),
                Szerepkör = x.Role.ToDisplayText(),
                MinimumLétszám = x.RequiredCount,
                Súlyosság = x.Severity.ToDisplayText()
            })
            .ToList();

        HideIdColumn(_coverageGrid);
    }

    private void RefreshScheduleCombo()
    {
        var selectedId = CurrentSchedule?.Id;
        var schedules = _data.Schedules.OrderByDescending(x => x.PeriodStart).ToList();

        _cmbSchedules.DataSource = null;
        _cmbSchedules.DataSource = schedules;
        _cmbSchedules.DisplayMember = nameof(SchedulePlan.DisplayTitle);

        if (selectedId.HasValue)
        {
            _cmbSchedules.SelectedItem = schedules.FirstOrDefault(x => x.Id == selectedId.Value);
        }

        if (_cmbSchedules.SelectedItem is null && schedules.Count > 0)
        {
            _cmbSchedules.SelectedIndex = 0;
        }
    }

    private void RefreshScheduleDetails()
    {
        var schedule = CurrentSchedule;
        if (schedule is null)
        {
            _shiftGrid.DataSource = null;
            _issuesList.Items.Clear();
            _summaryText.Text = "Nincs kiválasztott beosztás.";
            _lblScheduleStatus.Text = string.Empty;
            return;
        }

        var employeeLookup = _data.Employees.ToDictionary(x => x.Id);
        var locationLookup = _data.Locations.ToDictionary(x => x.Id);

        _shiftGrid.DataSource = schedule.Entries
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Start)
            .Select(x =>
            {
                employeeLookup.TryGetValue(x.EmployeeId, out var employee);
                locationLookup.TryGetValue(x.LocationId, out var location);

                return new ShiftGridRow
                {
                    Id = x.Id,
                    Dátum = x.Date.ToString("yyyy-MM-dd"),
                    Kezdés = x.Start.ToString("HH:mm"),
                    Vége = x.End.ToString("HH:mm"),
                    Dolgozó = employee?.DisplayName ?? "Ismeretlen dolgozó",
                    Telephely = location?.Name ?? "Ismeretlen telephely",
                    Szerepkör = employee?.Role.ToDisplayText() ?? string.Empty,
                    IdőTípus = x.TimeType.ToDisplayText(),
                    Óraszám = x.Hours,
                    Megjegyzés = x.Note
                };
            })
            .ToList();

        HideIdColumn(_shiftGrid);

        _currentReport = _validationService.Validate(_data, schedule);
        _issuesList.Items.Clear();

        foreach (var issue in _currentReport.Issues)
        {
            var item = new ListViewItem(issue.Severity.ToDisplayText());
            item.SubItems.Add(issue.Code);
            item.SubItems.Add(issue.Message);
            _issuesList.Items.Add(item);
        }

        var summaryRows = _queryService.BuildSummary(_data, schedule);
        var hardCount = _currentReport.Issues.Count(x => x.Severity == Severity.Hard);
        var softCount = _currentReport.Issues.Count(x => x.Severity == Severity.Soft);

        _summaryText.Text = string.Join(Environment.NewLine, new[]
        {
            $"Beosztás: {schedule.Name}",
            $"Időszak: {schedule.PeriodStart:yyyy-MM-dd} - {schedule.PeriodEnd:yyyy-MM-dd}",
            $"Státusz: {schedule.Status.ToDisplayText()}",
            $"Bejegyzések száma: {schedule.Entries.Count}",
            $"Blokkoló szabálysértések: {hardCount}",
            $"Figyelmeztetések: {softCount}",
            "",
            "Összesítés dolgozónként / időtípusonként:",
            string.Join(Environment.NewLine, summaryRows.Select(x =>
                $" - {x.EmployeeDisplayName} | {x.TimeTypeName} | {x.Hours:0.##} óra | {x.LocationNames}"))
        });

        _lblScheduleStatus.Text = $"Státusz: {schedule.Status.ToDisplayText()}";
    }

    private void LoadSettingsIntoControls()
    {
        _cmbDailySeverity.SelectedItem = _data.Settings.DailyHoursSeverity;
        _cmbMonthlySeverity.SelectedItem = _data.Settings.MonthlyHoursSeverity;
        _cmbPreferredSeverity.SelectedItem = _data.Settings.PreferredWindowSeverity;
        _cmbForbiddenSeverity.SelectedItem = _data.Settings.ForbiddenWindowSeverity;
        _cmbAllowedTimeTypeSeverity.SelectedItem = _data.Settings.AllowedTimeTypeSeverity;
        _cmbAllowedLocationSeverity.SelectedItem = _data.Settings.AllowedLocationSeverity;
        _cmbLeaveConflictSeverity.SelectedItem = _data.Settings.LeaveConflictSeverity;
    }

    private void SaveSettings()
    {
        _data.Settings.DailyHoursSeverity = (Severity)_cmbDailySeverity.SelectedItem!;
        _data.Settings.MonthlyHoursSeverity = (Severity)_cmbMonthlySeverity.SelectedItem!;
        _data.Settings.PreferredWindowSeverity = (Severity)_cmbPreferredSeverity.SelectedItem!;
        _data.Settings.ForbiddenWindowSeverity = (Severity)_cmbForbiddenSeverity.SelectedItem!;
        _data.Settings.AllowedTimeTypeSeverity = (Severity)_cmbAllowedTimeTypeSeverity.SelectedItem!;
        _data.Settings.AllowedLocationSeverity = (Severity)_cmbAllowedLocationSeverity.SelectedItem!;
        _data.Settings.LeaveConflictSeverity = (Severity)_cmbLeaveConflictSeverity.SelectedItem!;
        SaveData();
        MessageBox.Show(this, "A beállítások elmentve.", "Mentés", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void AddLocation()
    {
        using var dialog = new LocationEditorForm();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _data.Locations.Add(dialog.Model);
        SaveData();
        RefreshAllViews();
    }

    private void EditLocation()
    {
        var row = _locationsGrid.CurrentRow?.DataBoundItem as LocationGridRow;
        if (row is null) return;

        var model = _data.Locations.FirstOrDefault(x => x.Id == row.Id);
        if (model is null) return;

        using var dialog = new LocationEditorForm(model);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        model.Name = dialog.Model.Name;
        model.Address = dialog.Model.Address;
        model.IsActive = dialog.Model.IsActive;
        SaveData();
        RefreshAllViews();
    }

    private void DeleteLocation()
    {
        var row = _locationsGrid.CurrentRow?.DataBoundItem as LocationGridRow;
        if (row is null) return;

        if (_data.CoverageRules.Any(x => x.LocationId == row.Id) ||
            _data.Schedules.SelectMany(x => x.Entries).Any(x => x.LocationId == row.Id))
        {
            MessageBox.Show(this, "A telephely használatban van coverage szabályban vagy bejegyzésben, ezért nem törölhető.", "Nem törölhető", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var model = _data.Locations.FirstOrDefault(x => x.Id == row.Id);
        if (model is null) return;

        if (MessageBox.Show(this, $"Biztosan törlöd ezt a telephelyet?\n{model.Name}", "Megerősítés", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _data.Locations.Remove(model);
        SaveData();
        RefreshAllViews();
    }

    private void AddEmployee()
    {
        using var dialog = new EmployeeEditorForm(_data.Locations);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (dialog.Model.Role == EmployeeRole.PharmacyManager && _data.Employees.Any(x => x.Role == EmployeeRole.PharmacyManager))
        {
            MessageBox.Show(this, "Egyszerre csak egy gyógyszertárvezető lehet a prototípusban.", "Ütköző szerepkör", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _data.Employees.Add(dialog.Model);
        SaveData();
        RefreshAllViews();
    }

    private void EditEmployee()
    {
        var row = _employeesGrid.CurrentRow?.DataBoundItem as EmployeeGridRow;
        if (row is null) return;

        var model = _data.Employees.FirstOrDefault(x => x.Id == row.Id);
        if (model is null) return;

        using var dialog = new EmployeeEditorForm(_data.Locations, model);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (dialog.Model.Role == EmployeeRole.PharmacyManager && _data.Employees.Any(x => x.Id != model.Id && x.Role == EmployeeRole.PharmacyManager))
        {
            MessageBox.Show(this, "Egyszerre csak egy gyógyszertárvezető lehet a prototípusban.", "Ütköző szerepkör", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        model.FullName = dialog.Model.FullName;
        model.DisplayName = dialog.Model.DisplayName;
        model.BirthDate = dialog.Model.BirthDate;
        model.Role = dialog.Model.Role;
        model.MonthlyHoursLimit = dialog.Model.MonthlyHoursLimit;
        model.MaxDailyHours = dialog.Model.MaxDailyHours;
        model.PreferredWindows = dialog.Model.PreferredWindows;
        model.ForbiddenWindows = dialog.Model.ForbiddenWindows;
        model.IsActive = dialog.Model.IsActive;
        model.AllowedTimeTypes = dialog.Model.AllowedTimeTypes;
        model.AllowedLocationIds = dialog.Model.AllowedLocationIds;
        model.IncludeInAutoSchedule = dialog.Model.IncludeInAutoSchedule;
        model.AutoScheduleRoleOverride = dialog.Model.AutoScheduleRoleOverride;

        SaveData();
        RefreshAllViews();
    }

    private void DeleteEmployee()
    {
        var row = _employeesGrid.CurrentRow?.DataBoundItem as EmployeeGridRow;
        if (row is null) return;

        if (_data.Leaves.Any(x => x.EmployeeId == row.Id) ||
            _data.Schedules.SelectMany(x => x.Entries).Any(x => x.EmployeeId == row.Id))
        {
            MessageBox.Show(this, "A dolgozó távollétben vagy beosztásban szerepel, ezért nem törölhető.", "Nem törölhető", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var model = _data.Employees.FirstOrDefault(x => x.Id == row.Id);
        if (model is null) return;

        if (MessageBox.Show(this, $"Biztosan törlöd ezt a dolgozót?\n{model.DisplayName}", "Megerősítés", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _data.Employees.Remove(model);
        SaveData();
        RefreshAllViews();
    }

    private void AddLeave()
    {
        if (_data.Employees.Count == 0)
        {
            MessageBox.Show(this, "Előbb vegyél fel legalább egy dolgozót.", "Nincs dolgozó", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new LeaveEditorForm(_data.Employees);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _data.Leaves.Add(dialog.Model);
        SaveData();
        RefreshAllViews();
    }

    private void EditLeave()
    {
        var row = _leavesGrid.CurrentRow?.DataBoundItem as LeaveGridRow;
        if (row is null) return;

        var model = _data.Leaves.FirstOrDefault(x => x.Id == row.Id);
        if (model is null) return;

        using var dialog = new LeaveEditorForm(_data.Employees, model);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        model.EmployeeId = dialog.Model.EmployeeId;
        model.StartDate = dialog.Model.StartDate;
        model.EndDate = dialog.Model.EndDate;
        model.LeaveType = dialog.Model.LeaveType;
        model.Note = dialog.Model.Note;
        SaveData();
        RefreshAllViews();
    }

    private void DeleteLeave()
    {
        var row = _leavesGrid.CurrentRow?.DataBoundItem as LeaveGridRow;
        if (row is null) return;

        var model = _data.Leaves.FirstOrDefault(x => x.Id == row.Id);
        if (model is null) return;

        _data.Leaves.Remove(model);
        SaveData();
        RefreshAllViews();
    }

    private void AddCoverageRule()
    {
        if (_data.Locations.Count == 0)
        {
            MessageBox.Show(this, "Előbb vegyél fel legalább egy telephelyet.", "Nincs telephely", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new CoverageRuleEditorForm(_data.Locations);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _data.CoverageRules.Add(dialog.Model);
        SaveData();
        RefreshAllViews();
    }

    private void EditCoverageRule()
    {
        var row = _coverageGrid.CurrentRow?.DataBoundItem as CoverageGridRow;
        if (row is null) return;

        var model = _data.CoverageRules.FirstOrDefault(x => x.Id == row.Id);
        if (model is null) return;

        using var dialog = new CoverageRuleEditorForm(_data.Locations, model);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        model.LocationId = dialog.Model.LocationId;
        model.DayOfWeek = dialog.Model.DayOfWeek;
        model.Start = dialog.Model.Start;
        model.End = dialog.Model.End;
        model.Role = dialog.Model.Role;
        model.RequiredCount = dialog.Model.RequiredCount;
        model.Severity = dialog.Model.Severity;
        SaveData();
        RefreshAllViews();
    }

    private void DeleteCoverageRule()
    {
        var row = _coverageGrid.CurrentRow?.DataBoundItem as CoverageGridRow;
        if (row is null) return;

        var model = _data.CoverageRules.FirstOrDefault(x => x.Id == row.Id);
        if (model is null) return;

        _data.CoverageRules.Remove(model);
        SaveData();
        RefreshAllViews();
    }

    private void AddSchedule()
    {
        using var dialog = new ScheduleEditorForm();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _data.Schedules.Add(dialog.Model);
        SaveData();
        RefreshAllViews();
    }

    private void EditSchedule()
    {
        var schedule = CurrentSchedule;
        if (schedule is null) return;

        using var dialog = new ScheduleEditorForm(schedule);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        schedule.Name = dialog.Model.Name;
        schedule.PeriodStart = dialog.Model.PeriodStart;
        schedule.PeriodEnd = dialog.Model.PeriodEnd;
        schedule.Status = ScheduleStatus.Draft;
        SaveData();
        RefreshAllViews();
    }

    private void CopySchedule()
    {
        var schedule = CurrentSchedule;
        if (schedule is null) return;

        using var dialog = new ScheduleCopyForm(schedule.PeriodStart, schedule.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var offsetDays = dialog.NewStartDate.DayNumber - schedule.PeriodStart.DayNumber;
        var copied = new SchedulePlan
        {
            Name = dialog.NewName,
            PeriodStart = dialog.NewStartDate,
            PeriodEnd = schedule.PeriodEnd.AddDays(offsetDays),
            Status = ScheduleStatus.Draft,
            CreatedAt = DateTime.Now,
            CreatedBy = Environment.UserName,
            Entries = schedule.Entries.Select(x => new ShiftEntry
            {
                Id = Guid.NewGuid(),
                ScheduleId = Guid.Empty,
                EmployeeId = x.EmployeeId,
                LocationId = x.LocationId,
                Date = x.Date.AddDays(offsetDays),
                Start = x.Start,
                End = x.End,
                TimeType = x.TimeType,
                Note = x.Note
            }).ToList()
        };

        foreach (var entry in copied.Entries)
        {
            entry.ScheduleId = copied.Id;
        }

        _data.Schedules.Add(copied);
        SaveData();
        RefreshAllViews();
        _cmbSchedules.SelectedItem = copied;
    }

    private void DeleteSchedule()
    {
        var schedule = CurrentSchedule;
        if (schedule is null) return;

        if (MessageBox.Show(this, $"Biztosan törlöd ezt a beosztást?\n{schedule.DisplayTitle}", "Megerősítés", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _data.Schedules.Remove(schedule);
        SaveData();
        RefreshAllViews();
    }

    private void AddShift()
    {
        var schedule = CurrentSchedule;
        if (schedule is null)
        {
            MessageBox.Show(this, "Előbb hozz létre egy beosztást.", "Nincs beosztás", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_data.Employees.Count == 0 || _data.Locations.Count == 0)
        {
            MessageBox.Show(this, "Előbb vegyél fel dolgozót és telephelyet.", "Hiányzó törzsadat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new ShiftEditorForm(_data.Employees, _data.Locations);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        dialog.Model.ScheduleId = schedule.Id;
        schedule.Entries.Add(dialog.Model);
        schedule.Status = ScheduleStatus.Draft;
        SaveData();
        RefreshScheduleDetails();
    }

    private void EditShift()
    {
        var schedule = CurrentSchedule;
        var row = _shiftGrid.CurrentRow?.DataBoundItem as ShiftGridRow;
        if (schedule is null || row is null) return;

        var model = schedule.Entries.FirstOrDefault(x => x.Id == row.Id);
        if (model is null) return;

        using var dialog = new ShiftEditorForm(_data.Employees, _data.Locations, model);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        model.EmployeeId = dialog.Model.EmployeeId;
        model.LocationId = dialog.Model.LocationId;
        model.Date = dialog.Model.Date;
        model.Start = dialog.Model.Start;
        model.End = dialog.Model.End;
        model.TimeType = dialog.Model.TimeType;
        model.Note = dialog.Model.Note;
        schedule.Status = ScheduleStatus.Draft;
        SaveData();
        RefreshScheduleDetails();
    }

    private void DeleteShift()
    {
        var schedule = CurrentSchedule;
        var row = _shiftGrid.CurrentRow?.DataBoundItem as ShiftGridRow;
        if (schedule is null || row is null) return;

        var model = schedule.Entries.FirstOrDefault(x => x.Id == row.Id);
        if (model is null) return;

        schedule.Entries.Remove(model);
        schedule.Status = ScheduleStatus.Draft;
        SaveData();
        RefreshScheduleDetails();
    }

    private void AutoFillSchedule()
    {
        var schedule = CurrentSchedule;
        if (schedule is null) return;

        var count = _autoSchedulerService.FillCoverageGaps(_data, schedule);
        schedule.Status = ScheduleStatus.Draft;
        SaveData();
        RefreshScheduleDetails();

        MessageBox.Show(this, $"{count} darab bejegyzés jött létre automatikus kitöltéssel.", "Automatikus kitöltés", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ApproveSchedule()
    {
        var schedule = CurrentSchedule;
        if (schedule is null) return;

        RefreshScheduleDetails();

        if (_currentReport.HasBlockingIssues)
        {
            MessageBox.Show(this, "A beosztás nem hagyható jóvá, mert blokkoló szabálysértések vannak benne. Nézd meg az ellenőrzési listát.", "Jóváhagyás blokkolva", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        schedule.Status = ScheduleStatus.Approved;
        schedule.ApprovedAt = DateTime.Now;
        schedule.ApprovedBy = Environment.UserName;
        SaveData();
        RefreshScheduleDetails();

        MessageBox.Show(this, "A beosztás jóváhagyva.", "Siker", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportExcel()
    {
        var schedule = CurrentSchedule;
        if (schedule is null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = "Excel munkafüzet (*.xlsx)|*.xlsx",
            FileName = $"{SanitizeFileName(schedule.Name)}.xlsx"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _excelExportService.Export(_data, schedule, dialog.FileName);
        MessageBox.Show(this, "Az Excel export elkészült.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportPdf()
    {
        var schedule = CurrentSchedule;
        if (schedule is null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = "PDF fájl (*.pdf)|*.pdf",
            FileName = $"{SanitizeFileName(schedule.Name)}.pdf"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        RefreshScheduleDetails();
        _pdfExportService.Export(_data, schedule, _currentReport, dialog.FileName);
        MessageBox.Show(this, "A PDF export elkészült.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SaveData()
    {
        _store.Save(_data);
        _lblDataFile.Text = $"Adatfájl: {_store.FilePath}";
    }

    private void ReloadData()
    {
        _data = _store.Load();
        RefreshAllViews();
    }

    private void ResetToSampleData()
    {
        if (MessageBox.Show(this, "Biztosan visszaállítod a mintaadatokat? Ez felülírja a jelenlegi JSON állomány tartalmát.", "Megerősítés", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        _data = SampleDataFactory.Create();
        SaveData();
        RefreshAllViews();
    }

    private SchedulePlan? CurrentSchedule => _cmbSchedules.SelectedItem as SchedulePlan;

    private static DataGridView CreateReadOnlyGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            MultiSelect = false,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false
        };

        return grid;
    }

    private static void HideIdColumn(DataGridView grid)
    {
        if (grid.Columns.Contains("Id"))
        {
            grid.Columns["Id"]!.Visible = false;
        }
    }

    private static Button CreateButton(String text, EventHandler handler, int width = 108)
    {
        var button = new Button { Text = text, Width = width, Height = 30 };
        button.Click += handler;
        return button;
    }

    private static void BindSeverityCombo(ComboBox combo)
    {
        combo.DataSource = Enum.GetValues<Severity>();
        combo.Format += (_, e) =>
        {
            if (e.ListItem is Severity severity) e.Value = severity.ToDisplayText();
        };
    }

    private static string SanitizeFileName(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return string.Concat(input.Where(ch => !invalidChars.Contains(ch)));
    }
}
