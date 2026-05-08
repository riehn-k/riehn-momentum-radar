# Microsoft Store Submission Notes

Preferred path: use the MSIX package for Microsoft Store submission instead of uploading the raw `.exe` file.

Alternative Win32 URL path: if Partner Center asks for a versioned EXE/MSI package URL, use the GitHub raw tag URL documented in:

```text
STORE_GITHUB_UPLOAD_1.0.0.md
```

Important: for the Store MSI/EXE path, the EXE must be Authenticode-signed with a real code-signing certificate from a trusted CA. A self-signed certificate is not accepted for public Store submission.

## Package To Upload

```text
msix\Artifacts\RiehnMomentumRadar_1.0.0.0_x64.msix
```

The raw executable is still included for direct local use:

```text
Riehn Momentum Radar.exe
```

The versioned GitHub upload copy is:

```text
releases\1.0.0\RiehnMomentumRadar-1.0.0.exe
```

Partner Center package URL after creating/pushing the `v1.0.0` tag:

```text
https://raw.githubusercontent.com/riehn-k/riehn-momentum-radar/v1.0.0/releases/1.0.0/RiehnMomentumRadar-1.0.0.exe
```

## Why MSIX

Microsoft Store policy rejected the raw executable because it did not have a digital code signature. By packaging the app as MSIX for Store submission, the app follows the Microsoft Store packaging path. Microsoft can re-sign Store-distributed MSIX packages during certification/distribution.

## Important Partner Center Note

The package manifest currently uses this identity:

```xml
<Identity Name="RiehnMomentumRadar" Publisher="CN=Rene Riehn" Version="1.0.0.0" ProcessorArchitecture="x64" />
```

If Partner Center shows a different package identity or publisher value for the reserved app name, update `msix\Package\AppxManifest.xml` to match Partner Center exactly, then rebuild the package with MakeAppx.

## Rebuild Command

```powershell
.tools\Microsoft.Windows.SDK.BuildTools.10.0.28000.1721\bin\10.0.28000.0\x64\makeappx.exe pack /d msix\Package /p msix\Artifacts\RiehnMomentumRadar_1.0.0.0_x64.msix /o
```

## Validation Performed

The package was created with Microsoft MakeAppx and successfully unpacked again as a smoke test.
