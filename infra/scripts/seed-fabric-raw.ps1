param(
    [Parameter(Mandatory = $true)]
    [string]$WorkspaceId,

    [Parameter(Mandatory = $true)]
    [string]$LakehouseId,

    [Parameter(Mandatory = $true)]
    [string]$DatasetSeedPath,

    [string]$OneLakeEndpoint = 'https://onelake.dfs.fabric.microsoft.com'
)

$ErrorActionPreference = 'Stop'

function Get-OneLakeAccessToken {
    $resource = 'https://storage.azure.com'

    if (-not [string]::IsNullOrWhiteSpace($env:AZURE_CLIENT_ID)) {
        try {
            $uri = "http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=$resource&client_id=$($env:AZURE_CLIENT_ID)"
            $resp = Invoke-RestMethod -Uri $uri -Headers @{ Metadata = 'true' }
            if (-not [string]::IsNullOrWhiteSpace($resp.access_token)) {
                return $resp.access_token
            }
        }
        catch {
            Write-Verbose "IMDS token request failed: $_"
        }
    }

    try {
        return (Get-AzAccessToken -ResourceUrl $resource).Token
    }
    catch {}

    $token = az account get-access-token --resource $resource --query accessToken -o tsv 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($token)) {
        return $token
    }

    throw "Unable to acquire access token for '$resource'."
}

Write-Host '=== Fabric raw seed (HLS) ==='
Write-Host "WorkspaceId: $WorkspaceId"
Write-Host "LakehouseId: $LakehouseId"
Write-Host "DatasetSeedPath: $DatasetSeedPath"
Write-Host "OneLake endpoint: $OneLakeEndpoint"

$datasetSeedRoot = (Resolve-Path -LiteralPath $DatasetSeedPath).ProviderPath
if (-not $datasetSeedRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
    $datasetSeedRoot += [System.IO.Path]::DirectorySeparatorChar
}

Write-Host "Dataset-seed root: $datasetSeedRoot"

$allFiles = [System.Collections.ArrayList]::new()

$casesRoot = Join-Path $datasetSeedRoot 'cases'
if (Test-Path -LiteralPath $casesRoot -PathType Container) {
    Get-ChildItem -Path $casesRoot -Directory | ForEach-Object {
        $caseName = $_.Name
        $ingestPath = Join-Path $_.FullName 'ingest'
        if (Test-Path -LiteralPath $ingestPath -PathType Container) {
            Get-ChildItem -Path $ingestPath -File | ForEach-Object {
                [void]$allFiles.Add([pscustomobject]@{
                    FullName   = $_.FullName
                    CaseName   = $caseName
                    FileName   = $_.Name
                    TargetPath = "Files/raw/$caseName/$($_.Name)"
                })
            }
        }
        else {
            Write-Host "Case '$caseName' has no ingest folder, skipping."
        }
    }
}
else {
    throw "Cases directory not found: $casesRoot"
}

$policiesRoot = Join-Path $datasetSeedRoot 'policies'
if (Test-Path -LiteralPath $policiesRoot -PathType Container) {
    Write-Host "Uploading policy files..."
    Get-ChildItem -Path $policiesRoot -File | ForEach-Object {
        [void]$allFiles.Add([pscustomobject]@{
            FullName   = $_.FullName
            CaseName   = 'policies'
            FileName   = $_.Name
            TargetPath = "Files/policies/$($_.Name)"
        })
    }
}

if ($allFiles.Count -eq 0) {
    throw "No files found in $casesRoot (no cases with ingest folders)"
}

Write-Host "Found $($allFiles.Count) files to upload."

$token = Get-OneLakeAccessToken

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $allFiles | ForEach-Object -ThrottleLimit 10 -Parallel {
        $file = $_
        $wsId = $using:WorkspaceId
        $lhId = $using:LakehouseId
        $endpoint = $using:OneLakeEndpoint
        $token = $using:token

        $relativeTargetPath = $file.TargetPath
        $targetPath = "$lhId/$relativeTargetPath"
        $baseUri = "$endpoint/$wsId/$targetPath"
        $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)

        $handler = [System.Net.Http.HttpClientHandler]::new()
        $client = [System.Net.Http.HttpClient]::new($handler)
        try {
            $bearer = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)

            $create = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Put, "$baseUri`?resource=file")
            $create.Headers.Authorization = $bearer
            $resp = $client.SendAsync($create).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Create failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            $content = [System.Net.Http.ByteArrayContent]::new($fileBytes)
            $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/octet-stream')
            $content.Headers.ContentLength = $fileBytes.LongLength

            $append = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Patch, "$baseUri`?action=append&position=0")
            $append.Headers.Authorization = $bearer
            $append.Content = $content
            $resp = $client.SendAsync($append).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Append failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            $flush = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Patch, "$baseUri`?action=flush&position=$($fileBytes.LongLength)")
            $flush.Headers.Authorization = $bearer
            $resp = $client.SendAsync($flush).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Flush failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            Write-Host "Uploaded: $relativeTargetPath"
        }
        catch {
            throw "Upload failed for '$($file.FullName)': $($_.Exception.Message) [URI: $baseUri]"
        }
        finally {
            $client.Dispose()
            $handler.Dispose()
        }
    }
}
else {
    Write-Host 'PowerShell 5.x detected. Uploading raw files sequentially (parallel mode requires PowerShell 7+).'

    foreach ($file in $allFiles) {
        $relativeTargetPath = $file.TargetPath

        $targetPath = "$LakehouseId/$relativeTargetPath"
        $baseUri = "$OneLakeEndpoint/$WorkspaceId/$targetPath"
        $fileBytes = [System.IO.File]::ReadAllBytes($file.FullName)

        $handler = [System.Net.Http.HttpClientHandler]::new()
        $client = [System.Net.Http.HttpClient]::new($handler)
        try {
            $bearer = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)

            $create = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Put, "$baseUri`?resource=file")
            $create.Headers.Authorization = $bearer
            $resp = $client.SendAsync($create).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Create failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            $content = [System.Net.Http.ByteArrayContent]::new($fileBytes)
            $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/octet-stream')
            $content.Headers.ContentLength = $fileBytes.LongLength

            $append = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Patch, "$baseUri`?action=append&position=0")
            $append.Headers.Authorization = $bearer
            $append.Content = $content
            $resp = $client.SendAsync($append).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Append failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            $flush = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Patch, "$baseUri`?action=flush&position=$($fileBytes.LongLength)")
            $flush.Headers.Authorization = $bearer
            $resp = $client.SendAsync($flush).GetAwaiter().GetResult()
            if (-not $resp.IsSuccessStatusCode) {
                $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                throw "Flush failed for '$relativeTargetPath' ($($resp.StatusCode)): $body"
            }

            Write-Host "Uploaded: $relativeTargetPath"
        }
        catch {
            throw "Upload failed for '$($file.FullName)': $($_.Exception.Message) [URI: $baseUri]"
        }
        finally {
            $client.Dispose()
            $handler.Dispose()
        }
    }
}

Write-Host "Raw upload completed. Files uploaded: $($allFiles.Count)"
exit 0
