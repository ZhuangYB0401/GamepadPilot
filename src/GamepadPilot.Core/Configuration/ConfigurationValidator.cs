using GamepadPilot.Core.Domain;

namespace GamepadPilot.Core.Configuration;

public static class ConfigurationValidator
{
    public static void Normalize(AppConfiguration configuration)
    {
        ConfigurationMigration.Apply(configuration);
        configuration.Profiles ??= [];
        configuration.AppRules ??= [];
        configuration.Macros ??= [];
        configuration.Settings ??= new AppSettings();

        if (configuration.Profiles.Count == 0)
        {
            var defaults = DefaultConfigurationFactory.Create();
            configuration.Profiles = defaults.Profiles;
        }

        foreach (var profile in configuration.Profiles)
        {
            profile.Id = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id;
            profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "未命名配置" : profile.Name.Trim();
            profile.Bindings ??= [];
            profile.Combos ??= [];
            profile.LeftStick ??= new StickSettings();
            profile.RightStick ??= new StickSettings();
            profile.Bindings.RemoveAll(x => x.Action is null);
            profile.Combos.RemoveAll(x => x.Action is null || x.Buttons.Count == 0);
        }

        foreach (var macro in configuration.Macros)
        {
            macro.Id = macro.Id == Guid.Empty ? Guid.NewGuid() : macro.Id;
            macro.Name = string.IsNullOrWhiteSpace(macro.Name) ? "未命名宏" : macro.Name.Trim();
            macro.Steps ??= [];
            macro.RepeatCount = Math.Max(1, macro.RepeatCount);
            macro.Speed = Math.Clamp(macro.Speed, 0.1d, 10d);
        }

        var validProfiles = configuration.Profiles.Select(x => x.Id).ToHashSet();
        if (!validProfiles.Contains(configuration.DefaultProfileId))
        {
            configuration.DefaultProfileId = configuration.Profiles[0].Id;
        }

        if (!validProfiles.Contains(configuration.ActiveProfileId))
        {
            configuration.ActiveProfileId = configuration.DefaultProfileId;
        }

        configuration.AppRules.RemoveAll(x => !validProfiles.Contains(x.ProfileId));
        for (var i = 0; i < configuration.AppRules.Count; i++)
        {
            configuration.AppRules[i].Order = i;
            if (configuration.AppRules[i].Id == Guid.Empty)
            {
                configuration.AppRules[i].Id = Guid.NewGuid();
            }
        }

        configuration.Settings.TypingWheelDirectionThreshold = Math.Clamp(configuration.Settings.TypingWheelDirectionThreshold, 0.15d, 0.9d);
        configuration.Settings.TypingWheelTriggerThreshold = Math.Clamp(configuration.Settings.TypingWheelTriggerThreshold, 0.2d, 0.95d);
        configuration.Settings.TypingWheelBackgroundColor = NormalizeColor(configuration.Settings.TypingWheelBackgroundColor);
        configuration.Settings.TypingWheelBackgroundOpacity = Math.Clamp(configuration.Settings.TypingWheelBackgroundOpacity, 0d, 1d);
        configuration.Settings.DoublePressWindowMs = Math.Clamp(configuration.Settings.DoublePressWindowMs, 120, 800);
        configuration.Settings.LongPressWindowMs = Math.Clamp(configuration.Settings.LongPressWindowMs, 250, 2000);
    }

    private static string NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "#1A2433";
        var text = value.Trim();
        if (text.Length == 7 && text[0] == '#' && text.Skip(1).All(Uri.IsHexDigit)) return text.ToUpperInvariant();
        if (text.Length == 6 && text.All(Uri.IsHexDigit)) return "#" + text.ToUpperInvariant();
        return "#1A2433";
    }
}
