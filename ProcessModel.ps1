#!/usr/local/bin/pwsh
param(
    [string]$csOut="null",
    [string]$tsOut="null",
    [string]$dbHooksOut="null",
    [string]$tsHeader="null",
    [string]$dbClass="null",
    [string]$dbInterface="null",
    [string]$dbClassNs="null",
    [string]$dbInterfaceNs="null",
    [string]$collectionType="List",
    [string]$jsonNav="true",
    [string]$csv=$(throw "-csv required"),
    [string]$namespace=$(throw "-namespace required"),
    [string]$firestore="false",
    [string]$uidInterface="null",
    [string]$uidProp="Uid",
    [string]$autoICreated="false",
    [string]$autoICompleted="false"
)

dotnet run --project "$PSScriptRoot/GenModel" -c Release -- `
    -csv "$csv" `
    -namespace "$namespace" `
    -csOut "$csOut" `
    -tsOut "$tsOut" `
    -dbHooksOut "$dbHooksOut" `
    -tsHeader "$tsHeader" `
    -dbClass "$dbClass" `
    -dbInterface "$dbInterface" `
    -dbClassNs "$dbClassNs" `
    -dbInterfaceNs "$dbInterfaceNs" `
    -collectionType "$collectionType" `
    -jsonNav "$jsonNav" `
    -firestore "$firestore" `
    -uidInterface "$uidInterface" `
    -uidProp "$uidProp" `
    -autoICreated "$autoICreated" `
    -autoICompleted "$autoICompleted"