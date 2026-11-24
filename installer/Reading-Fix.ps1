param([string]$BasePath,[string]$InstallRoot=(Join-Path $env:LOCALAPPDATA 'ArabicGamesLauncher'),[switch]$TestMode)
$ErrorActionPreference='Stop'
$allowed=@('GameTranslator.exe','GameTranslator.dll','GameTranslator.Core.dll','GameTranslator.Shell.dll','GameTranslator.Translation.dll','GameTranslator.RemotePlay.dll','GameTranslator.TvInstall.dll','gametranslator_native.dll','Native-LICENSES.txt','GameTranslator.deps.json','GameTranslator.runtimeconfig.json')
try {
    $manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'patch.json') -Raw | ConvertFrom-Json
    if($manifest.version -notmatch '^\d+\.\d+\.\d+$' -or @($manifest.files).Count -ne $allowed.Count){throw 'Invalid repair manifest.'}
    $seen=@{}
    foreach($entry in $manifest.files){
        if($entry.path -notin $allowed -or $seen.ContainsKey([string]$entry.path)){throw 'Unexpected repair file.'}
        $seen[[string]$entry.path]=$true
        $file=Join-Path (Join-Path $PSScriptRoot 'files') ([string]$entry.path)
        if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.sha256){throw 'Repair checksum mismatch. Extract the ZIP again.'}
    }
    $currentFile=Join-Path $InstallRoot 'current.json'
    $old=$null
    if(Test-Path -LiteralPath $currentFile){$old=Get-Content -LiteralPath $currentFile -Raw | ConvertFrom-Json}
    if(-not $BasePath -and $old){$BasePath=[string]$old.path}
    if(-not $BasePath){
        $running=@(Get-Process -Name GameTranslator -ErrorAction SilentlyContinue | Where-Object {$_.MainWindowTitle -like '*مترجم الألعاب بالعربية*'})
        if($running.Count -eq 1){$BasePath=Split-Path $running[0].Path}
    }
    if(-not $BasePath -and -not $TestMode){
        Add-Type -AssemblyName System.Windows.Forms
        $picker=New-Object System.Windows.Forms.FolderBrowserDialog
        $picker.Description='اختر مجلد النسخة الحالية الذي يحتوي GameTranslator.exe'
        if($picker.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK){$BasePath=$picker.SelectedPath}else{return}
    }
    if(-not $BasePath -or -not (Test-Path -LiteralPath (Join-Path $BasePath 'GameTranslator.exe'))){throw 'Select the existing app folder, not the repair folder.'}
    $BasePath=[IO.Path]::GetFullPath($BasePath).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if($old -and $old.version -match '^\d+\.\d+\.\d+$' -and [version]$old.version -gt [version]$manifest.version){throw 'A newer version is already installed.'}
    foreach($name in @('GameTranslator.RemotePlay.dll','GameTranslator.TvInstall.dll')){if(-not(Test-Path -LiteralPath (Join-Path $BasePath $name))){throw 'Existing app files are incomplete; use the complete installer instead.'}}
    $versions=Join-Path $InstallRoot 'versions'
    $target=Join-Path $versions ($manifest.version+'-fix-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
    if([IO.Path]::GetFullPath($target).StartsWith($BasePath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Cannot copy an app into its own subfolder.'}
    Write-Host 'Applying reading fix locally. No model or API key download is needed...'
    [void](New-Item -ItemType Directory -Path $target -Force)
    Get-ChildItem -LiteralPath $BasePath -Force | Copy-Item -Destination $target -Recurse -Force
    foreach($entry in $manifest.files){$destination=Join-Path $target $entry.path;[void](New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force);Copy-Item -LiteralPath (Join-Path (Join-Path $PSScriptRoot 'files') $entry.path) -Destination $destination -Force}
    foreach($entry in $manifest.files){if((Get-FileHash -LiteralPath (Join-Path $target $entry.path) -Algorithm SHA256).Hash -ne $entry.sha256){throw 'Installed file checksum mismatch.'}}
    if(-not $TestMode){
        foreach($app in @(Get-Process -Name GameTranslator -ErrorAction SilentlyContinue | Where-Object {$_.Path -and $_.Path.Equals((Join-Path $BasePath 'GameTranslator.exe'),[StringComparison]::OrdinalIgnoreCase)})){
            [void]$app.CloseMainWindow();if(-not $app.WaitForExit(8000)){throw 'Close the translator then run the repair again.'}
        }
        $shell=New-Object -ComObject WScript.Shell
        $shortcut=$shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'ArabicGames.lnk'))
        $shortcut.TargetPath=Join-Path $target 'GameTranslator.exe';$shortcut.WorkingDirectory=$target;$shortcut.IconLocation=$shortcut.TargetPath;$shortcut.Save()
    }
    if(Test-Path -LiteralPath $currentFile){Copy-Item -LiteralPath $currentFile -Destination (Join-Path $InstallRoot 'previous.json') -Force}
    @{version=[string]$manifest.version;path=$target}|ConvertTo-Json|Set-Content -LiteralPath ($currentFile+'.tmp') -Encoding UTF8
    Move-Item -LiteralPath ($currentFile+'.tmp') -Destination $currentFile -Force
    if($TestMode){Write-Output $target;return}
    Start-Process -FilePath (Join-Path $target 'GameTranslator.exe') -WorkingDirectory $target
} catch {
    if($TestMode){throw}
    Add-Type -AssemblyName System.Windows.Forms
    [void][System.Windows.Forms.MessageBox]::Show($_.Exception.Message,'تعذر تطبيق الإصلاح — النسخة السابقة محفوظة')
    exit 1
}
