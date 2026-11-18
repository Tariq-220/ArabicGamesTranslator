param([Parameter(Mandatory=$true)][string]$CertificateProfile,[string]$Target,[string]$TizenCommand='tizen')
$ErrorActionPreference='Stop'
# Keep this profile for later updates. Never upload the certificate or password to GitHub.
$project=Join-Path $PSScriptRoot 'project'
if(-not(Test-Path (Join-Path $project 'config.xml'))){throw 'Extract the complete Developer Kit before running this script.'}
& $TizenCommand build-web -- $project
if($LASTEXITCODE -ne 0){throw 'Tizen build failed. Install Tizen Studio and Samsung TV extensions first.'}
$result=Join-Path $project '.buildResult'
& $TizenCommand package -t wgt -s $CertificateProfile -- $result
if($LASTEXITCODE -ne 0){throw 'Signing failed. Check the Samsung certificate profile and TV DUID in Certificate Manager.'}
$package=Get-ChildItem $result -Filter '*.wgt'|Select-Object -First 1
if(-not $package){throw 'No signed WGT was produced.'}
Write-Output ('Signed package: '+$package.FullName)
if($Target){
 & $TizenCommand install -t $Target --name $package.Name -- $package.DirectoryName
 if($LASTEXITCODE -ne 0){throw 'Install failed. Check Developer Mode, PC IP, device connection and certificate DUID.'}
}
