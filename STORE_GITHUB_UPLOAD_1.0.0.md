# Microsoft Store upload via GitHub - version 1.0.0

Use this path when Partner Center asks for a versioned HTTPS URL for an EXE/MSI package:

```text
https://raw.githubusercontent.com/riehn-k/riehn-momentum-radar/v1.0.0/releases/1.0.0/RiehnMomentumRadar-1.0.0.exe
```

Do not use a GitHub Releases download URL like `github.com/.../releases/download/...`.
Those URLs redirect to another host and Partner Center rejects redirected package URLs.

## Local release file

```text
releases/1.0.0/RiehnMomentumRadar-1.0.0.exe
```

Current unsigned SHA256:

```text
E44D054B281BAE187468C3C60AC4B22BF6916513A06EB79C0FF056923F46490A
```

After signing, the SHA256 changes. Run the signing script so `SHA256SUMS.txt`
is regenerated.

## Required signing

For the Microsoft Store MSI/EXE path, the EXE and all PE files must be signed
with an Authenticode code-signing certificate that chains to a CA in the
Microsoft Trusted Root Program. A self-signed certificate is not enough.

Check whether the file is signed:

```powershell
Get-AuthenticodeSignature .\releases\1.0.0\RiehnMomentumRadar-1.0.0.exe
```

Sign with the best available code-signing certificate in the user certificate
store:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Sign-StoreExe.ps1
```

Or sign with a specific certificate thumbprint:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Sign-StoreExe.ps1 -Thumbprint YOUR_CERT_THUMBPRINT
```

Verify:

```powershell
.tools\Microsoft.Windows.SDK.BuildTools.10.0.28000.1721\bin\10.0.28000.0\x64\signtool.exe verify /pa /v .\releases\1.0.0\RiehnMomentumRadar-1.0.0.exe
```

## GitHub steps

1. Commit the release file and docs.
2. Push to GitHub.
3. Create an immutable version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

4. Open the raw URL in a private browser window:

```text
https://raw.githubusercontent.com/riehn-k/riehn-momentum-radar/v1.0.0/releases/1.0.0/RiehnMomentumRadar-1.0.0.exe
```

5. Use exactly that URL in Partner Center.

## Partner Center values

Package URL:

```text
https://raw.githubusercontent.com/riehn-k/riehn-momentum-radar/v1.0.0/releases/1.0.0/RiehnMomentumRadar-1.0.0.exe
```

Version:

```text
1.0.0
```

Notes:

- The URL is versioned because it points to the `v1.0.0` Git tag.
- Do not change the binary behind that tag after submission.
- For updates, create a new folder and tag, for example `releases/1.0.1` and `v1.0.1`.
- If Partner Center expects an installer rather than a portable app EXE, submit the MSIX package path instead or build a real MSI/EXE installer with silent install support.
