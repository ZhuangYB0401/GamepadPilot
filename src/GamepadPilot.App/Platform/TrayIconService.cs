using System.Drawing;
using System.Windows.Forms;
using GamepadPilot.Core.Domain;

namespace GamepadPilot.App.Platform;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _profilesMenu;
    private readonly Dictionary<Guid, ToolStripMenuItem> _profileItems = [];

    public TrayIconService()
    {
        _enabledItem = new ToolStripMenuItem("启用映射", null, (_, _) => EnabledToggleRequested?.Invoke(this, EventArgs.Empty));
        _pauseItem = new ToolStripMenuItem("暂停映射", null, (_, _) => PauseToggleRequested?.Invoke(this, EventArgs.Empty));
        _profilesMenu = new ToolStripMenuItem("切换配置");

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开 GamepadPilot", null, (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(_profilesMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "GamepadPilot - 手柄领航",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? EnabledToggleRequested;
    public event EventHandler? PauseToggleRequested;
    public event EventHandler<Guid>? ProfileRequested;
    public event EventHandler? ExitRequested;

    public void Update(
        IReadOnlyList<ProfileDefinition> profiles,
        Guid activeProfileId,
        bool enabled,
        bool suspended,
        string tooltip)
    {
        _enabledItem.Checked = enabled;
        _enabledItem.Text = enabled ? "暂停映射" : "启用映射";
        _pauseItem.Enabled = enabled;
        _pauseItem.Checked = suspended;
        _pauseItem.Text = suspended ? "恢复映射" : "立即暂停";

        _profilesMenu.DropDownItems.Clear();
        _profileItems.Clear();
        foreach (var profile in profiles)
        {
            var item = new ToolStripMenuItem(profile.Name)
            {
                Checked = profile.Id == activeProfileId,
                Tag = profile.Id
            };
            item.Click += (_, _) => ProfileRequested?.Invoke(this, profile.Id);
            _profileItems[profile.Id] = item;
            _profilesMenu.DropDownItems.Add(item);
        }

        _notifyIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
    }

    public void ShowMessage(string title, string text)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
