using System.Diagnostics;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class WorkspaceAutoActionService
{
    public IEnumerable<WorkspaceAutoAction> DangerousActions(IEnumerable<WorkspaceAutoAction> actions)
        => actions.Where(action => action.Enabled && action.Kind is WorkspaceAutoActionKind.Sleep or WorkspaceAutoActionKind.Shutdown or WorkspaceAutoActionKind.Restart or WorkspaceAutoActionKind.SignOut);

    public void RunSafeActions(IEnumerable<WorkspaceAutoAction> actions, SettingsService settingsService)
    {
        foreach (var action in actions.Where(action => action.Enabled))
        {
            if (action.Kind is WorkspaceAutoActionKind.Sleep or WorkspaceAutoActionKind.Shutdown or WorkspaceAutoActionKind.Restart or WorkspaceAutoActionKind.SignOut)
                continue;

            Run(action, settingsService);
        }
    }

    public void RunDangerousAction(WorkspaceAutoAction action, SettingsService settingsService)
        => Run(action, settingsService);

    private static void Run(WorkspaceAutoAction action, SettingsService settingsService)
    {
        try
        {
            switch (action.Kind)
            {
                case WorkspaceAutoActionKind.OpenTarget:
                case WorkspaceAutoActionKind.OpenSettingsUri:
                    if (!string.IsNullOrWhiteSpace(action.Target))
                    {
                        var startInfo = new ProcessStartInfo { FileName = action.Target, UseShellExecute = true };
                        if (!string.IsNullOrWhiteSpace(action.Arguments))
                            startInfo.Arguments = action.Arguments;
                        if (!string.IsNullOrWhiteSpace(action.WorkingDirectory))
                            startInfo.WorkingDirectory = action.WorkingDirectory;
                        Process.Start(startInfo);
                    }
                    break;
                case WorkspaceAutoActionKind.Theme:
                    if (!string.IsNullOrWhiteSpace(action.Target))
                    {
                        settingsService.Settings.CurrentTheme = action.Target;
                        settingsService.Save();
                    }
                    break;
                case WorkspaceAutoActionKind.Volume:
                case WorkspaceAutoActionKind.Mute:
                    Process.Start(new ProcessStartInfo { FileName = "ms-settings:sound", UseShellExecute = true });
                    break;
                case WorkspaceAutoActionKind.Sleep:
                    Process.Start(new ProcessStartInfo { FileName = "rundll32.exe", Arguments = "powrprof.dll,SetSuspendState 0,1,0", UseShellExecute = true });
                    break;
                case WorkspaceAutoActionKind.Shutdown:
                    Process.Start(new ProcessStartInfo { FileName = "shutdown", Arguments = "/s /t 0", UseShellExecute = true });
                    break;
                case WorkspaceAutoActionKind.Restart:
                    Process.Start(new ProcessStartInfo { FileName = "shutdown", Arguments = "/r /t 0", UseShellExecute = true });
                    break;
                case WorkspaceAutoActionKind.SignOut:
                    Process.Start(new ProcessStartInfo { FileName = "shutdown", Arguments = "/l", UseShellExecute = true });
                    break;
            }
        }
        catch { }
    }
}
