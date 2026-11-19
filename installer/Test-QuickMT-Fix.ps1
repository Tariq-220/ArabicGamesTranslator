param([Parameter(Mandatory)][string]$Package)
$ErrorActionPreference='Stop'
$root=Join-Path $env:RUNNER_TEMP ('quickmt-path-'+[Guid]::NewGuid().ToString('N'))
try{
 $base=Join-Path $root 'اسم عربي'
 [void](New-Item -ItemType Directory -Path (Join-Path $base 'local-engine') -Force)
 Set-Content -LiteralPath (Join-Path $base 'GameTranslator.exe') -Value 'existing'
 Set-Content -LiteralPath (Join-Path $base 'local-engine/server.py') -Value 'old'
 Set-Content -LiteralPath (Join-Path $base 'local-engine/token') -Value 'keep'
 & (Join-Path $Package 'Fix-QuickMT.ps1') -BasePath $base -TestMode
 if((Get-Content -LiteralPath (Join-Path $base 'local-engine/token') -Raw).Trim() -ne 'keep'){throw 'Token changed'}
 if(-not(Test-Path -LiteralPath (Join-Path $base 'local-engine/server.py.before-unicode-fix'))){throw 'No backup'}
 if((Get-FileHash -LiteralPath (Join-Path $base 'local-engine/server.py')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $Package 'server.py')).Hash){throw 'Repair not applied'}
 Add-Content -LiteralPath (Join-Path $Package 'server.py') -Value 'tampered'
 $rejected=$false
 try{& (Join-Path $Package 'Fix-QuickMT.ps1') -BasePath $base -TestMode}catch{$rejected=$true}
 if(-not $rejected){throw 'Tampered repair accepted'}
 Write-Host 'Unicode repair, backup preservation and tamper rejection passed.'
}finally{if(Test-Path -LiteralPath $root){Remove-Item -LiteralPath $root -Recurse -Force}}
