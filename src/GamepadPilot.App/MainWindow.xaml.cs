using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GamepadPilot.App.ViewModels;
using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Services;
using Microsoft.Win32;

namespace GamepadPilot.App;

public partial class MainWindow : Window
{
    private readonly AppController _controller;
    private bool _loading;
    private ControlBinding? _selectedBinding;
    private ComboBinding? _selectedCombo;
    private MacroDefinition? _selectedMacro;
    private MacroStep? _selectedMacroStep;
    private AppRule? _selectedRule;
    private KeyChord? _capturedActionChord;
    private KeyChord _toggleHotkey;
    private KeyChord _panicHotkey;
    private KeyChord _showHotkey;
    private KeyChord _recordHotkey;
    private string? _capturingTarget;

    public MainWindow(AppController controller)
    {
        _controller = controller;
        _toggleHotkey = controller.Configuration.Settings.ToggleHotkey.Clone();
        _panicHotkey = controller.Configuration.Settings.PanicHotkey.Clone();
        _showHotkey = controller.Configuration.Settings.ShowHotkey.Clone();
        _recordHotkey = controller.Configuration.Settings.RecordStopHotkey.Clone();
        InitializeComponent();
        DataContext = controller;
        InitializeOptions();
        WireControllerEvents();
        ConfigPathText.Text = controller.ConfigPath;
        RefreshAll();
        ShowPanel("overview");
    }

