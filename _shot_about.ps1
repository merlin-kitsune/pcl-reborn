
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$code = @"
using System;
using System.Runtime.InteropServices;
public class W {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
}
"@
Add-Type -TypeDefinition $code
$exe = 'F:\MCProject\PCL\Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe'
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 12
$p = Get-Process -Name 'Plain Craft Launcher 2' | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$h = $p.MainWindowHandle
function Click($x,$y){ $lp = [IntPtr](($y -shl 16) -bor $x); [W]::SendMessage($h,0x0201,[IntPtr]1,$lp) | Out-Null; Start-Sleep -Milliseconds 80; [W]::SendMessage($h,0x0202,[IntPtr]0,$lp) | Out-Null }
Click 595 41
Start-Sleep -Milliseconds 1500
Click 65 129
Start-Sleep -Milliseconds 2000
$r = New-Object W+RECT
[W]::GetWindowRect($h, [ref]$r) | Out-Null
$w = $r.Right-$r.Left; $hh = $r.Bottom-$r.Top
$bmp = New-Object System.Drawing.Bitmap([int]$w, [int]$hh)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[W]::PrintWindow($h, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc); $g.Dispose()
$bmp.Save('F:\MCProject\_pcl_verify\about_page.png', [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output ("saved " + $w + "x" + $hh)
Stop-Process -Id $p.Id -Force
