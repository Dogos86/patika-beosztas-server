using PharmacyScheduler.Core.Models;

namespace PharmacyScheduler.WinForms.Dialogs;

public sealed class LocationEditorForm : Form
{
    private readonly TextBox _txtName = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txtAddress = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _chkActive = new() { Text = "Aktív telephely", Checked = true, AutoSize = true };

    public LocationEditorForm(Location? location = null)
    {
        Text = location is null ? "Új telephely" : "Telephely szerkesztése";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 480;
        Height = 220;

        Model = location is null
            ? new Location()
            : new Location
            {
                Id = location.Id,
                Name = location.Name,
                Address = location.Address,
                IsActive = location.IsActive
            };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(12),
            AutoSize = true
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Név:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_txtName, 1, 0);
        layout.Controls.Add(new Label { Text = "Cím:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_txtAddress, 1, 1);
        layout.Controls.Add(_chkActive, 1, 2);

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
        _txtAddress.Text = Model.Address;
        _chkActive.Checked = Model.IsActive;
    }

    public Location Model { get; }

    private void SaveBack()
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show(this, "A telephely neve kötelező.", "Hiányzó adat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        Model.Name = _txtName.Text.Trim();
        Model.Address = _txtAddress.Text.Trim();
        Model.IsActive = _chkActive.Checked;
    }
}
