#!/usr/local/bin/pwsh
param(
    [string]$csOut="null",
    [string]$tsOut="null",
    [string]$dbClass="null",
    [string]$dbInterface="null",
    [string]$dbClassNs="null",
    [string]$dbInterfaceNs="null",
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
$nl=' `'+"`n    "

$content="#!/usr/local/bin/pwsh`n"
$content+= `
    '&"$PSScriptRoot/'+$rel+'/ProcessModel.ps1"' + $nl  +`
    ' -csv "$PSScriptRoot/'+$csv+'"' + $nl + `
    ' -namespace '+$namespace + $nl + `
    ' -tsout '+$tsOut + $nl + `
    ' -csout '+$csOut + $nl + `
    ' -dbClass '+$dbClass + $nl + `
    ' -dbInterface '+$dbInterface + $nl + `
    ' -dbClassNs '+$dbClassNs + $nl + `
    ' -dbInterfaceNs '+$dbInterfaceNs + `
    "`n"

$content > $script
chmod +x $script