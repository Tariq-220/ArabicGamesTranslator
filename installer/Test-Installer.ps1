$ErrorActionPreference='Stop'
Add-Type -TypeDefinition 'namespace System.Windows.Forms {public static class MessageBox {public static string Last;public static int Show(string text,string title){Last=text;return 0;}}}'
function global:Add-Type { param($AssemblyName) }
$global:TestDesktop=Join-Path $PSScriptRoot 'test-desktop'
[void](New-Item -ItemType Directory -Path $global:TestDesktop -Force)
function global:Join-Path {param([string]$Path,[string]$ChildPath) if(-not $Path){$Path=$global:TestDesktop};Microsoft.PowerShell.Management\Join-Path -Path $Path -ChildPath $ChildPath}
function global:Get-Process {param($Name,$ErrorAction) @()}
function global:Start-Process {param($FilePath,$WorkingDirectory) $global:StartedExe=$FilePath}
function global:New-Object {param($ComObject)
 if($ComObject -ne 'WScript.Shell'){throw 'Unexpected COM class'}
 $shell=[pscustomobject]@{}
 $shell|Add-Member ScriptMethod CreateShortcut {param($path)
  $link=[pscustomobject]@{FullName=$path;TargetPath='';WorkingDirectory='';IconLocation=''}
  $link|Add-Member ScriptMethod Save {Set-Content -LiteralPath $this.FullName -Value $this.TargetPath}
  return $link
 }
 return $shell
}
$env:LOCALAPPDATA=Join-Path $PSScriptRoot 'test-user'
$payload=Join-Path $PSScriptRoot 'ArabicGames-Windows'
[void](New-Item -ItemType Directory -Path $payload -Force)
Set-Content -LiteralPath (Join-Path $payload 'GameTranslator.exe') -Value 'Fake executable; never executed'
Set-Content -LiteralPath (Join-Path $payload 'sample.dll') -Value 'sample'
$files=@(Get-ChildItem -LiteralPath $payload -File | ForEach-Object { @{path=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
@{version='0.4.7';files=$files}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Encoding UTF8
$script=Join-Path $PSScriptRoot 'Install.ps1'
$current=Join-Path $env:LOCALAPPDATA 'ArabicGamesLauncher/current.json'
& $script
if(-not (Test-Path -LiteralPath $current)){throw 'First install failed: '+[System.Windows.Forms.MessageBox]::Last}
$first=Get-Content -LiteralPath $current -Raw | ConvertFrom-Json
if(-not (Test-Path -LiteralPath (Join-Path $first.path 'sample.dll'))){throw 'Payload was not copied'}
Write-Output 'PASS: fresh version install, real file copying and pointer creation'
& $script
$second=Get-Content -LiteralPath $current -Raw | ConvertFrom-Json
if($first.path -eq $second.path){throw 'Existing version overwritten'}
if(-not (Test-Path -LiteralPath $first.path)){throw 'Previous version deleted'}
Write-Output 'PASS: reinstall preserves previous version and installs separately'
Set-Content -LiteralPath (Join-Path $payload 'sample.dll') -Value 'tampered'
& $script
$after=Get-Content -LiteralPath $current -Raw | ConvertFrom-Json
if($after.path -ne $second.path){throw 'Corrupt package changed current version'}
Write-Output 'PASS: checksum failure leaves installed version pointer unchanged'
& $script -Rollback
$rolled=Get-Content -LiteralPath $current -Raw | ConvertFrom-Json
if($rolled.path -ne $first.path){throw 'Rollback failed'}
Write-Output 'PASS: rollback restores previous pointer and shortcut target'
