param([Parameter(Mandatory)][string]$Package)
$ErrorActionPreference='Stop'
$root=Join-Path $env:RUNNER_TEMP ('reading-fix-test-'+[Guid]::NewGuid().ToString('N'))
try{
 $baseline=Join-Path $root 'baseline';$install=Join-Path $root 'managed'
 [void](New-Item -ItemType Directory -Path $baseline -Force)
 foreach($name in @('GameTranslator.exe','GameTranslator.RemotePlay.dll','GameTranslator.TvInstall.dll','keep-model-cache.txt')){Set-Content -LiteralPath (Join-Path $baseline $name) -Value 'existing-file'}
 $old=Get-FileHash -LiteralPath (Join-Path $baseline 'GameTranslator.exe')
 $target=& (Join-Path $Package 'Reading-Fix.ps1') -BasePath $baseline -InstallRoot $install -TestMode
 if(-not(Test-Path -LiteralPath (Join-Path $target 'keep-model-cache.txt'))){throw 'Existing app resources were lost'}
 if((Get-FileHash -LiteralPath (Join-Path $baseline 'GameTranslator.exe')).Hash -ne $old.Hash){throw 'Original app was modified'}
 if((Get-Content -LiteralPath (Join-Path $install 'current.json') -Raw|ConvertFrom-Json).path -ne $target){throw 'New app path was not saved'}
 $before=Get-Content -LiteralPath (Join-Path $install 'current.json') -Raw
 $copy=Join-Path $root 'damaged';Copy-Item -LiteralPath $Package -Destination $copy -Recurse
 Add-Content -LiteralPath (Join-Path $copy 'files/GameTranslator.dll') -Value 'tampered'
 $rejected=$false
 try{& (Join-Path $copy 'Reading-Fix.ps1') -BasePath $baseline -InstallRoot $install -TestMode}catch{$rejected=$true}
 if(-not $rejected){throw 'Tampered repair was accepted'}
 if((Get-Content -LiteralPath (Join-Path $install 'current.json') -Raw) -ne $before){throw 'Failed patch changed the selected version'}
 Write-Host 'Reading fix integration and checksum rejection passed.'
}finally{if(Test-Path -LiteralPath $root){Remove-Item -LiteralPath $root -Recurse -Force}}