    public bool AllowClose { get; set; }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Topmost = true;
        Topmost = false;
    }

    private void WireControllerEvents()
    {
        _controller.StateChanged += (_, _) => RunOnUi(UpdateState);
        _controller.ConfigurationChanged += (_, _) => RunOnUi(() =>
        {
            if (!IsKeyboardFocusWithin)
            {
                RefreshAll();
            }
        });
        _controller.MacroStepRecorded += (_, _) => RunOnUi(RefreshMacroSteps);
    }

    private void RunOnUi(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.BeginInvoke(action);
        }
    }

    private void InitializeOptions()
    {
        SourceCombo.ItemsSource = ControlCatalog.AllControls.Select(x => new OptionItem<GamepadControl>(x, ControlCatalog.DisplayName(x))).ToList();
        TriggerCombo.ItemsSource = Enum.GetValues<TriggerMode>().Select(x => new OptionItem<TriggerMode>(x, DisplayFormat.Trigger(x))).ToList();
        ActionTypeCombo.ItemsSource = Enum.GetValues<ActionType>().Select(x => new OptionItem<ActionType>(x, ActionName(x))).ToList();
        MouseButtonCombo.ItemsSource = Enum.GetValues<MouseButtonKind>().Select(x => new OptionItem<MouseButtonKind>(x, MouseButtonName(x))).ToList();
        WheelCombo.ItemsSource = Enum.GetValues<MouseWheelDirection>().Select(x => new OptionItem<MouseWheelDirection>(x, WheelName(x))).ToList();
        VolumeCombo.ItemsSource = Enum.GetValues<VolumeDirection>().Select(x => new OptionItem<VolumeDirection>(x, VolumeName(x))).ToList();
        TypingWheelToggleCombo.ItemsSource = ControlCatalog.ButtonControls.Select(x => new OptionItem<GamepadControl>(x, ControlCatalog.DisplayName(x))).ToList();
        MacroRepeatCombo.ItemsSource = Enum.GetValues<MacroRepeatMode>().Select(x => new OptionItem<MacroRepeatMode>(x, RepeatName(x))).ToList();
    }

    private void RefreshAll()
    {
        _loading = true;
        try
        {
            var activeProfileId = _controller.Configuration.ActiveProfileId;
            MappingProfileCombo.ItemsSource = _controller.Configuration.Profiles;
            MappingProfileCombo.SelectedItem = _controller.Configuration.FindProfile(activeProfileId) ?? _controller.Configuration.Profiles.FirstOrDefault();
            MacroCombo.ItemsSource = _controller.Configuration.Macros;
            RuleProfileCombo.ItemsSource = _controller.Configuration.Profiles;
            ProfileList.ItemsSource = _controller.Configuration.Profiles;
            MacroList.ItemsSource = _controller.Configuration.Macros;
            ProfileList.SelectedItem = _controller.Configuration.FindProfile(activeProfileId) ?? _controller.Configuration.Profiles.FirstOrDefault();
            RefreshBindingRows();
            RefreshRules();
            LoadSettings();
            if (_selectedMacro is not null && _controller.Configuration.Macros.Contains(_selectedMacro))
            {
                MacroList.SelectedItem = _selectedMacro;
                LoadMacro(_selectedMacro);
            }
            else if (_controller.Configuration.Macros.Count > 0)
            {
                MacroList.SelectedIndex = 0;
                LoadMacro(_controller.Configuration.Macros[0]);
            }
        }
        finally
        {
            _loading = false;
        }
        UpdateState();
    }

    private void RefreshBindingRows()
    {
        var profile = _controller.Configuration.FindProfile(_controller.Configuration.ActiveProfileId);
        if (profile is null)
        {
            BindingsGrid.ItemsSource = null;
            CombosGrid.ItemsSource = null;
            return;
        }

        var selectedBindingId = _selectedBinding?.Id;
        var selectedComboId = _selectedCombo?.Id;
        var bindingRows = profile.Bindings.Select(x => new BindingRow(x)).ToList();
        var comboRows = profile.Combos.Select(x => new ComboRow(x)).ToList();
        BindingsGrid.ItemsSource = bindingRows;
        CombosGrid.ItemsSource = comboRows;
        if (selectedBindingId is { } bindingId)
        {
            BindingsGrid.SelectedItem = bindingRows.FirstOrDefault(x => x.Binding.Id == bindingId);
        }
        if (selectedComboId is { } comboId)
        {
            CombosGrid.SelectedItem = comboRows.FirstOrDefault(x => x.Binding.Id == comboId);
        }
    }

    private void RefreshRules()
    {
        var selectedId = _selectedRule?.Id;
        var rows = _controller.Configuration.AppRules
            .Select(x => new RuleRow(x, _controller.Configuration.FindProfile(x.ProfileId)?.Name ?? "缺失配置"))
            .ToList();
        RulesGrid.ItemsSource = rows;
        if (selectedId is { } id)
        {
            RulesGrid.SelectedItem = rows.FirstOrDefault(x => x.Rule.Id == id);
        }
    }

    private void UpdateState()
    {
        var snapshot = _controller.LastSnapshot;
        StatusBadge.Text = _controller.StatusText;
        StatusTextBlock.Text = _controller.StatusText;
        ControllerTextBlock.Text = _controller.ControllerText;
        AdminTextBlock.Text = _controller.IsAdministrator ? "管理员模式，可控制管理员程序" : "普通权限模式";
        EngineToggleButton.Content = _controller.IsEnabled ? "暂停映射" : "启用映射";
        PauseButton.Content = _controller.IsManuallyPaused ? "恢复映射" : "立即暂停";

        // The UI calculates stick values directly to keep the overview responsive.
        LeftXText.Text = Normalize(snapshot.LeftThumbX).ToString("0.00");
        LeftYText.Text = (-Normalize(snapshot.LeftThumbY)).ToString("0.00");
        RightXText.Text = Normalize(snapshot.RightThumbX).ToString("0.00");
        RightYText.Text = (-Normalize(snapshot.RightThumbY)).ToString("0.00");
        LeftTriggerText.Text = $"{snapshot.LeftTrigger / 255d:P0}";
        RightTriggerText.Text = $"{snapshot.RightTrigger / 255d:P0}";
        BackPaddle12Text.Text = $"{ButtonState(snapshot, GamepadControl.BackPaddle1)} / {ButtonState(snapshot, GamepadControl.BackPaddle2)}";
        BackPaddle34Text.Text = $"{ButtonState(snapshot, GamepadControl.BackPaddle3)} / {ButtonState(snapshot, GamepadControl.BackPaddle4)}";
    }

    private static string ButtonState(GamepadSnapshot snapshot, GamepadControl control) => snapshot.IsConnected && snapshot.IsDown(control) ? "按下" : "—";

    private static double Normalize(short value) => Math.Clamp(value >= 0 ? value / 32767d : value / 32768d, -1d, 1d);

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page })
        {
            ShowPanel(page);
        }
    }

    private void ShowPanel(string page)
    {
        OverviewPanel.Visibility = page == "overview" ? Visibility.Visible : Visibility.Collapsed;
        MappingPanel.Visibility = page == "mapping" ? Visibility.Visible : Visibility.Collapsed;
        MacrosPanel.Visibility = page == "macros" ? Visibility.Visible : Visibility.Collapsed;
        ProfilesPanel.Visibility = page == "profiles" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        HelpPanel.Visibility = page == "help" ? Visibility.Visible : Visibility.Collapsed;
        PageTitle.Text = page switch
        {
            "overview" => "总览",
            "mapping" => "按键映射",
            "macros" => "宏",
            "profiles" => "配置与自动规则",
            "settings" => "设置",
            "help" => "使用说明",
            _ => "GamepadPilot"
        };
        foreach (var button in new[] { NavOverview, NavMapping, NavMacros, NavProfiles, NavSettings, NavHelp })
        {
            button.Background = System.Windows.Media.Brushes.Transparent;
        }

        var selected = page switch
        {
            "overview" => NavOverview,
            "mapping" => NavMapping,
            "macros" => NavMacros,
            "profiles" => NavProfiles,
            "help" => NavHelp,
            _ => NavSettings
        };
        selected.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 56, 77));
    }

    private void ToggleEnabled_Click(object sender, RoutedEventArgs e) => _controller.ToggleEnabled();
    private void Pause_Click(object sender, RoutedEventArgs e) => _controller.ToggleManualPause();

    private void MappingProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || MappingProfileCombo.SelectedItem is not ProfileDefinition profile)
        {
            return;
        }

        _selectedBinding = null;
        _selectedCombo = null;
        _controller.SelectProfile(profile.Id, manual: true);
        RefreshBindingRows();
        ClearBindingEditor();
    }

    private void BindingsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || BindingsGrid.SelectedItem is not BindingRow row)
        {
            return;
        }

        _selectedBinding = row.Binding;
        _selectedCombo = null;
        CombosGrid.SelectedItem = null;
        LoadBinding(row.Binding);
    }

    private void CombosGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || CombosGrid.SelectedItem is not ComboRow row)
        {
            return;
        }

        _selectedCombo = row.Binding;
        _selectedBinding = null;
        BindingsGrid.SelectedItem = null;
        LoadCombo(row.Binding);
    }

    private void LoadBinding(ControlBinding binding)
    {
        _loading = true;
        BindingEditorTitle.Text = "编辑映射";
        SourceCombo.IsEnabled = true;
        SelectOption(SourceCombo, binding.Source);
        SelectOption(TriggerCombo, binding.Trigger);
        LoadAction(binding.Action);
        ComboNameBox.Text = string.Empty;
        ComboButtonsBox.Text = string.Empty;
        _loading = false;
    }

    private void LoadCombo(ComboBinding combo)
    {
        _loading = true;
        BindingEditorTitle.Text = "编辑组合";
        SourceCombo.IsEnabled = false;
        SelectOption(TriggerCombo, combo.Trigger);
        LoadAction(combo.Action);
        ComboNameBox.Text = combo.Name;
        ComboButtonsBox.Text = string.Join("+", combo.Buttons.Select(ControlCatalog.DisplayName));
        _loading = false;
    }

    private void LoadAction(ActionDefinition action)
    {
        SelectOption(ActionTypeCombo, action.Type);
        SelectOption(MouseButtonCombo, action.MouseButton);
        SelectOption(WheelCombo, action.WheelDirection);
        SelectOption(VolumeCombo, action.VolumeDirection);
        MacroCombo.SelectedItem = action.MacroId is { } id
            ? _controller.Configuration.Macros.FirstOrDefault(x => x.Id == id)
            : null;
        ScaleBox.Text = action.Scale.ToString("0.##", CultureInfo.InvariantCulture);
        CurveBox.Text = action.Curve.ToString("0.##", CultureInfo.InvariantCulture);
        ActionHoldCheck.IsChecked = action.Hold;
        ActionDoubleClickCheck.IsChecked = action.ClickCount >= 2;
        _capturedActionChord = action.Key?.Clone();
        KeyCaptureBox.Text = DisplayFormat.Chord(action.Key);
    }

    private void ClearBindingEditor()
    {
        _loading = true;
        BindingEditorTitle.Text = "编辑映射";
        SourceCombo.IsEnabled = true;
        SourceCombo.SelectedIndex = -1;
        TriggerCombo.SelectedIndex = -1;
        ActionTypeCombo.SelectedIndex = -1;
        MouseButtonCombo.SelectedIndex = -1;
        WheelCombo.SelectedIndex = -1;
        VolumeCombo.SelectedIndex = -1;
        MacroCombo.SelectedIndex = -1;
        KeyCaptureBox.Text = "点击后按键";
        _capturedActionChord = null;
        ScaleBox.Text = "1";
        CurveBox.Text = "1.5";
        ActionHoldCheck.IsChecked = false;
        ActionDoubleClickCheck.IsChecked = false;
        ComboNameBox.Text = string.Empty;
        ComboButtonsBox.Text = string.Empty;
        _loading = false;
    }

    private void ClearBindingSelection_Click(object sender, RoutedEventArgs e)
    {
        _selectedBinding = null;
        _selectedCombo = null;
        BindingsGrid.SelectedItem = null;
        CombosGrid.SelectedItem = null;
        ClearBindingEditor();
    }

    private void AddBinding_Click(object sender, RoutedEventArgs e)
    {
        var profile = ActiveProfile();
        if (profile is null) return;
        var binding = new ControlBinding
        {
            Source = GamepadControl.A,
            Trigger = TriggerMode.Press,
            Action = new ActionDefinition { Type = ActionType.None }
        };
        profile.Bindings.Add(binding);
        _selectedBinding = binding;
        _controller.CommitConfiguration();
        RefreshAll();
        BindingsGrid.SelectedItem = BindingsGrid.Items.Cast<BindingRow>().FirstOrDefault(x => x.Binding.Id == binding.Id);
    }

    private void DeleteBinding_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBinding is null || ActiveProfile() is not { } profile) return;
        profile.Bindings.RemoveAll(x => x.Id == _selectedBinding.Id);
        _selectedBinding = null;
        _controller.CommitConfiguration();
        RefreshAll();
        ClearBindingEditor();
    }

    private void SaveBinding_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBinding is null && _selectedCombo is null) return;
        var action = BuildAction();
        if (_selectedBinding is { } binding)
        {
            if (SourceCombo.SelectedItem is OptionItem<GamepadControl> source) binding.Source = source.Value;
            if (TriggerCombo.SelectedItem is OptionItem<TriggerMode> trigger) binding.Trigger = trigger.Value;
            binding.Action = action;
        }
        else if (_selectedCombo is { } combo)
        {
            combo.Name = string.IsNullOrWhiteSpace(ComboNameBox.Text) ? "组合按键" : ComboNameBox.Text.Trim();
            var buttons = ParseControls(ComboButtonsBox.Text);
            if (buttons.Count >= 2) combo.Buttons = buttons;
            if (TriggerCombo.SelectedItem is OptionItem<TriggerMode> trigger) combo.Trigger = trigger.Value;
            combo.Action = action;
        }
        _controller.CommitConfiguration();
        RefreshAll();
    }

    private ActionDefinition BuildAction()
    {
        var type = ActionTypeCombo.SelectedItem is OptionItem<ActionType> action ? action.Value : ActionType.None;
        var result = new ActionDefinition
        {
            Type = type,
            Key = _capturedActionChord?.Clone(),
            MouseButton = MouseButtonCombo.SelectedItem is OptionItem<MouseButtonKind> mouse ? mouse.Value : MouseButtonKind.Left,
            WheelDirection = WheelCombo.SelectedItem is OptionItem<MouseWheelDirection> wheel ? wheel.Value : MouseWheelDirection.Up,
            VolumeDirection = VolumeCombo.SelectedItem is OptionItem<VolumeDirection> volume ? volume.Value : VolumeDirection.Up,
            MacroId = (MacroCombo.SelectedItem as MacroDefinition)?.Id,
            Hold = ActionHoldCheck.IsChecked == true,
            ClickCount = ActionDoubleClickCheck.IsChecked == true ? 2 : 1,
            Scale = ParseDouble(ScaleBox.Text, 1d, 0.1d, 5d),
            Curve = ParseDouble(CurveBox.Text, 1.5d, 0.25d, 4d)
        };
        if (type is ActionType.Keyboard or ActionType.Media or ActionType.Browser or ActionType.System && result.Key is null)
        {
            result.Key = KeyChordFactory.Of(KeyChordFactory.KeyEnter);
        }
        return result;
    }

    private void AddCombo_Click(object sender, RoutedEventArgs e)
    {
        var profile = ActiveProfile();
        if (profile is null) return;
        var combo = new ComboBinding
        {
            Name = "A+B 组合",
            Buttons = [GamepadControl.A, GamepadControl.B],
            Trigger = TriggerMode.Press,
            Action = new ActionDefinition { Type = ActionType.None }
        };
        profile.Combos.Add(combo);
        _selectedCombo = combo;
        _controller.CommitConfiguration();
        RefreshAll();
        CombosGrid.SelectedItem = CombosGrid.Items.Cast<ComboRow>().FirstOrDefault(x => x.Binding.Id == combo.Id);
    }

    private void DeleteCombo_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCombo is null || ActiveProfile() is not { } profile) return;
        profile.Combos.RemoveAll(x => x.Id == _selectedCombo.Id);
        _selectedCombo = null;
        _controller.CommitConfiguration();
        RefreshAll();
        ClearBindingEditor();
    }

    private void ActionTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectedCombo is null && _selectedBinding is null) return;
        var type = ActionTypeCombo.SelectedItem is OptionItem<ActionType> option ? option.Value : ActionType.None;
        KeyCaptureBox.IsEnabled = type is ActionType.Keyboard or ActionType.Media or ActionType.Browser or ActionType.System;
        MouseButtonCombo.IsEnabled = type == ActionType.MouseButton;
        WheelCombo.IsEnabled = type == ActionType.MouseWheel;
        VolumeCombo.IsEnabled = type == ActionType.Volume;
        MacroCombo.IsEnabled = type == ActionType.Macro;
    }

    private void KeyCaptureBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        if (e.Key == Key.Escape)
        {
            _capturedActionChord = null;
            KeyCaptureBox.Text = "未设置";
            return;
        }

        var key = KeyInterop.VirtualKeyFromKey(e.Key);
        if (key == 0 || IsModifierKey(e.Key)) return;
        _capturedActionChord = new KeyChord
        {
            VirtualKey = (uint)key,
            Control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
            Alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt),
            Shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift),
            Windows = Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)
        };
        KeyCaptureBox.Text = DisplayFormat.Chord(_capturedActionChord);
    }

    private void Capture_GotFocus(object sender, RoutedEventArgs e)
    {
        _controller.SetUiInputCaptureActive(true);
        if (ReferenceEquals(sender, KeyCaptureBox))
        {
            _capturingTarget = "action";
            return;
        }

        if (sender is TextBox box)
        {
            _capturingTarget = box.Name;
            box.SelectAll();
        }
    }

    private void Capture_LostFocus(object sender, RoutedEventArgs e)
    {
        _controller.SetUiInputCaptureActive(false);
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        if (sender is not TextBox box || _capturingTarget is null) return;
        if (e.Key == Key.Escape)
        {
            box.Text = "未设置";
            SetHotkey(_capturingTarget, null);
            return;
        }

        var key = KeyInterop.VirtualKeyFromKey(e.Key);
        if (key == 0 || IsModifierKey(e.Key)) return;
        var chord = new KeyChord
        {
            VirtualKey = (uint)key,
            Control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control),
            Alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt),
            Shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift),
            Windows = Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)
        };
        box.Text = DisplayFormat.Chord(chord);
        SetHotkey(_capturingTarget, chord);
    }

    private void SetHotkey(string name, KeyChord? chord)
    {
        chord ??= new KeyChord();
        switch (name)
        {
            case nameof(ToggleHotkeyBox): _toggleHotkey = chord; break;
            case nameof(PanicHotkeyBox): _panicHotkey = chord; break;
            case nameof(ShowHotkeyBox): _showHotkey = chord; break;
            case nameof(RecordHotkeyBox): _recordHotkey = chord; break;
        }
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System;

    private void MacroList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || MacroList.SelectedItem is not MacroDefinition macro) return;
        LoadMacro(macro);
    }

    private void LoadMacro(MacroDefinition macro)
    {
        _selectedMacro = macro;
        _loading = true;
        MacroEditorTitle.Text = $"宏编辑 - {macro.Name}";
        MacroNameBox.Text = macro.Name;
        SelectOption(MacroRepeatCombo, macro.RepeatMode);
        MacroCountBox.Text = macro.RepeatCount.ToString(CultureInfo.InvariantCulture);
        MacroSpeedBox.Text = macro.Speed.ToString("0.##", CultureInfo.InvariantCulture);
        _loading = false;
        RefreshMacroSteps();
    }

    private void RefreshMacroSteps()
    {
        if (_selectedMacro is null)
        {
            MacroStepsGrid.ItemsSource = null;
            return;
        }

        var selectedIndex = _selectedMacroStep is null ? -1 : _selectedMacro.Steps.IndexOf(_selectedMacroStep);
        var rows = _selectedMacro.Steps.Select((step, index) => new MacroStepRow(index + 1, step)).ToList();
        MacroStepsGrid.ItemsSource = rows;
        if (selectedIndex >= 0 && selectedIndex < rows.Count)
        {
            MacroStepsGrid.SelectedIndex = selectedIndex;
        }
    }

    private void MacroStepsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MacroStepsGrid.SelectedItem is not MacroStepRow row) return;
        _selectedMacroStep = row.Step;
        StepValueBox.Text = row.Step.Type switch
        {
            MacroStepType.Delay => row.Step.DelayMs.ToString(CultureInfo.InvariantCulture),
            MacroStepType.MouseMove => $"{row.Step.DeltaX},{row.Step.DeltaY}",
            MacroStepType.MouseWheel => row.Step.WheelDelta.ToString(CultureInfo.InvariantCulture),
            _ => string.Empty
        };
    }

    private void AddMacro_Click(object sender, RoutedEventArgs e)
    {
        var macro = new MacroDefinition
        {
            Name = $"新宏 {_controller.Configuration.Macros.Count + 1}",
            Steps = [new MacroStep { Type = MacroStepType.Delay, DelayMs = 100 }]
        };
        _controller.Configuration.Macros.Add(macro);
        _selectedMacro = macro;
        _controller.CommitConfiguration();
        RefreshAll();
        MacroList.SelectedItem = macro;
    }

    private void DeleteMacro_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro is null) return;
        _controller.Configuration.Macros.RemoveAll(x => x.Id == _selectedMacro.Id);
        _selectedMacro = null;
        _selectedMacroStep = null;
        _controller.CommitConfiguration();
        RefreshAll();
    }

    private void SaveMacro_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro is null) return;
        _selectedMacro.Name = string.IsNullOrWhiteSpace(MacroNameBox.Text) ? "未命名宏" : MacroNameBox.Text.Trim();
        if (MacroRepeatCombo.SelectedItem is OptionItem<MacroRepeatMode> repeat) _selectedMacro.RepeatMode = repeat.Value;
        _selectedMacro.RepeatCount = ParseInt(MacroCountBox.Text, 2, 1, 10000);
        _selectedMacro.Speed = ParseDouble(MacroSpeedBox.Text, 1d, 0.1d, 10d);
        _controller.CommitConfiguration();
        RefreshAll();
    }

    private void RecordMacro_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro is null)
        {
            MessageBox.Show("请先新建或选择一个宏。", "GamepadPilot");
            return;
        }

        if (MessageBox.Show("开始录制前是否清空当前步骤？", "GamepadPilot", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _selectedMacro.Steps.Clear();
        }
        _controller.StartMacroRecording(_selectedMacro);
        RefreshMacroSteps();
    }

    private void StopRecordMacro_Click(object sender, RoutedEventArgs e) => _controller.StopMacroRecording();

    private void AddDelayStep_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro is null) return;
        var delay = ParseInt(StepValueBox.Text, 100, 0, 60000);
        _selectedMacro.Steps.Add(new MacroStep { Type = MacroStepType.Delay, DelayMs = delay });
        _controller.CommitConfiguration();
        RefreshMacroSteps();
    }

    private void AddMoveStep_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro is null) return;
        var parts = StepValueBox.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var x = parts.Length > 0 ? ParseInt(parts[0], 0, -5000, 5000) : 0;
        var y = parts.Length > 1 ? ParseInt(parts[1], 0, -5000, 5000) : 0;
        _selectedMacro.Steps.Add(new MacroStep { Type = MacroStepType.MouseMove, DeltaX = x, DeltaY = y });
        _controller.CommitConfiguration();
        RefreshMacroSteps();
    }

    private void DeleteMacroStep_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMacro is null || _selectedMacroStep is null) return;
        _selectedMacro.Steps.Remove(_selectedMacroStep);
        _selectedMacroStep = null;
        _controller.CommitConfiguration();
        RefreshMacroSteps();
    }

    private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ProfileList.SelectedItem is not ProfileDefinition profile) return;
        ProfileNameBox.Text = profile.Name;
    }

    private void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = new ProfileDefinition { Name = $"新配置 {_controller.Configuration.Profiles.Count + 1}" };
        _controller.Configuration.Profiles.Add(profile);
        _controller.CommitConfiguration();
        RefreshAll();
        ProfileList.SelectedItem = profile;
    }

    private void DuplicateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not ProfileDefinition source) return;
        var clone = CloneProfile(source);
        _controller.Configuration.Profiles.Add(clone);
        _controller.CommitConfiguration();
        RefreshAll();
        ProfileList.SelectedItem = clone;
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not ProfileDefinition profile || _controller.Configuration.Profiles.Count <= 1) return;
        if (profile.Id == _controller.Configuration.ActiveProfileId)
        {
            _controller.Configuration.ActiveProfileId = _controller.Configuration.Profiles.First(x => x.Id != profile.Id).Id;
        }
        _controller.Configuration.Profiles.RemoveAll(x => x.Id == profile.Id);
        _controller.Configuration.AppRules.RemoveAll(x => x.ProfileId == profile.Id);
        _controller.CommitConfiguration();
        RefreshAll();
    }

    private void SaveProfileName_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not ProfileDefinition profile) return;
        profile.Name = string.IsNullOrWhiteSpace(ProfileNameBox.Text) ? "未命名配置" : ProfileNameBox.Text.Trim();
        _controller.CommitConfiguration();
        RefreshAll();
    }

    private void SetDefaultProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is not ProfileDefinition profile) return;
        _controller.Configuration.DefaultProfileId = profile.Id;
        _controller.CommitConfiguration();
        RefreshAll();
    }

    private void RulesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || RulesGrid.SelectedItem is not RuleRow row) return;
        _selectedRule = row.Rule;
        RuleProcessBox.Text = row.Rule.ProcessName;
        RuleTitleBox.Text = row.Rule.TitleContains ?? string.Empty;
        RuleProfileCombo.SelectedItem = _controller.Configuration.FindProfile(row.Rule.ProfileId);
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        if (_controller.Configuration.Profiles.Count == 0) return;
        var rule = new AppRule
        {
            ProcessName = "notepad",
            ProfileId = RuleProfileCombo.SelectedItem is ProfileDefinition selected ? selected.Id : _controller.Configuration.DefaultProfileId,
            Order = _controller.Configuration.AppRules.Count
        };
        _controller.Configuration.AppRules.Add(rule);
        _selectedRule = rule;
        _controller.CommitConfiguration();
        RefreshAll();
        RulesGrid.SelectedItem = RulesGrid.Items.Cast<RuleRow>().FirstOrDefault(x => x.Rule.Id == rule.Id);
    }

    private void SaveRule_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRule is null) return;
        _selectedRule.ProcessName = Path.GetFileNameWithoutExtension(RuleProcessBox.Text.Trim());
        _selectedRule.TitleContains = string.IsNullOrWhiteSpace(RuleTitleBox.Text) ? null : RuleTitleBox.Text.Trim();
        if (RuleProfileCombo.SelectedItem is ProfileDefinition profile) _selectedRule.ProfileId = profile.Id;
        _controller.CommitConfiguration();
        RefreshAll();
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRule is null) return;
        _controller.Configuration.AppRules.RemoveAll(x => x.Id == _selectedRule.Id);
        _selectedRule = null;
        _controller.CommitConfiguration();
        RefreshAll();
    }

    private void LoadSettings()
    {
        var settings = _controller.Configuration.Settings;
        StartupCheck.IsChecked = settings.StartWithWindows;
        TypingWheelEnabledCheck.IsChecked = settings.TypingWheelEnabled;
        SelectOption(TypingWheelToggleCombo, settings.TypingWheelToggleControl);
        DoublePressBox.Text = settings.DoublePressWindowMs.ToString(CultureInfo.InvariantCulture);
        LongPressBox.Text = settings.LongPressWindowMs.ToString(CultureInfo.InvariantCulture);
        var profile = ActiveProfile();
        if (profile is not null)
        {
            LeftDeadZoneBox.Text = profile.LeftStick.DeadZone.ToString("0.##", CultureInfo.InvariantCulture);
            LeftSensitivityBox.Text = profile.LeftStick.Sensitivity.ToString("0", CultureInfo.InvariantCulture);
            LeftSmoothingBox.Text = profile.LeftStick.Smoothing.ToString("0.##", CultureInfo.InvariantCulture);
            LeftCurveBox.Text = profile.LeftStick.Curve.ToString("0.##", CultureInfo.InvariantCulture);
            RightScrollBox.Text = profile.RightStick.ScrollRate.ToString("0", CultureInfo.InvariantCulture);
            RightStickInvertCheck.IsChecked = profile.RightStick.InvertY;
        }
        ToggleHotkeyBox.Text = DisplayFormat.Chord(_toggleHotkey);
        PanicHotkeyBox.Text = DisplayFormat.Chord(_panicHotkey);
        ShowHotkeyBox.Text = DisplayFormat.Chord(_showHotkey);
        RecordHotkeyBox.Text = DisplayFormat.Chord(_recordHotkey);
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var settings = _controller.Configuration.Settings;
        settings.StartWithWindows = StartupCheck.IsChecked == true;
        settings.TypingWheelEnabled = TypingWheelEnabledCheck.IsChecked == true;
        settings.TypingWheelToggleControl = TypingWheelToggleCombo.SelectedItem is OptionItem<GamepadControl> toggle ? toggle.Value : GamepadControl.LeftThumb;
        settings.DoublePressWindowMs = ParseInt(DoublePressBox.Text, 250, 120, 800);
        settings.LongPressWindowMs = ParseInt(LongPressBox.Text, 550, 250, 2000);
        settings.ToggleHotkey = _toggleHotkey.Clone();
        settings.PanicHotkey = _panicHotkey.Clone();
        settings.ShowHotkey = _showHotkey.Clone();
        settings.RecordStopHotkey = _recordHotkey.Clone();
        var profile = ActiveProfile();
        if (profile is not null)
        {
            profile.LeftStick.DeadZone = ParseDouble(LeftDeadZoneBox.Text, 0.15d, 0d, 0.8d);
            profile.LeftStick.Sensitivity = ParseDouble(LeftSensitivityBox.Text, 1500d, 100d, 4000d);
            profile.LeftStick.Smoothing = ParseDouble(LeftSmoothingBox.Text, 0.05d, 0d, 0.5d);
            profile.LeftStick.Curve = ParseDouble(LeftCurveBox.Text, 1d, 0.5d, 3d);
            foreach (var binding in profile.Bindings.Where(x => x.Source == GamepadControl.LeftStick && x.Trigger == TriggerMode.Continuous && x.Action.Type == ActionType.MouseMove))
            {
                binding.Action.Curve = profile.LeftStick.Curve;
            }
            profile.RightStick.ScrollRate = ParseDouble(RightScrollBox.Text, 14d, 1d, 60d);
            profile.RightStick.InvertY = RightStickInvertCheck.IsChecked == true;
        }
        _controller.CommitConfiguration();
        MessageBox.Show("设置已保存。", "GamepadPilot");
    }

    private void ImportConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "GamepadPilot 配置 (*.json)|*.json|所有文件 (*.*)|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _controller.ImportConfiguration(dialog.FileName);
            RefreshAll();
            MessageBox.Show("配置已导入。", "GamepadPilot");
        }
        catch (Exception exception)
        {
            MessageBox.Show($"导入失败：{exception.Message}", "GamepadPilot", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "GamepadPilot 配置 (*.json)|*.json",
            FileName = $"GamepadPilot-{DateTime.Now:yyyyMMdd-HHmm}.json"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _controller.ExportConfiguration(dialog.FileName);
            MessageBox.Show("配置已导出。", "GamepadPilot");
        }
        catch (Exception exception)
        {
            MessageBox.Show($"导出失败：{exception.Message}", "GamepadPilot", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenConfigFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(Path.GetDirectoryName(_controller.ConfigPath)!);
    private void OpenLogFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GamepadPilot", "logs"));

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    private ProfileDefinition? ActiveProfile() => _controller.Configuration.FindProfile(_controller.Configuration.ActiveProfileId);

    private static void SelectOption<T>(System.Windows.Controls.ComboBox combo, T value)
    {
        combo.SelectedItem = combo.Items.Cast<OptionItem<T>>().FirstOrDefault(x => EqualityComparer<T>.Default.Equals(x.Value, value));
    }

    private static List<GamepadControl> ParseControls(string text)
    {
        var result = new List<GamepadControl>();
        foreach (var part in text.Split(['+', ',', '，'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var match = ControlCatalog.AllControls.FirstOrDefault(x =>
                string.Equals(ControlCatalog.DisplayName(x), part, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.ToString(), part, StringComparison.OrdinalIgnoreCase));
            if (match != default || ControlCatalog.AllControls.Contains(match))
            {
                result.Add(match);
            }
        }
        return result.Distinct().ToList();
    }

    private static ProfileDefinition CloneProfile(ProfileDefinition source) => new()
    {
        Name = $"{source.Name} - 副本",
        Bindings = source.Bindings.Select(x => new ControlBinding
        {
            Id = Guid.NewGuid(),
            Source = x.Source,
            Trigger = x.Trigger,
            Enabled = x.Enabled,
            Scale = x.Scale,
            Action = x.Action.Clone()
        }).ToList(),
        Combos = source.Combos.Select(x => new ComboBinding
        {
            Id = Guid.NewGuid(),
            Name = x.Name,
            Buttons = [.. x.Buttons],
            Trigger = x.Trigger,
            Enabled = x.Enabled,
            Action = x.Action.Clone()
        }).ToList(),
        LeftStick = new StickSettings
        {
            DeadZone = source.LeftStick.DeadZone,
            Sensitivity = source.LeftStick.Sensitivity,
            Curve = source.LeftStick.Curve,
            ScrollRate = source.LeftStick.ScrollRate,
            Smoothing = source.LeftStick.Smoothing
        },
        RightStick = new StickSettings
        {
            DeadZone = source.RightStick.DeadZone,
            Sensitivity = source.RightStick.Sensitivity,
            Curve = source.RightStick.Curve,
            ScrollRate = source.RightStick.ScrollRate,
            Smoothing = source.RightStick.Smoothing,
            InvertY = source.RightStick.InvertY
        }
    };

    private static int ParseInt(string text, int fallback, int min, int max) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, min, max) : fallback;

    private static double ParseDouble(string text, double fallback, double min, double max) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, min, max) : fallback;

    private static string ActionName(ActionType type) => type switch
    {
        ActionType.None => "未绑定",
        ActionType.Keyboard => "键盘组合键",
        ActionType.MouseButton => "鼠标按钮",
        ActionType.MouseWheel => "鼠标滚轮",
        ActionType.MouseMove => "鼠标移动",
        ActionType.Volume => "线性音量",
        ActionType.Media => "媒体键",
        ActionType.Browser => "浏览器键",
        ActionType.System => "系统键",
        ActionType.Macro => "运行宏",
        _ => type.ToString()
    };

    private static string MouseButtonName(MouseButtonKind button) => button switch
    {
        MouseButtonKind.Left => "左键",
        MouseButtonKind.Right => "右键",
        MouseButtonKind.Middle => "中键",
        MouseButtonKind.XButton1 => "鼠标侧键 1（后退）",
        MouseButtonKind.XButton2 => "鼠标侧键 2（前进）",
        _ => button.ToString()
    };

    private static string WheelName(MouseWheelDirection direction) => direction switch
    {
        MouseWheelDirection.Up => "向上",
        MouseWheelDirection.Down => "向下",
        MouseWheelDirection.Left => "向左",
        MouseWheelDirection.Right => "向右",
        _ => direction.ToString()
    };

    private static string VolumeName(VolumeDirection direction) => direction == VolumeDirection.Up ? "音量增加" : "音量降低";

    private static string RepeatName(MacroRepeatMode repeat) => repeat switch
    {
        MacroRepeatMode.Once => "执行一次",
        MacroRepeatMode.Count => "指定次数",
        MacroRepeatMode.WhileHeld => "按住循环",
        MacroRepeatMode.Toggle => "开关循环",
        _ => repeat.ToString()
    };

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (AllowClose) return;
        e.Cancel = true;
        Hide();
    }
}
