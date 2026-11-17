param([switch]$Rollback)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$root = Join-Path $env:LOCALAPPDATA 'ArabicGamesLauncher'
$versions = Join-Path $root 'versions'
$currentFile = Join-Path $root 'current.json'
$previousFile = Join-Path $root 'previous.json'
function Close-Translator {
    $apps = @(Get-Process -Name GameTranslator -ErrorAction SilentlyContinue | Where-Object {
        $_.MainWindowTitle -like '*مترجم الألعاب بالعربية*' -or ($_.Path -and $_.Path.StartsWith($versions,[StringComparison]::OrdinalIgnoreCase))
    })
    foreach ($app in $apps) {
        [void]$app.CloseMainWindow()
        if (-not $app.WaitForExit(8000)) { throw 'أغلق نافذة المترجم ثم أعد تشغيل التثبيت. لن يتم إغلاق المتصفح أو أي برنامج آخر.' }
    }
}
function Write-Shortcut([string]$exe) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Desktop')) 'ArabicGames.lnk'))
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = Split-Path $exe
    $shortcut.IconLocation = $exe
    $shortcut.Save()
}
function Write-Current($record) {
    $temporary = "$currentFile.tmp"
    $record | ConvertTo-Json | Set-Content -LiteralPath $temporary -Encoding UTF8
    Move-Item -LiteralPath $temporary -Destination $currentFile -Force
}
try {
    [void](New-Item -ItemType Directory -Path $versions -Force)
    Close-Translator
    if ($Rollback) {
        if (-not (Test-Path -LiteralPath $previousFile)) { throw 'لا توجد نسخة سابقة مثبتة بهذه الأداة للرجوع إليها.' }
        $previous = Get-Content -LiteralPath $previousFile -Raw | ConvertFrom-Json
        $oldPath = [IO.Path]::GetFullPath([string]$previous.path)
        if (-not $oldPath.StartsWith(([IO.Path]::GetFullPath($versions)+[IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)) { throw 'مسار النسخة السابقة غير صالح.' }
        $exe = Join-Path $oldPath 'GameTranslator.exe'
        if (-not (Test-Path -LiteralPath $exe)) { throw 'ملفات النسخة السابقة غير موجودة.' }
        Write-Shortcut $exe
        Write-Current $previous
        Start-Process -FilePath $exe -WorkingDirectory $oldPath
        exit 0
    }
    $source = Join-Path $PSScriptRoot 'ArabicGames-Windows'
    if (-not (Test-Path -LiteralPath (Join-Path $source 'GameTranslator.exe'))) { throw 'فك ضغط الحزمة كاملة أولًا. لا تشغّل المثبت من داخل ZIP.' }
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
    if ([string]$manifest.version -notmatch '^\d+\.\d+\.\d+$') { throw 'رقم الإصدار غير صالح.' }
    Write-Host 'Checking package integrity...'
    $base = [IO.Path]::GetFullPath($source)+[IO.Path]::DirectorySeparatorChar
    foreach ($entry in $manifest.files) {
        $file = [IO.Path]::GetFullPath((Join-Path $source ([string]$entry.path)))
        if (-not $file.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)) { throw 'مسار داخل الحزمة غير صالح.' }
        if (-not (Test-Path -LiteralPath $file)) { throw ('ملف ناقص: '+$entry.path) }
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.sha256) { throw ('فشل تحقق الملف: '+$entry.path+'. أعد فك ضغط الحزمة.') }
    }
    $target = Join-Path $versions ([string]$manifest.version)
    if (Test-Path -LiteralPath $target) { $target += '-'+[Guid]::NewGuid().ToString('N').Substring(0,8) }
    Write-Host 'Installing a new version; previous app folders are kept...'
    [void](New-Item -ItemType Directory -Path $target)
    Get-ChildItem -LiteralPath $source -Force | Copy-Item -Destination $target -Recurse -Force
    $exe = Join-Path $target 'GameTranslator.exe'
    Write-Shortcut $exe
    if (Test-Path -LiteralPath $currentFile) { Copy-Item -LiteralPath $currentFile -Destination $previousFile -Force }
    Write-Current @{ version=[string]$manifest.version; path=$target }
    [void][System.Windows.Forms.MessageBox]::Show('اكتمل التثبيت. افتح المترجم من اختصار ArabicGames على سطح المكتب. الإعدادات والمفاتيح والنماذج المحفوظة لم تُحذف.','مترجم الألعاب')
    Start-Process -FilePath $exe -WorkingDirectory $target
} catch {
    [void][System.Windows.Forms.MessageBox]::Show($_.Exception.Message,'لم يكتمل التثبيت')
    exit 1
}
