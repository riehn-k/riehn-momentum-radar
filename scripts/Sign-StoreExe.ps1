param(
    [string]$Version = "1.0.0",
    [string]$Thumbprint = "",
    [switch]$AllowAutoSelect
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $repoRoot ("releases\{0}\RiehnMomentumRadar-{0}.exe" -f $Version)
$signtool = Join-Path $repoRoot ".tools\Microsoft.Windows.SDK.BuildTools.10.0.28000.1721\bin\10.0.28000.0\x64\signtool.exe"
$hashPath = Join-Path $repoRoot ("releases\{0}\SHA256SUMS.txt" -f $Version)

if (!(Test-Path -LiteralPath $exePath)) {
    throw "Release EXE not found: $exePath"
}

if (!(Test-Path -LiteralPath $signtool)) {
    throw "SignTool not found: $signtool"
}

if ([string]::IsNullOrWhiteSpace($Thumbprint) -and !$AllowAutoSelect) {
    throw @"
No certificate thumbprint was provided.

This script intentionally avoids SignTool /a auto-selection because it can pick
self-signed test certificates, which Microsoft Store and Windows SmartScreen do
not trust.

Install a real Authenticode code-signing certificate first, then run:

  powershell -ExecutionPolicy Bypass -File .\scripts\Sign-StoreExe.ps1 -Thumbprint YOUR_CERT_THUMBPRINT

To list possible certificates:

  Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -CodeSigningCert | Format-List Subject,Issuer,Thumbprint,NotAfter,HasPrivateKey
"@
}

$signArgs = @(
    "sign",
    "/fd", "SHA256",
    "/tr", "http://timestamp.acs.microsoft.com",
    "/td", "SHA256",
    "/v"
)

if (![string]::IsNullOrWhiteSpace($Thumbprint)) {
    $signArgs += @("/sha1", $Thumbprint)
} else {
    $signArgs += "/a"
}

$signArgs += $exePath

& $signtool @signArgs
if ($LASTEXITCODE -ne 0) {
    throw "SignTool failed with exit code $LASTEXITCODE. Install a trusted Authenticode code-signing certificate first."
}

& $signtool verify /pa /v $exePath
if ($LASTEXITCODE -ne 0) {
    throw @"
Signature verification failed with exit code $LASTEXITCODE.

Most likely cause: the selected certificate is self-signed or does not chain to
a trusted public root CA. Microsoft Store EXE/MSI submission needs a real
Authenticode code-signing certificate from a trusted CA.
"@
}

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $exePath
("{0}  {1}" -f $hash.Hash, (Split-Path -Leaf $exePath)) | Set-Content -Encoding ASCII -LiteralPath $hashPath

Write-Host "Signed and verified: $exePath"
Write-Host "SHA256 written to: $hashPath"
