# Release Auto Update

Muc tieu: phat hanh plugin theo tag Git va cho user trong team cap nhat tu dong ma khong phai pull source code.

## Thanh phan

- `ShopDrawing.Plugin`: plugin chay trong AutoCAD.
- `ShopDrawing.Installer`: app ngoai AutoCAD, cai `ApplicationPlugins` bundle sau khi AutoCAD dong.
- `latest.json`: manifest release duoc publish cung release asset.
- `.github/workflows/release.yml`: pipeline build theo tag `v*`.

## Cau hinh update

Plugin doc file `update-settings.json` nam cung thu muc cai dat.

Mac dinh trong repo:

```json
{
  "manifestUrl": "",
  "checkOnStartup": true,
  "checkDelaySeconds": 8,
  "channelName": "stable"
}
```

Trong GitHub Actions, workflow se tu dien `manifestUrl` thanh:

```text
https://github.com/<owner>/<repo>/releases/latest/download/latest.json
```

## Release flow

1. Dev push code len branch chinh.
2. Dev tao tag: `v1.0.0`.
3. GitHub Actions tren self-hosted runner Windows co AutoCAD 2026 se:
   - restore solution
   - build `ShopDrawing.Plugin`
   - publish `ShopDrawing.Installer`
   - tao `ShopDrawing.bundle.zip`
   - tao `ShopDrawing.Setup.<version>.zip`
   - tao `latest.json`
   - upload assets len GitHub Release
4. User mo AutoCAD, plugin check `latest.json`.
5. Neu co ban moi, plugin se mo `ShopDrawing.Installer.exe`.
6. User dong AutoCAD, installer cho process thoat roi cap nhat bundle trong `ApplicationPlugins`.

## Cai lan dau

- User tai `ShopDrawing.Setup.<version>.zip` tu release.
- Giai nen zip.
- Chay `ShopDrawing.Installer.exe`.
- Installer doc `install-settings.json` di kem, tu tai `ShopDrawing.bundle.zip` va cai vao `%AppData%\Autodesk\ApplicationPlugins`.

## Luu y ha tang

- Workflow hien tai can `self-hosted` Windows runner co AutoCAD 2026 cai o duong dan mac dinh.
- Plugin project dang reference thang vao AutoCAD DLL tai `C:\Program Files\Autodesk\AutoCAD 2026\`.
- `ShopDrawing.bundle.zip` la goi plugin cho AutoCAD bundle autoload.
- `ShopDrawing.Installer.exe` can duoc dat cung release asset de user cai lan dau va de plugin goi khi auto-update.
- `ShopDrawing.Setup.<version>.zip` la goi nen dua cho team khi phat hanh ban dau.
