
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$tmp = 'F:\MCProject\_pcl_zip'
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
New-Item -ItemType Directory -Path $tmp | Out-Null
Copy-Item 'F:\MCProject\PCL\Plain Craft Launcher 2\bin\Plain Craft Launcher 2.exe' (Join-Path $tmp 'Plain Craft Launcher 2.exe')
$out = 'F:\MCProject\PCL\最新正式版.zip'
if (Test-Path $out) { Remove-Item $out -Force }
[System.IO.Compression.ZipFile]::CreateFromDirectory($tmp, $out, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Remove-Item $tmp -Recurse -Force
$z=[System.IO.Compression.ZipFile]::OpenRead($out)
$z.Entries | ForEach-Object { Write-Output ($_.FullName + ' | ' + $_.Length) }
$z.Dispose()
Write-Output ('zip bytes ' + (Get-Item $out).Length)
