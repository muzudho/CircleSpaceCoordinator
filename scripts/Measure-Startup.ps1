param(
    [string]$Executable = 'CircleSpaceCoordinator.Desktop.Windows/bin/Release/net10.0-windows/CircleSpaceCoordinator.Desktop.Windows.exe',
    [int]$Runs = 3
)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class StartupMeasurementWindow {
    public delegate bool Callback(IntPtr hwnd, IntPtr data);
    [DllImport("user32.dll")] public static extern bool EnumWindows(Callback callback, IntPtr data);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint id);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder text, int size);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
    public static bool Close(int pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hwnd, data) => {
            uint id; GetWindowThreadProcessId(hwnd, out id);
            var name = new StringBuilder(256); GetClassName(hwnd, name, name.Capacity);
            if (id == pid && name.ToString().StartsWith("SDL")) { found = hwnd; return false; }
            return true;
        }, IntPtr.Zero);
        return found != IntPtr.Zero && PostMessage(found, 0x0010, IntPtr.Zero, IntPtr.Zero);
    }
}
"@
$exe = (Resolve-Path -LiteralPath $Executable).Path
$appDirectory = Split-Path -Parent $exe
for ($trial = 1; $trial -le $Runs; $trial++) {
    $appProcess = Start-Process -FilePath $exe -WorkingDirectory $appDirectory -WindowStyle Hidden -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(60)
        $summary = $null
        do {
            Start-Sleep -Milliseconds 200
            $appProcess.Refresh()
            $log = Get-ChildItem -LiteralPath (Join-Path $appDirectory 'logs') -Filter "performance-*-$($appProcess.Id).jsonl" -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($log) {
                foreach ($line in (Get-Content -LiteralPath $log.FullName)) {
                    if ($line -notmatch '"kind":"startup_summary"') { continue }
                    $summary = $line | ConvertFrom-Json
                }
            }
            if ($appProcess.HasExited -and -not $summary) { throw 'Application exited without a startup summary.' }
        } while (-not $summary -and [DateTime]::UtcNow -lt $deadline)
        if (-not $summary) { throw 'Startup measurement timed out.' }
        if (-not $summary.success) { throw 'Startup failed; inspect the performance log.' }
        $thinkingLog = Get-ChildItem -LiteralPath (Join-Path $appDirectory 'logs') -Filter "thinking-startup-*-$($appProcess.Id)-*.json" | Select-Object -First 1
        $thinking = if ($thinkingLog) { Get-Content -Raw -LiteralPath $thinkingLog.FullName | ConvertFrom-Json } else { $null }
        [pscustomobject]@{ run = $trial; processId = $appProcess.Id; log = $log.FullName; summary = $summary; thinking = $thinking } | ConvertTo-Json -Depth 8 -Compress
        if (-not [StartupMeasurementWindow]::Close($appProcess.Id)) { throw 'Cannot request normal application close.' }
        if (-not $appProcess.WaitForExit(10000)) { throw 'Application did not close normally.' }
        if ($appProcess.ExitCode -ne 0) { throw "Application exit code: $($appProcess.ExitCode)" }
    }
    finally {
        # Only the process created by this trial is eligible for cleanup.
        if (-not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id; $appProcess.WaitForExit() }
        $appProcess.Dispose()
    }
}
