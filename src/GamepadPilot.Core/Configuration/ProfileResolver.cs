using GamepadPilot.Core.Domain;
using GamepadPilot.Core.Services;

namespace GamepadPilot.Core.Configuration;

public sealed class ProfileResolver
{
    private Guid? _manualProfileId;
    private string _lastProcessName = string.Empty;

    public Guid Resolve(AppConfiguration configuration, ForegroundAppInfo? foreground)
    {
        var processName = foreground?.ProcessName ?? string.Empty;
        if (!string.Equals(_lastProcessName, processName, StringComparison.OrdinalIgnoreCase))
        {
            _manualProfileId = null;
            _lastProcessName = processName;
        }

        if (_manualProfileId is { } manualId && configuration.FindProfile(manualId) is not null)
        {
            return manualId;
        }

        if (foreground is not null)
        {
            var rule = configuration.AppRules
                .Where(x => x.Enabled && x.ProfileId != Guid.Empty)
                .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.TitleContains))
                .ThenBy(x => x.Order)
                .FirstOrDefault(x =>
                    string.Equals(Path.GetFileNameWithoutExtension(x.ProcessName), foreground.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(x.TitleContains) ||
                     foreground.WindowTitle.Contains(x.TitleContains, StringComparison.OrdinalIgnoreCase)));

            if (rule is not null && configuration.FindProfile(rule.ProfileId) is not null)
            {
                return rule.ProfileId;
            }
        }

        return configuration.FindProfile(configuration.DefaultProfileId) is not null
            ? configuration.DefaultProfileId
            : configuration.Profiles.FirstOrDefault()?.Id ?? Guid.Empty;
    }

    public void SelectManual(AppConfiguration configuration, Guid profileId)
    {
        if (configuration.FindProfile(profileId) is null)
        {
            return;
        }

        _manualProfileId = profileId;
        configuration.ActiveProfileId = profileId;
    }
}
