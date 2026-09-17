param(
    [string]$Version = "2.17.0"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$BuildDir = Join-Path $Root ".build-blast"
$DownloadDir = Join-Path $BuildDir "download"
$ExtractDir = Join-Path $BuildDir "extract"
$StageDir = Join-Path $BuildDir "stage"
$ResourcesDir = Join-Path $Root "Resources"

$ArchiveName = "ncbi-blast-$Version+-x64-win64.tar.gz"
$BaseUrl = "https://ftp.ncbi.nlm.nih.gov/blast/executables/blast+/$Version"
$ArchiveUrl = "$BaseUrl/$ArchiveName"
$Md5Url = "$ArchiveUrl.md5"
$ArchivePath = Join-Path $DownloadDir $ArchiveName
$Md5Path = "$ArchivePath.md5"
$OutputZip = Join-Path $ResourcesDir "blast-win64-$Version.zip"

New-Item -ItemType Directory -Force -Path $DownloadDir, $ResourcesDir | Out-Null

if (-not (Test-Path $ArchivePath)) {
    Write-Host "Downloading official NCBI BLAST+ $Version for Windows x64 (~137 MB)..."
    Invoke-WebRequest -Uri $ArchiveUrl -OutFile $ArchivePath -UseBasicParsing
} else {
    Write-Host "Using cached BLAST+ archive: $ArchivePath"
}

Write-Host "Verifying NCBI archive MD5..."
Invoke-WebRequest -Uri $Md5Url -OutFile $Md5Path -UseBasicParsing
$Md5Text = (Get-Content $Md5Path -Raw).Trim()
$Expected = ([regex]::Match($Md5Text, '[0-9a-fA-F]{32}')).Value.ToLowerInvariant()
$Actual = (Get-FileHash -Path $ArchivePath -Algorithm MD5).Hash.ToLowerInvariant()
if ([string]::IsNullOrWhiteSpace($Expected) -or $Actual -ne $Expected) {
    Remove-Item $ArchivePath -Force -ErrorAction SilentlyContinue
    throw "NCBI BLAST+ archive MD5 verification failed. Expected $Expected, got $Actual. Run build-exe.bat again to redownload."
}

Remove-Item $ExtractDir, $StageDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $ExtractDir, $StageDir | Out-Null

Write-Host "Extracting BLAST+ package..."
& tar.exe -xzf $ArchivePath -C $ExtractDir
if ($LASTEXITCODE -ne 0) {
    throw "Windows tar.exe could not extract the NCBI BLAST+ archive."
}

$Blastn = Get-ChildItem -Path $ExtractDir -Recurse -File -Filter "blastn.exe" | Select-Object -First 1
if (-not $Blastn) {
    throw "blastn.exe was not found in the downloaded NCBI package."
}
$BinDir = $Blastn.Directory.FullName

$Required = @("blastn.exe", "blastp.exe", "tblastn.exe")
foreach ($Name in $Required) {
    $Source = Join-Path $BinDir $Name
    if (-not (Test-Path $Source)) {
        throw "$Name was not found in $BinDir"
    }
    Copy-Item $Source -Destination $StageDir -Force
}

# Include native DLLs shipped by NCBI, if present.
Get-ChildItem -Path $BinDir -File -Filter "*.dll" -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item $_.FullName -Destination $StageDir -Force
}

@"
NCBI BLAST+ $Version
Official package: $ArchiveUrl

BLAST software is public domain software from the U.S. National Center for
Biotechnology Information (NCBI). LocalBlast bundles the official Windows x64
executables for local sequence searching.
"@ | Set-Content -Path (Join-Path $StageDir "NCBI_BLAST_NOTICE.txt") -Encoding UTF8

Remove-Item $OutputZip -Force -ErrorAction SilentlyContinue
Write-Host "Creating embedded BLAST resource..."
Compress-Archive -Path (Join-Path $StageDir "*") -DestinationPath $OutputZip -CompressionLevel Optimal

$ZipSizeMb = [math]::Round((Get-Item $OutputZip).Length / 1MB, 1)
Write-Host "Bundled BLAST resource ready: $OutputZip ($ZipSizeMb MB)"
