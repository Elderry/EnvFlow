<#
.SYNOPSIS
    Generates application icon from SVG source file.

.DESCRIPTION
    Converts app_icon.svg to app.ico with multiple resolutions (256, 128, 64, 48, 32, 16)
    using ImageMagick. The output icon includes transparency support.

.NOTES
    Requires ImageMagick to be installed and available in PATH.
    Run this script from the EnvFlow project directory.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Check if ImageMagick is installed
if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    Write-Error "ImageMagick is not installed or not in PATH. Please install ImageMagick from https://imagemagick.org/"
    exit 1
}

# Define file paths
$svgFile = "app_icon.svg"
$icoFile = "app.ico"

# Verify source file exists
if (-not (Test-Path $svgFile)) {
    Write-Error "Source file '$svgFile' not found in current directory."
    exit 1
}

Write-Host "Converting $svgFile to $icoFile..." -ForegroundColor Cyan

# Convert SVG to ICO with multiple sizes and transparent background
& magick -background none $svgFile -define icon:auto-resize=256,128,64,48,32,16 $icoFile

if ($LASTEXITCODE -ne 0) {
    throw "ImageMagick conversion failed with exit code $LASTEXITCODE"
}

Write-Host "✓ Icon generated successfully: $icoFile" -ForegroundColor Green
Write-Host "  Sizes: 256x256, 128x128, 64x64, 48x48, 32x32, 16x16" -ForegroundColor Gray
