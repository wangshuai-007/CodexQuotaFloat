$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class ClientWindowProbe {
    [DllImport("user32.dll", CharSet=CharSet.Unicode)]
    public static extern int GetClassName(IntPtr handle, StringBuilder name, int count);
    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr handle, uint command);
}
'@
Get-Process -Name 'ChatGPT' -ErrorAction SilentlyContinue | ForEach-Object {
    if ($_.Path -match '[\\/]OpenAI\.Codex_[^\\/]+[\\/]app[\\/]ChatGPT\.exe$') {
        $name = New-Object System.Text.StringBuilder 256
        [void][ClientWindowProbe]::GetClassName($_.MainWindowHandle, $name, 256)
        [pscustomobject]@{ ProcessId=$_.Id; Handle=$_.MainWindowHandle; Class=$name.ToString(); Owner=[ClientWindowProbe]::GetWindow($_.MainWindowHandle,4) }
    }
}
