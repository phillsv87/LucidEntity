#!/usr/local/bin/pwsh
param(
    [string]$csOut="null",
    [string]$tsOut="null",
    [string]$collectionType="List",
    [string]$csv=$(throw "-csv required")
)

dotnet run --project "$PSScriptRoot/GenModel" -c Release -- `
    -csv "$csv" `
    -csOut "$csOut" `
    -tsOut "$tsOut" `
    -collectionType "$collectionType"