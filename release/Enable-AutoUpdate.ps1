$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$repo='Tariq-220/ArabicGamesTranslator'
$folder=Join-Path $env:LOCALAPPDATA ('ArabicGamesLauncher\bootstrap\'+[Guid]::NewGuid().ToString('N'))
try {
    [void](New-Item -ItemType Directory -Path $folder -Force)
    Write-Host 'Checking latest ArabicGames release...'
    $release=Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/latest" -Headers @{'User-Agent'='ArabicGames-Setup'}
    if($release.draft -or $release.prerelease -or $release.tag_name -notmatch '^v\d+\.\d+\.\d+$'){throw 'No stable release available.'}
    $version=([string]$release.tag_name).Substring(1)
    $name="ArabicGames-Windows-$version-Full-Install.zip"
    $asset=@($release.assets|Where-Object {$_.name -eq $name})
    if($asset.Count -ne 1 -or $asset[0].digest -notmatch '^sha256:[0-9a-fA-F]{64}$' -or $asset[0].size -lt 1 -or $asset[0].size -gt 1GB){throw 'Missing trusted release metadata.'}
    $url="https://github.com/$repo/releases/download/v$version/$name"
    if($asset[0].browser_download_url -cne $url){throw 'Unexpected download origin.'}
    Write-Host "Downloading ArabicGames $version automatically..."
    $zip=Join-Path $folder 'update.zip'
    $client=New-Object Net.WebClient
    $client.Headers.Add('User-Agent','ArabicGames-Setup')
    try{$client.DownloadFile($url,$zip)}finally{$client.Dispose()}
    if((Get-Item -LiteralPath $zip).Length -ne $asset[0].size -or (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne ([string]$asset[0].digest).Substring(7)){throw 'Downloaded update checksum mismatch.'}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive=[IO.Compression.ZipFile]::OpenRead($zip)
    try {
        if($archive.Entries.Count -gt 10000){throw 'Too many update files.'}
        [long]$total=0
        foreach($entry in $archive.Entries){
            $path=$entry.FullName.Replace('\','/')
            if($path.StartsWith('/') -or $path.Contains(':') -or ($path.Split('/') -contains '..')){throw 'Unsafe archive path.'}
            $total+=$entry.Length
            if($total -gt 2GB){throw 'Update package too large.'}
        }
        if(-not $archive.GetEntry('Install.ps1') -or -not $archive.GetEntry('manifest.json') -or -not $archive.GetEntry('ArabicGames-Windows/GameTranslator.exe')){throw 'Incomplete update package.'}
    } finally{$archive.Dispose()}
    $target=Join-Path $folder 'package'
    [IO.Compression.ZipFile]::ExtractToDirectory($zip,$target)
    $installer=Join-Path $target 'Install.ps1'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer
    if($LASTEXITCODE -ne 0){throw 'Installer did not complete. Your previous version is kept.'}
} catch {
    [void][System.Windows.Forms.MessageBox]::Show($_.Exception.Message,'تعذر تفعيل التحديث التلقائي')
    exit 1
}
