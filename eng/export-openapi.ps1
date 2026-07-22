[CmdletBinding()]
param(
    [uri]$ApiBaseUri = "https://localhost:7180",
    [ValidateRange(10, 300)]
    [int]$StartupTimeoutSeconds = 90
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$expectedTitle = "Patika Beoszt$([char]0x00E1)s API"
$expectedVersion = "0.3.0-phase2b"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repositoryRoot ".env"
$apiProject = Join-Path $repositoryRoot "src/PatikaBeosztas.Api/PatikaBeosztas.Api.csproj"
$apiAssembly = Join-Path $repositoryRoot "src/PatikaBeosztas.Api/bin/Release/net10.0/PatikaBeosztas.Api.dll"
$outputPath = Join-Path $repositoryRoot "contracts/openapi.phase2b.json"
$openApiUri = [uri]::new($ApiBaseUri, "/openapi/v1.json")
$startedApiProcess = $null

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

function Get-OpenApiResponse {
    param([Parameter(Mandatory)][uri]$Uri)

    return Invoke-WebRequest -Uri $Uri -Method Get -TimeoutSec 5 -UseBasicParsing
}

function Assert-OpenApiDocument {
    param(
        [Parameter(Mandatory)][string]$Json,
        [Parameter(Mandatory)][string]$ContentType
    )

    if (-not $ContentType.StartsWith("application/json", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Az OpenAPI végpont nem application/json választ adott."
    }

    try {
        $document = $Json | ConvertFrom-Json
    }
    catch {
        throw "Az OpenAPI végpont válasza nem érvényes JSON."
    }

    if ([string]::IsNullOrWhiteSpace($document.openapi)) {
        throw "A JSON nem OpenAPI dokumentum: hiányzik az openapi mező."
    }

    if ($document.info.title -ne $expectedTitle) {
        throw "Az OpenAPI dokumentum API-címe nem a várt '$expectedTitle'."
    }

    if ($document.info.version -ne $expectedVersion) {
        throw "Az OpenAPI dokumentum verziója nem a várt '$expectedVersion'."
    }

    $requiredPaths = @(
        "/api/auth/csrf",
        "/api/auth/login",
        "/api/auth/logout",
        "/api/auth/session",
        "/api/admin/employees",
        "/api/admin/users",
        "/api/admin/locations",
        "/api/me/work-preferences",
        "/api/me/leave-requests",
        "/api/admin/locations/{locationId}/weekly-opening",
        "/api/admin/locations/{locationId}/shift-templates",
        "/api/admin/coverage-requirements",
        "/api/admin/employees/{employeeId}/capabilities",
        "/api/admin/employees/{employeeId}/work-profile",
        "/api/admin/employees/{employeeId}/shift-quota-rules"
    )
    $availablePaths = $document.paths.PSObject.Properties.Name
    foreach ($path in $requiredPaths) {
        if ($availablePaths -notcontains $path) {
            throw "A runtime OpenAPI dokumentumból hiányzik a várt útvonal: $path"
        }
    }
}

if (Test-Path -LiteralPath $envFile -PathType Leaf) {
    Import-RepositoryEnv -Path $envFile
}

try {
    try {
        $response = Get-OpenApiResponse -Uri $openApiUri
        Write-Host "A már futó helyi API használata."
    }
    catch {
        $requiredEnvironmentValues = @(
            "ConnectionStrings__DefaultConnection",
            "Seed__DemoPassword"
        )
        if (Test-Path -LiteralPath $envFile -PathType Leaf) {
            $requiredEnvironmentValues += @(
                "POSTGRES_DB",
                "POSTGRES_USER",
                "POSTGRES_PASSWORD"
            )
        }

        foreach ($name in $requiredEnvironmentValues) {
            Assert-RequiredEnvironmentValue -Name $name
        }

        $dotnetCommand = Get-DotNet10Command
        if ([string]::IsNullOrWhiteSpace($env:Cors__AllowedOrigins__0)) {
            $env:Cors__AllowedOrigins__0 = "https://localhost:5173"
        }

        $env:ASPNETCORE_ENVIRONMENT = "Development"
        $env:ASPNETCORE_URLS = $ApiBaseUri.AbsoluteUri.TrimEnd("/")

        if (Test-Path -LiteralPath $envFile -PathType Leaf) {
            $dockerCommand = Get-Command docker -ErrorAction SilentlyContinue
            if ($null -eq $dockerCommand) {
                throw "Az API indításához szükséges docker parancs nem érhető el."
            }

            $null = & $dockerCommand.Source version --format '{{.Server.Version}}' 2>$null
            if ($LASTEXITCODE -ne 0) {
                throw "Az API indításához szükséges Docker motor nem érhető el."
            }

            Push-Location $repositoryRoot
            try {
                & $dockerCommand.Source compose --env-file .env up -d postgres
                if ($LASTEXITCODE -ne 0) {
                    throw "A PostgreSQL konténer nem indítható el."
                }
            }
            finally {
                Pop-Location
            }
        }

        Write-Host "Az API Release buildjének elkészítése..."
        & $dotnetCommand build $apiProject --configuration Release --nologo
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $apiAssembly -PathType Leaf)) {
            throw "Az API Release buildje sikertelen."
        }

        $startedApiProcess = Start-Process `
            -FilePath $dotnetCommand `
            -ArgumentList @("`"$apiAssembly`"") `
            -WorkingDirectory $repositoryRoot `
            -WindowStyle Hidden `
            -PassThru

        $deadline = [DateTimeOffset]::UtcNow.AddSeconds($StartupTimeoutSeconds)
        $response = $null
        while ([DateTimeOffset]::UtcNow -lt $deadline) {
            $startedApiProcess.Refresh()
            if ($startedApiProcess.HasExited) {
                throw "A helyi API az OpenAPI végpont elérhetővé válása előtt leállt. Ellenőrizd az adatbázist és a fejlesztői HTTPS-tanúsítványt."
            }

            try {
                $response = Get-OpenApiResponse -Uri $openApiUri
                break
            }
            catch {
                Start-Sleep -Milliseconds 500
            }
        }

        if ($null -eq $response) {
            throw "A helyi API nem vált elérhetővé $StartupTimeoutSeconds másodpercen belül. Szükség esetén futtasd: dotnet dev-certs https --trust"
        }
    }

    if ($null -ne $response.RawContentStream -and
        $response.RawContentStream -is [IO.MemoryStream]) {
        $responseBytes = $response.RawContentStream.ToArray()
    }
    else {
        $responseBytes = [Text.Encoding]::UTF8.GetBytes($response.Content)
    }

    $responseJson = [Text.Encoding]::UTF8.GetString($responseBytes)
    $contentType = $response.Headers["Content-Type"]
    Assert-OpenApiDocument -Json $responseJson -ContentType $contentType

    $temporaryPath = Join-Path `
        (Split-Path -Parent $outputPath) `
        ([IO.Path]::GetRandomFileName())
    try {
        [IO.File]::WriteAllBytes($temporaryPath, $responseBytes)

        Move-Item -LiteralPath $temporaryPath -Destination $outputPath -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }

    $hash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host "Runtime OpenAPI export elkészült: contracts/openapi.phase2b.json"
    Write-Host "SHA256: $hash"
}
finally {
    if ($null -ne $startedApiProcess -and -not $startedApiProcess.HasExited) {
        Stop-Process -Id $startedApiProcess.Id -Force
        $startedApiProcess.WaitForExit()
    }
}
