param([string]$BasePath,[switch]$TestMode)
$ErrorActionPreference='Stop'
try {
 $payload=Join-Path $PSScriptRoot 'server.py'
 $expected=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'server.sha256') -Raw).Trim()
 if($expected -notmatch '^[a-f0-9]{64}$' -or (Get-FileHash -LiteralPath $payload -Algorithm SHA256).Hash -ne $expected){throw 'Repair checksum mismatch. Download and extract the ZIP again.'}
 $targets=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
 if($BasePath){[void]$targets.Add([IO.Path]::GetFullPath($BasePath))}
 if(-not $TestMode){
  $install=Join-Path $env:LOCALAPPDATA 'ArabicGamesLauncher'
  $current=Join-Path $install 'current.json'
  if(Test-Path -LiteralPath $current){try{$saved=Get-Content -LiteralPath $current -Raw|ConvertFrom-Json;if($saved.path){[void]$targets.Add([string]$saved.path)}}catch{}}
  $versions=Join-Path $install 'versions'
  if(Test-Path -LiteralPath $versions){foreach($dir in Get-ChildItem -LiteralPath $versions -Directory){[void]$targets.Add($dir.FullName)}}
  foreach($process in Get-Process -Name GameTranslator -ErrorAction SilentlyContinue){try{if($process.Path){[void]$targets.Add((Split-Path $process.Path))}}catch{}}
 }
 $valid=@($targets|Where-Object {(Test-Path -LiteralPath (Join-Path $_ 'GameTranslator.exe')) -and (Test-Path -LiteralPath (Join-Path $_ 'local-engine/server.py'))})
 if($valid.Count -eq 0 -and -not $TestMode){
  Add-Type -AssemblyName System.Windows.Forms
  $picker=New-Object System.Windows.Forms.FolderBrowserDialog
  $picker.Description='اختر مجلد مترجم الألعاب الذي يحتوي GameTranslator.exe'
  if($picker.ShowDialog() -ne [Windows.Forms.DialogResult]::OK){return}
  $valid=@($picker.SelectedPath)
 }
 if($valid.Count -eq 0){throw 'Existing translator folder not found.'}
 foreach($folder in $valid){
  $file=Join-Path $folder 'local-engine/server.py'
  if(-not(Test-Path -LiteralPath (Join-Path $folder 'GameTranslator.exe')) -or -not(Test-Path -LiteralPath $file)){throw 'Select the translator folder containing GameTranslator.exe.'}
  $backup=$file+'.before-unicode-fix'
  if(-not(Test-Path -LiteralPath $backup)){Copy-Item -LiteralPath $file -Destination $backup}
  Copy-Item -LiteralPath $payload -Destination $file -Force
  if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $expected){throw 'Installed repair checksum mismatch.'}
 }
 Write-Host ('Fixed '+$valid.Count+' installation(s). Restart QuickMT. Models and keys were not changed.')
 if(-not $TestMode){Add-Type -AssemblyName System.Windows.Forms;[void][Windows.Forms.MessageBox]::Show('تم إصلاح قراءة النموذج. أغلق نافذة QuickMT القديمة ثم شغّل المحرك مجددًا من التطبيق. النماذج والمفاتيح محفوظة.','إصلاح QuickMT')}
}catch{if($TestMode){throw};Add-Type -AssemblyName System.Windows.Forms;[void][Windows.Forms.MessageBox]::Show($_.Exception.Message,'تعذر تطبيق إصلاح QuickMT');exit 1}
