param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePng,

    [Parameter(Mandatory = $true)]
    [string]$TargetPng,

    [Parameter(Mandatory = $true)]
    [string]$TargetIco
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore

$sourcePath = [System.IO.Path]::GetFullPath($SourcePng)
$pngPath = [System.IO.Path]::GetFullPath($TargetPng)
$icoPath = [System.IO.Path]::GetFullPath($TargetIco)

if (-not [System.IO.File]::Exists($sourcePath)) {
    throw "Icon source does not exist: $sourcePath"
}

[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($pngPath)) | Out-Null
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($icoPath)) | Out-Null
[System.IO.File]::Copy($sourcePath, $pngPath, $true)

$stream = [System.IO.File]::OpenRead($sourcePath)
try {
    $decoder = [System.Windows.Media.Imaging.BitmapDecoder]::Create(
        $stream,
        [System.Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
        [System.Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $source = $decoder.Frames[0]
}
finally {
    $stream.Dispose()
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = foreach ($size in $sizes) {
    $scaleX = $size / [double]$source.PixelWidth
    $scaleY = $size / [double]$source.PixelHeight
    $scaled = [System.Windows.Media.Imaging.TransformedBitmap]::new(
        $source,
        [System.Windows.Media.ScaleTransform]::new($scaleX, $scaleY))
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($scaled))
    $memory = [System.IO.MemoryStream]::new()
    try {
        $encoder.Save($memory)
        ,$memory.ToArray()
    }
    finally {
        $memory.Dispose()
    }
}

$output = [System.IO.File]::Create($icoPath)
$writer = [System.IO.BinaryWriter]::new($output)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$index].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$index].Length
    }

    foreach ($image in $images) {
        $writer.Write($image)
    }
}
finally {
    $writer.Dispose()
    $output.Dispose()
}

Write-Host "Updated icon master: $pngPath"
Write-Host "Generated $($sizes.Count)-frame ICO: $icoPath"
