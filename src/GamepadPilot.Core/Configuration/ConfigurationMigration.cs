using GamepadPilot.Core.Domain;

namespace GamepadPilot.Core.Configuration;

public static class ConfigurationMigration
{
    public static void Apply(AppConfiguration configuration)
    {
        configuration.Profiles ??= [];
        configuration.AppRules ??= [];

        if (configuration.SchemaVersion < 2)
        {
            MigrateToVersion2(configuration);
            configuration.SchemaVersion = 2;
        }

        if (configuration.SchemaVersion < 3)
        {
            MigrateToVersion3(configuration);
            configuration.SchemaVersion = 3;
        }

        if (configuration.SchemaVersion < 4)
        {
            MigrateToVersion4(configuration);
            configuration.SchemaVersion = 4;
        }

        if (configuration.SchemaVersion < 5)
        {
            configuration.Settings ??= new AppSettings();
            configuration.Settings.TypingWheelEnabled = true;
            configuration.Settings.TypingWheelToggleControl = GamepadControl.LeftThumb;
            configuration.SchemaVersion = 5;
        }

        if (configuration.SchemaVersion < 6)
        {
            configuration.Settings ??= new AppSettings();
            configuration.Settings.AutoPauseFullscreen = false;
            configuration.Settings.TypingWheelBackgroundColor = "#1A2433";
            configuration.Settings.TypingWheelBackgroundOpacity = 0.88d;
            configuration.SchemaVersion = 6;
        }
    }

    private static void MigrateToVersion2(AppConfiguration configuration)
    {
        var desktop = FindDesktopProfile(configuration);
        if (desktop is not null)
        {
            MigrateDesktopProfileToVersion2(desktop);
        }

        var browserProfile = configuration.Profiles.FirstOrDefault(x => x.Id == DefaultConfigurationFactory.BrowserProfileId);
        if (browserProfile is null)
        {
            browserProfile = DefaultConfigurationFactory.BuildBrowserPreset();
            configuration.Profiles.Add(browserProfile);
        }

        AddBrowserRuleIfAbsent(configuration, browserProfile.Id, "chrome", 0);
        AddBrowserRuleIfAbsent(configuration, browserProfile.Id, "msedge", 1);
    }

    private static void MigrateToVersion3(AppConfiguration configuration)
    {
        var desktop = FindDesktopProfile(configuration);
        if (desktop is null)
        {
            return;
        }

        desktop.Bindings ??= [];
        AddDefaultIfMissing(desktop, GamepadControl.LeftBumper, Keyboard(KeyChordFactory.KeyBrowserBack));
        AddDefaultIfMissing(desktop, GamepadControl.RightBumper, Keyboard(KeyChordFactory.KeyBrowserForward));
        AddDefaultIfMissing(desktop, GamepadControl.LeftTrigger, Keyboard(KeyChordFactory.KeyPageUp));
        AddDefaultIfMissing(desktop, GamepadControl.RightTrigger, Keyboard(KeyChordFactory.KeyPageDown));
    }

    private static void MigrateToVersion4(AppConfiguration configuration)
    {
        foreach (var profile in configuration.Profiles.Where(x => x.Id == DefaultConfigurationFactory.DesktopProfileId || x.Id == DefaultConfigurationFactory.BrowserProfileId))
        {
            TuneLegacyMouseProfile(profile);
        }
    }

    private static void TuneLegacyMouseProfile(ProfileDefinition profile)
    {
        var stick = profile.LeftStick;
        if (stick is null || !Nearly(stick.DeadZone, 0.15d) || !Nearly(stick.Sensitivity, 1100d) || !Nearly(stick.Curve, 1.5d) || !Nearly(stick.Smoothing, 0.18d))
        {
            return;
        }

        stick.Sensitivity = 1500d;
        stick.Curve = 1d;
        stick.Smoothing = 0.05d;
        if (profile.RightStick is { } rightStick)
        {
            rightStick.Curve = 1d;
            rightStick.Smoothing = 0.05d;
            if (Nearly(rightStick.ScrollRate, 14d)) rightStick.ScrollRate = 16d;
        }

        foreach (var binding in profile.Bindings.Where(x => x.Source == GamepadControl.LeftStick && x.Trigger == TriggerMode.Continuous && x.Action.Type == ActionType.MouseMove && Nearly(x.Action.Curve, 1.5d)))
        {
            binding.Action.Curve = 1d;
        }
        foreach (var binding in profile.Bindings.Where(x => x.Source == GamepadControl.RightStick && x.Trigger == TriggerMode.Continuous && x.Action.Type == ActionType.MouseWheel && Nearly(x.Action.Curve, 1.5d)))
        {
            binding.Action.Curve = 1d;
        }
    }

