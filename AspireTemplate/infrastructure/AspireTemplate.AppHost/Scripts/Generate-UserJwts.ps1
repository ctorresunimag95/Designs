# Generate dotnet user-jwts tokens for local API development.
# Run from the repo root:
#   .\infrastructure\AspireTemplate.AppHost\Scripts\Generate-UserJwts.ps1
#
# Each invocation issues a new token. Old tokens remain valid until they expire.
# To invalidate all tokens at once, reset the signing key:
#   dotnet user-jwts key --reset --project src/AspireTemplate.SampleApi
#
# Parameters:
#   -Project  Path to the API .csproj (default: src/AspireTemplate.SampleApi)
#   -Audience Audience claim embedded in the token (must match Authentication:Audience, default: sample-api)
#   -ValidFor Token lifetime (default: 365d). Examples: 8h, 30d, 365d
param(
    [string]$Project  = "src/AspireTemplate.SampleApi",
    [string]$Audience = "sample-api",
    [string]$ValidFor = "365d"
)

$ErrorActionPreference = "Stop"

Write-Host "Generating dotnet user-jwts tokens" -ForegroundColor Cyan
Write-Host "  Project  : $Project"
Write-Host "  Audience : $Audience"
Write-Host "  Valid for: $ValidFor"
Write-Host ""

Write-Host "--- dev-user (api-reader) ---" -ForegroundColor Yellow
dotnet user-jwts create `
    --project $Project `
    --name    dev-user `
    --audience $Audience `
    --claim   roles=api-reader `
    --valid-for $ValidFor `
    --output  token

Write-Host ""
Write-Host "--- dev-admin (api-reader + api-writer) ---" -ForegroundColor Yellow
dotnet user-jwts create `
    --project $Project `
    --name    dev-admin `
    --audience $Audience `
    --claim   roles=api-reader `
    --claim   roles=api-writer `
    --valid-for $ValidFor `
    --output  token

Write-Host ""
Write-Host "--- sample-api-m2m (M2M simulation, api-reader + api-writer) ---" -ForegroundColor Yellow
dotnet user-jwts create `
    --project $Project `
    --name    sample-api-m2m `
    --audience $Audience `
    --claim   roles=api-reader `
    --claim   roles=api-writer `
    --claim   client_id=sample-api-m2m `
    --valid-for $ValidFor `
    --output  token

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "appsettings.Development.json in '$Project' has been updated with the ValidIssuer config." -ForegroundColor DarkGray
Write-Host "Make sure Keycloak is commented out in AppHost.cs before starting the AppHost." -ForegroundColor DarkGray
