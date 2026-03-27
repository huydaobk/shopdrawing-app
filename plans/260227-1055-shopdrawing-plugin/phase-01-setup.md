# Phase 01: Project Setup
Status: ✅ Complete
Sprint: 1 (T1)

## Objective
Tạo C# .NET 8 Class Library project, cấu hình AutoCAD 2026 references, NuGet packages, folder structure chuẩn.

## Requirements
- [ ] Solution tên: `ShopDrawing.sln`
- [ ] Project tên: `ShopDrawing.Plugin` (Class Library, net8.0-windows)
- [ ] Target: net8.0-windows (WPF yêu cầu -windows)

## Implementation Steps
1. [ ] Tạo solution và project
   ```
   dotnet new classlib -n ShopDrawing.Plugin -f net8.0-windows
   ```
2. [ ] Thêm AutoCAD 2026 references (từ thư mục cài AutoCAD)
   - `accoremgd.dll`
   - `acdbmgd.dll`
   - `acmgd.dll`
   - Copy Local = false (không bundle vào DLL)
3. [ ] Thêm NuGet packages
   ```xml
   <PackageReference Include="Microsoft.Data.Sqlite" Version="8.*" />
   ```
4. [ ] Tạo folder structure
   ```
   ShopDrawing.Plugin/
   ├── Commands/
   ├── Core/
   ├── Data/
   ├── Models/
   ├── UI/
   └── Resources/
   ```
5. [ ] Tạo `ShopDrawingApp.cs` — entry point (IExtensionApplication)
6. [ ] Tạo `panel_specs.json` mặc định trong Resources/
7. [ ] Verify build thành công

## Files to Create
- `ShopDrawing.Plugin.csproj`
- `ShopDrawingApp.cs` — IExtensionApplication
- `Resources/panel_specs.json`

## Test Criteria
- [ ] Build thành công, không lỗi
- [ ] Load được vào AutoCAD bằng NETLOAD
- [ ] Console không báo lỗi khi load

---
Next: [Phase 02 — Core Models](phase-02-models.md)