    private static bool Nearly(double left, double right) => Math.Abs(left - right) < 0.0001d;

    private static ProfileDefinition? FindDesktopProfile(AppConfiguration configuration) =>
        configuration.Profiles.FirstOrDefault(x => x.Id == DefaultConfigurationFactory.DesktopProfileId)
        ?? configuration.Profiles.FirstOrDefault(x => x.IsSystem && x.Name == "桌面操控预设");

    private static void MigrateDesktopProfileToVersion2(ProfileDefinition profile)
    {
        profile.Bindings ??= [];
        profile.RightStick ??= new StickSettings();
        profile.RightStick.InvertY = true;
        foreach (var binding in profile.Bindings)
        {
            if (binding.Source is GamepadControl.A or GamepadControl.B or GamepadControl.X or GamepadControl.Y &&
                binding.Trigger == TriggerMode.Press &&
                IsOldDefault(binding))
            {
                binding.Trigger = TriggerMode.Hold;
            }
        }

        profile.Bindings.RemoveAll(binding => binding.Trigger == TriggerMode.Press && IsOldOptionalDefault(binding));
    }

    private static void AddDefaultIfMissing(ProfileDefinition profile, GamepadControl source, ActionDefinition action)
    {
        if (profile.Bindings.Any(x => x.Source == source))
        {
            return;
        }

        profile.Bindings.Add(new ControlBinding
        {
            Source = source,
            Trigger = TriggerMode.Press,
            Action = action
        });
    }

    private static ActionDefinition Keyboard(uint key) => new()
    {
        Type = ActionType.Keyboard,
        Key = KeyChordFactory.Of(key)
    };

    private static bool IsOldDefault(ControlBinding binding) => binding.Source switch
    {
        GamepadControl.A => IsMouseButton(binding.Action, MouseButtonKind.Left),
        GamepadControl.B => IsMouseButton(binding.Action, MouseButtonKind.Right),
        GamepadControl.X => IsKeyboard(binding.Action, KeyChordFactory.KeyEnter),
        GamepadControl.Y => IsKeyboard(binding.Action, KeyChordFactory.KeyEscape),
        _ => false
    };

    private static bool IsOldOptionalDefault(ControlBinding binding) => binding.Source switch
    {
        GamepadControl.LeftBumper => IsKeyboard(binding.Action, KeyChordFactory.KeyBrowserBack),
        GamepadControl.RightBumper => IsKeyboard(binding.Action, KeyChordFactory.KeyBrowserForward),
        GamepadControl.LeftTrigger => IsKeyboard(binding.Action, KeyChordFactory.KeyPageUp),
        GamepadControl.RightTrigger => IsKeyboard(binding.Action, KeyChordFactory.KeyPageDown),
        _ => false
    };

    private static bool IsKeyboard(ActionDefinition action, uint key) =>
        action.Type == ActionType.Keyboard && action.Key is { } chord && chord.VirtualKey == key;

    private static bool IsMouseButton(ActionDefinition action, MouseButtonKind button) =>
        action.Type == ActionType.MouseButton && action.MouseButton == button;

    private static void AddBrowserRuleIfAbsent(AppConfiguration configuration, Guid profileId, string processName, int order)
    {
        if (configuration.AppRules.Any(x => string.Equals(Path.GetFileNameWithoutExtension(x.ProcessName), processName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        configuration.AppRules.Add(new AppRule
        {
            ProcessName = processName,
            ProfileId = profileId,
            Order = order
        });
    }
}
