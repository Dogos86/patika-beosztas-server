[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repositoryRoot ".env"
$apiProject = Join-Path $repositoryRoot "src/PatikaBeosztas.Api/PatikaBeosztas.Api.csproj"

function Import-RepositoryEnv {
    param([Parameter(Mandatory)][string]$Path)

    foreach ($line in Get-Content -LiteralPath $Path -Encoding utf8) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith("#", [StringComparison]::Ordinal)) {
            continue
        }

        $separatorIndex = $line.IndexOf("=", [StringComparison]::Ordinal)
        if ($separatorIndex -lt 1) {
            throw "Érvénytelen .env sor: hiányzik a változónév vagy az egyenlőségjel."
        }

        $name = $line.Substring(0, $separatorIndex).Trim()
        if ($name -notmatch "^[A-Za-z_][A-Za-z0-9_]*$") {
            throw "Érvénytelen környezetiváltozó-név a .env fájlban."
        }

        $value = $line.Substring($separatorIndex + 1).Trim()
        if ($value.Length -ge 2) {
            $isSingleQuoted = $value.StartsWith("'", [StringComparison]::Ordinal) -and
                $value.EndsWith("'", [StringComparison]::Ordinal)
            $isDoubleQuoted = $value.StartsWith('"', [StringComparison]::Ordinal) -and
                $value.EndsWith('"', [StringComparison]::Ordinal)
            if ($isSingleQuoted -or $isDoubleQuoted) {
                $value = $value.Substring(1, $value.Length - 2)
            }
        }

        if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($name, "Process"))) {
            [Environment]::SetEnvironmentVariable($name, $value, "Process")
        }
    }
}

function Assert-RequiredEnvironmentValue {
    param([Parameter(Mandatory)][string]$Name)

    $value = [Environment]::GetEnvironmentVariable($Name, "Process")
    if ([string]::IsNullOrWhiteSpace($value) -or
        $value.IndexOf("CHANGE_ME", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "A(z) $Name környezeti változó hiányzik vagy placeholder értéket tartalmaz."
    }
}

function Get-DotNet10Command {
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "A dotnet parancs nem érhető el. Telepítsd a .NET 10 SDK-t."
    }

    $versionText = (& $command.Source --version).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw "A .NET SDK verziója nem kérdezhető le."
    }

    try {
        $version = [version]($versionText.Split("-", 2)[0])
    }
    catch {
        throw "A .NET SDK verziója nem értelmezhető."
    }

    if ($version.Major -ne 10) {
        throw "A projekthez .NET 10 SDK szükséges; az aktív főverzió: $($version.Major)."
    }

    return $command.Source
}

function Assert-DockerEngine {
    $command = Get-Command docker -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "A docker parancs nem érhető el. Indítsd vagy telepítsd a Docker Desktopot."
    }

    $null = & $command.Source version --format '{{.Server.Version}}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "A Docker motor nem érhető el. Indítsd el a Docker Desktopot."
    }

    return $command.Source
}

if (-not (Test-Path -LiteralPath $envFile -PathType Leaf)) {
    throw "Hiányzik a repository .env fájlja. Másold le a .env.example fájlt, majd cseréld le a CHANGE_ME értékeket."
}

Import-RepositoryEnv -Path $envFile
foreach ($name in @(
        "POSTGRES_DB",
        "POSTGRES_USER",
        "POSTGRES_PASSWORD",
        "ConnectionStrings__DefaultConnection",
        "Seed__DemoPassword")) {
    Assert-RequiredEnvironmentValue -Name $name
}

if ([string]::IsNullOrWhiteSpace($env:Cors__AllowedOrigins__0)) {
    $env:Cors__AllowedOrigins__0 = "https://localhost:5173"
}

$dotnetCommand = Get-DotNet10Command
$dockerCommand = Assert-DockerEngine

Push-Location $repositoryRoot
try {
    Write-Host "A helyi PostgreSQL konténer indítása vagy újrahasználata..."
    & $dockerCommand compose --env-file .env up -d postgres
    if ($LASTEXITCODE -ne 0) {
        throw "A PostgreSQL konténer nem indítható el."
    }

    $null = & $dotnetCommand dev-certs https --check --trust 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "A fejlesztői HTTPS-tanúsítvány nem megbízható. Futtasd: dotnet dev-certs https --trust"
    }

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = "https://localhost:7180"

    Write-Host "Az API indul: https://localhost:7180"
    Write-Host "Leállítás: Ctrl+C"
    & $dotnetCommand run --project $apiProject --no-launch-profile
    if ($LASTEXITCODE -ne 0) {
        throw "Az API folyamat hibával állt le."
    }
}
finally {
    Pop-Location
}
