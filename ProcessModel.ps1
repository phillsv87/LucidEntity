#!/usr/local/bin/pwsh
param(
    [string]$csOut="null",
    [string]$tsOut="null",
    [string]$collectionType="List",
    [string]$csv=$(throw "-csv required"),
    [string]$namespace=$(throw "-namespace required")
)

dotnet run --project "$PSScriptRoot/GenModel" -c Release -- `
    -csv "$csv" `
    -namespace "$namespace" `
    -csOut "$csOut" `
    -tsOut "$tsOut" `
    -collectionType "$collectionType"