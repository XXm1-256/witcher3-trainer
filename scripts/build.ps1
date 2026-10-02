param([ValidateSet('zh-CN','en')][string]$Language='zh-CN')
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$sourceRoot=$repoRoot
if ($Language -eq 'en') {
    python "$PSScriptRoot/localize.py"
    if ($LASTEXITCODE -ne 0) { throw 'English source generation failed' }
    $sourceRoot=Join-Path $repoRoot '.build/en'
}
$outputRoot=Join-Path $repoRoot "dist/$Language"
dotnet publish "$sourceRoot/Witcher3Modifier/Witcher3Modifier.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $outputRoot --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host "Player executable: $outputRoot/Witcher3Modifier.exe"
