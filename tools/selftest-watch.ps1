# Self-test helper. Start it right after launching the game with the self-test flag file in place:
#   powershell -File tools\selftest-watch.ps1 -GameDir "C:\path\to\FRUKT"
# Tails MelonLoader's log while the self-test runs and acts on its markers:
#   [SelfTest] SCREENSHOT <name>            -> screenshot of the primary screen
#   [SelfTest] CLICK <name> <fx> <fy>       -> left click at a fraction of FRUKT's client area (top-left origin)
#   [SelfTest] WHEEL <name> <notches>       -> mouse wheel (negative = down)
#   [SelfTest] KEY <name> <virtual-key>     -> key press
# Input is only sent while FRUKT is the foreground window. Exits when the test finishes or the game closes.
param(
    [string]$GameDir = "D:\SteamLibrary\steamapps\common\FRUKT",
    [string]$OutDir = "$PSScriptRoot\shots"
)
New-Item -ItemType Directory -Force $OutDir | Out-Null
Add-Type -AssemblyName System.Drawing, System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class W32 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, int data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, UIntPtr extra);
    [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
}
"@
[W32]::SetProcessDPIAware() | Out-Null
$log = Join-Path $GameDir "MelonLoader\Latest.log"
$done = @{}
$start = Get-Date

function Get-GameWindow {
    $fg = [W32]::GetForegroundWindow()
    $procId = 0
    [W32]::GetWindowThreadProcessId($fg, [ref]$procId) | Out-Null
    $game = Get-Process FRUKT -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $game -or $game.Id -ne $procId) { return $null }
    return $fg
}

Start-Sleep -Seconds 15
while (((Get-Date) - $start).TotalMinutes -lt 7) {
    Start-Sleep -Milliseconds 250
    $text = $null
    try {
        $fs = [System.IO.File]::Open($log, 'Open', 'Read', 'ReadWrite')
        $reader = New-Object System.IO.StreamReader($fs)
        $text = $reader.ReadToEnd()
        $reader.Close()
    } catch { continue }
    foreach ($m in [regex]::Matches($text, '\[SelfTest\] (SCREENSHOT|CLICK|WHEEL|KEY) ([\w-]+)(?: (-?[\d.]+))?(?: (-?[\d.]+))?')) {
        $kind = $m.Groups[1].Value
        $name = $m.Groups[2].Value
        $id = "$kind $name"
        if ($done.ContainsKey($id)) { continue }
        $done[$id] = $true
        Start-Sleep -Milliseconds 300
        if ($kind -eq 'SCREENSHOT') {
            $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
            $bmp = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
            $path = Join-Path $OutDir "$name.png"
            $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
            $g.Dispose(); $bmp.Dispose()
            "captured $path"
            continue
        }
        $hwnd = Get-GameWindow
        if (-not $hwnd) { "skipped $id (FRUKT is not the foreground window)"; continue }
        if ($kind -eq 'CLICK') {
            $rect = New-Object W32+RECT
            [W32]::GetClientRect($hwnd, [ref]$rect) | Out-Null
            $origin = New-Object W32+POINT
            [W32]::ClientToScreen($hwnd, [ref]$origin) | Out-Null
            $x = [int]($origin.X + [double]$m.Groups[3].Value * ($rect.R - $rect.L))
            $y = [int]($origin.Y + [double]$m.Groups[4].Value * ($rect.B - $rect.T))
            [W32]::SetCursorPos($x, $y) | Out-Null
            Start-Sleep -Milliseconds 120
            [W32]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero) # left down
            Start-Sleep -Milliseconds 80
            [W32]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero) # left up
            "clicked $name at $x,$y"
        } elseif ($kind -eq 'WHEEL') {
            $notches = [int]$m.Groups[3].Value
            for ($i = 0; $i -lt [Math]::Abs($notches); $i++) {
                [W32]::mouse_event(0x0800, 0, 0, 120 * [Math]::Sign($notches), [UIntPtr]::Zero)
                Start-Sleep -Milliseconds 80
            }
            "wheel $name $notches"
        } elseif ($kind -eq 'KEY') {
            $vk = [byte][int]$m.Groups[3].Value
            $scan = [byte][W32]::MapVirtualKey($vk, 0)
            [W32]::keybd_event($vk, $scan, 0, [UIntPtr]::Zero)
            Start-Sleep -Milliseconds 80
            [W32]::keybd_event($vk, $scan, 2, [UIntPtr]::Zero)
            "key $name"
        }
    }
    if ($text -match '\[SelfTest\] Done') { "test finished"; break }
    if (((Get-Date) - $start).TotalSeconds -gt 40 -and -not (Get-Process FRUKT -ErrorAction SilentlyContinue)) { "game closed"; break }
}
