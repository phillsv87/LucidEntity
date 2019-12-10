#!/usr/local/bin/pwsh
param(
    [string]$csOut="null",
    [string]$tsOut="null",
    [string]$collectionType="List",
    [string]$csv=$(throw "-csv required"),
    [string]$namespace=$(throw "-namespace required"),
    [string]$script=$(throw "-script required")
)

$ErrorActionPreference="Stop"

if($csOut -ne 'null'){
    $csOut='"$PSScriptRoot/'+$csOut+'"'
}

if($tsOut -ne 'null'){
    $tsOut='"$PSScriptRoot/'+$tsOut+'"'
}

$rel=Resolve-Path -Relative $PSScriptRoot

$content="#!/usr/local/bin/pwsh`n"
$content+= `
    '&"$PSScriptRoot/'+$rel+'/ProcessModel.ps1"' + `
    ' -csv "$PSScriptRoot/'+$csv+'"'+ `
    ' -namespace '+$namespace+ `
    ' -tsout '+$tsOut+ `
    ' -csout '+$csOut+ `
    "`n"

$content > $script
chmod +x $script