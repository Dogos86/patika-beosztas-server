[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern("^https://")]
    [string] $BaseUrl,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $Email,

    [PSCredential] $Credential
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($null -eq $Credential) {
    $Credential = Get-Credential `
        -UserName $Email `
        -Message "Add meg a pilot felhasználó jelszavát. A script nem írja ki és nem tárolja."
}

if ($Credential.UserName -ne $Email) {
    throw "A Credential felhasználóneve egyezzen meg az -Email értékével."
}

$root = [Uri]::new($BaseUrl.TrimEnd("/") + "/")
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.CookieContainer = [System.Net.CookieContainer]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(30)

function Invoke-PilotRequest {
    param(
        [Parameter(Mandatory)]
        [System.Net.Http.HttpMethod] $Method,

        [Parameter(Mandatory)]
        [string] $Path,

        [string] $JsonBody,

        [string] $CsrfToken
    )

    $requestUri = [Uri]::new($root, $Path.TrimStart("/"))
    $request = [System.Net.Http.HttpRequestMessage]::new($Method, $requestUri)
    try {
        if ($null -ne $JsonBody) {
            $request.Content = [System.Net.Http.StringContent]::new(
                $JsonBody,
                [System.Text.Encoding]::UTF8,
                "application/json")
        }

        if ($null -ne $CsrfToken) {
            [void] $request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", $CsrfToken)
        }

        return $client.SendAsync($request).GetAwaiter().GetResult()
    }
    finally {
        $request.Dispose()
    }
}

function Assert-Status {
    param(
        [Parameter(Mandatory)]
        [System.Net.Http.HttpResponseMessage] $Response,

        [Parameter(Mandatory)]
        [int] $Expected,

        [Parameter(Mandatory)]
        [string] $Check
    )

    $actual = [int] $Response.StatusCode
    if ($actual -ne $Expected) {
        $body = $Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        throw "$Check sikertelen: HTTP $actual, várt HTTP $Expected. Válasz: $body"
    }

    Write-Host "[OK] $Check (HTTP $actual)"
}

try {
    $frontend = Invoke-PilotRequest -Method ([System.Net.Http.HttpMethod]::Get) -Path "/"
    Assert-Status -Response $frontend -Expected 200 -Check "Frontend"
    $frontend.Dispose()

    $live = Invoke-PilotRequest -Method ([System.Net.Http.HttpMethod]::Get) -Path "/health/live"
    Assert-Status -Response $live -Expected 200 -Check "API liveness"
    $live.Dispose()

    $ready = Invoke-PilotRequest -Method ([System.Net.Http.HttpMethod]::Get) -Path "/health/ready"
    Assert-Status -Response $ready -Expected 200 -Check "API readiness és PostgreSQL"
    $ready.Dispose()

    $csrf = Invoke-PilotRequest -Method ([System.Net.Http.HttpMethod]::Get) -Path "/api/auth/csrf"
    Assert-Status -Response $csrf -Expected 200 -Check "CSRF token"
    $csrfPayload = $csrf.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    $csrfToken = [string] $csrfPayload.requestToken
    if ([string]::IsNullOrWhiteSpace($csrfToken)) {
        throw "A CSRF válasz nem tartalmaz requestToken értéket."
    }
    $csrf.Dispose()

    $plainPassword = $Credential.GetNetworkCredential().Password
    try {
        $loginBody = @{
            email      = $Email
            password   = $plainPassword
            rememberMe = $false
        } | ConvertTo-Json -Compress
    }
    finally {
        $plainPassword = $null
    }

    try {
        $login = Invoke-PilotRequest `
            -Method ([System.Net.Http.HttpMethod]::Post) `
            -Path "/api/auth/login" `
            -JsonBody $loginBody `
            -CsrfToken $csrfToken
    }
    finally {
        $loginBody = $null
    }
    Assert-Status -Response $login -Expected 200 -Check "Bejelentkezés"
    $login.Dispose()

    $session = Invoke-PilotRequest -Method ([System.Net.Http.HttpMethod]::Get) -Path "/api/auth/session"
    Assert-Status -Response $session -Expected 200 -Check "Hitelesített munkamenet"
    $session.Dispose()

    $schedule = Invoke-PilotRequest -Method ([System.Net.Http.HttpMethod]::Get) -Path "/api/me/schedule"
    Assert-Status -Response $schedule -Expected 200 -Check "Saját Published beosztás"
    $schedule.Dispose()

    $freshCsrf = Invoke-PilotRequest -Method ([System.Net.Http.HttpMethod]::Get) -Path "/api/auth/csrf"
    Assert-Status -Response $freshCsrf -Expected 200 -Check "CSRF token mutációhoz"
    $freshCsrfPayload =
        $freshCsrf.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    $freshCsrfToken = [string] $freshCsrfPayload.requestToken
    $freshCsrf.Dispose()

    $logout = Invoke-PilotRequest `
        -Method ([System.Net.Http.HttpMethod]::Post) `
        -Path "/api/auth/logout" `
        -CsrfToken $freshCsrfToken
    Assert-Status -Response $logout -Expected 204 -Check "CSRF-védett mutáció (kijelentkezés)"
    $logout.Dispose()

    $openApi = Invoke-PilotRequest `
        -Method ([System.Net.Http.HttpMethod]::Get) `
        -Path "/openapi/v1.json"
    Assert-Status -Response $openApi -Expected 404 -Check "Production OpenAPI tiltva"
    $openApi.Dispose()

    Write-Host "A pilot smoke teszt minden ellenőrzése sikeres."
}
finally {
    $client.Dispose()
    $handler.Dispose()
}
