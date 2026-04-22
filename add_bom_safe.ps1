$files = Get-ChildItem -Path "c:\my_project\shopdrawing-app\ShopDrawing.Plugin" -Filter *.cs -Recurse
foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    if ($bytes.Length -gt 2 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) {
        # Already has BOM
    } else {
        $bom = [byte[]](239, 187, 191)
        [System.IO.File]::WriteAllBytes($f.FullName, $bom + $bytes)
        Write-Host "Added BOM to $($f.Name)"
    }
}
Write-Host "Done safely adding BOM"
