<#
    Starts ProCargo on a Windows PC for local testing: the API and both portals, each in its own window.

    Before the first run:
      1. Run Database\ProCargo.Database\SSMS\ProCargo_Install.sql and ProCargo_TestData.sql in SSMS.
      2. Install the .NET 10 SDK and Node.js 22.

    Usage (from the repository folder, in PowerShell):
      .\start-procargo.ps1                                         # SQL Server on this PC, Windows login
      .\start-procargo.ps1 -Server "localhost\SQLEXPRESS"          # SQL Server Express
      .\start-procargo.ps1 -SqlUser sa -SqlPassword "your-pass"    # SQL login instead of Windows login

    The first run stores the connection string and generated keys with 'dotnet user-secrets' (in your Windows
    profile, never in this folder) and installs the portals' packages. Later runs just start everything.
#>
param(
    [string]$Server = "localhost",
    [string]$SqlUser,
    [string]$SqlPassword
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$api = Join-Path $root "Backend\src\ProCargo.API"
$ui = Join-Path $root "CustomerPortal\ProCargo.UI"
$ops = Join-Path $root "OperationsPortal\ProCargo.Operations"

function Require([string]$command, [string]$hint) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        Write-Host "$command was not found. $hint" -ForegroundColor Red
        exit 1
    }
}
Require "dotnet" "Install the .NET 10 SDK from https://dotnet.microsoft.com/download and open a new PowerShell window."
Require "npm" "Install Node.js 22 LTS from https://nodejs.org and open a new PowerShell window."

function New-Secret([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buffer)
    [Convert]::ToBase64String($buffer)
}

# ---------- API settings (stored outside the repository) ----------
Push-Location $api
try {
    $existing = (dotnet user-secrets list 2>$null) -join "`n"

    $auth = if ($SqlUser) { "User Id=$SqlUser;Password=$SqlPassword" } else { "Trusted_Connection=True" }
    $connection = "Server=$Server;Database=ProCargo;$auth;TrustServerCertificate=True;Encrypt=True"
    if ($SqlUser -or $PSBoundParameters.ContainsKey('Server') -or $existing -notmatch "ConnectionStrings:ProCargo") {
        dotnet user-secrets set "ConnectionStrings:ProCargo" $connection | Out-Null
        Write-Host "Database connection set to $Server / ProCargo"
    }
    if ($existing -notmatch "Jwt:SigningKey") { dotnet user-secrets set "Jwt:SigningKey" (New-Secret 64) | Out-Null }
    if ($existing -notmatch "Security:OtpHashingKey") { dotnet user-secrets set "Security:OtpHashingKey" (New-Secret 64) | Out-Null }
    if ($existing -notmatch "Payments:Sandbox:Secret") { dotnet user-secrets set "Payments:Sandbox:Secret" (New-Secret 32) | Out-Null }
    # No mail server locally: switch e-mail off unless you have configured one yourself.
    if ($existing -notmatch "Email:Provider") { dotnet user-secrets set "Email:Provider" "None" | Out-Null }

    if (-not (Test-Path (Join-Path $env:APPDATA "ASP.NET\Https"))) {
        Write-Host "Trusting the local HTTPS development certificate (click Yes if Windows asks)..."
    }
    dotnet dev-certs https --trust | Out-Null
}
finally {
    Pop-Location
}

# ---------- portal packages (first run only) ----------
foreach ($portal in @($ui, $ops)) {
    if (-not (Test-Path (Join-Path $portal "node_modules"))) {
        Write-Host "Installing packages in $portal (first run, takes a few minutes)..."
        Push-Location $portal
        try { npm install --no-audit --no-fund } finally { Pop-Location }
    }
}

# ---------- start everything, one window each ----------
$shell = if (Get-Command pwsh -ErrorAction SilentlyContinue) { "pwsh" } else { "powershell" }
function Start-Window([string]$title, [string]$folder, [string]$command) {
    $script = "`$Host.UI.RawUI.WindowTitle = '$title'; Set-Location '$folder'; $command"
    Start-Process $shell -ArgumentList "-NoExit", "-Command", $script | Out-Null
}

Start-Window "ProCargo API" $api "dotnet run --launch-profile https"
Start-Window "ProCargo customer portal" $ui "npm run dev"
Start-Window "ProCargo operations portal" $ops "npm run dev"

Write-Host ""
Write-Host "Starting... the API takes about 20 seconds the first time." -ForegroundColor Green
Write-Host "  API (Swagger)       https://localhost:7180/swagger"
Write-Host "  Customer portal     http://localhost:5173   customer@ / owner@ / driver@procargo.test"
Write-Host "  Operations portal   http://localhost:5174   admin@ / ops@ / finance@ / support@procargo.test"
Write-Host "  Password for all test accounts: ProCargo@Dev1"
Write-Host "Close the three windows to stop."

Start-Sleep -Seconds 20
Start-Process "http://localhost:5173"
Start-Process "http://localhost:5174"
