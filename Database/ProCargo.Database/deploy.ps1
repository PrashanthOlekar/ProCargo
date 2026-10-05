<#
    ProCargo database deployment (Windows / SSMS users).
    Runs every script in order: 01-Database (master) -> 02..09 -> 11-Security -> [10-TestData].

    Examples
      .\deploy.ps1 -Server "localhost" -WithTestData                       # Windows authentication
      .\deploy.ps1 -Server "localhost,1433" -User sa -Password "<secret>"  # SQL authentication

    Requires sqlcmd (installed with SSMS, or 'winget install sqlcmd').
    -I turns QUOTED_IDENTIFIER ON (needed for filtered indexes); -b stops on the first error.
#>
param(
    [string]$Server = "localhost",
    [string]$Database = "ProCargo",
    [string]$User,
    [string]$Password,
    [switch]$WithTestData
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$auth = if ($User) { @("-U", $User, "-P", $Password) } else { @("-E") }

function Invoke-Script([string]$db, [string]$file) {
    Write-Host ">> [$db] $($file.Substring($root.Length + 1))"
    & sqlcmd -S $Server -d $db @auth -C -b -I -i $file
    if ($LASTEXITCODE -ne 0) { throw "Script failed: $file" }
}

Get-ChildItem "$root\01-Database\*.sql" | Sort-Object Name | ForEach-Object { Invoke-Script "master" $_.FullName }

$folders = "02-Schemas","03-Tables","04-Constraints","05-Indexes","06-Views","07-Functions","08-StoredProcedures","09-SeedData","11-Security"
if ($WithTestData) { $folders += "10-TestData" }

foreach ($folder in $folders) {
    Get-ChildItem "$root\$folder\*.sql" | Sort-Object Name | ForEach-Object { Invoke-Script $Database $_.FullName }
}

Write-Host "ProCargo database deployed to $Server/$Database" -ForegroundColor Green
