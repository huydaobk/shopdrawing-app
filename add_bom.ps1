$utf8WithBom = New-Object System.Text.UTF8Encoding $true
Get-ChildItem -Path "c:\my_project\shopdrawing-app\ShopDrawing.Plugin" -Filter *.cs -Recurse | ForEach-Object {
    $content = Get-Content -Path $_.FullName -Raw
    [System.IO.File]::WriteAllText($_.FullName, $content, $utf8WithBom)
}
Write-Host "Done adding BOM to all .cs files"
