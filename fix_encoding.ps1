$files = Get-ChildItem -Path "ShopDrawing.Plugin" -Filter *.cs -Recurse
foreach ($f in $files) {
    try {
        $text = [IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8)
        [IO.File]::WriteAllText($f.FullName, $text, [Text.Encoding]::UTF8)
        Write-Host "Processed $($f.FullName)"
    } catch {
        Write-Host "Failed to process $($f.FullName): $_"
    }
}
