# Deploy Runbook

Muc tieu: bien release thanh viec lap lai duoc, khong phu thuoc vao nho tay.

## Tong quan

- Team dung plugin da cai san tren may cua ho.
- GitHub Releases luu file cai va file update.
- May dev chi can bat khi can build release moi, vi self-hosted runner dang nam tren may nay.

## Sau khi cai xong, team co phu thuoc may dev khong

Khong.

- Plugin da cai trong AutoCAD cua tung may.
- File update da nam tren GitHub Release.
- May dev tat di, team van mo AutoCAD va dung plugin binh thuong.

May dev chi anh huong den viec:

- build ban moi
- tao release moi
- dua file cai moi len GitHub

## Quy trinh phat hanh chuan

1. Chot code can phat hanh tren `master`.
2. Dam bao branch da push len `origin/master`.
3. Tao tag version theo format `vX.Y.Z`.
4. GitHub Actions workflow `release` tu build va publish.
5. Gui file `ShopDrawing.Setup.X.Y.Z.zip` cho team neu can cai moi.
6. Team da cai plugin truoc do se nhan thong bao update qua `latest.json`.

## Lenh phat hanh

Chay trong root repo:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release.ps1 -Version 0.1.7
```

Neu local branch da commit xong nhung chua push:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release.ps1 -Version 0.1.7 -PushLatestCommit
```

Script se:

- check branch hien tai
- check working tree sach
- check tag chua ton tai
- tao tag `vX.Y.Z`
- push tag len GitHub de kich hoat release workflow

## Sau khi tag xong

Theo doi workflow:

```powershell
gh run list --workflow release --limit 3
gh run watch <run-id> --exit-status
```

Kiem tra release asset:

- `ShopDrawing.Setup.X.Y.Z.zip`
- `ShopDrawing.Installer.exe`
- `ShopDrawing.bundle.zip`
- `latest.json`

## File nao gui cho team

- Cai moi: gui `ShopDrawing.Setup.X.Y.Z.zip`
- Auto update: plugin tu doc `latest.json` va goi `ShopDrawing.Installer.exe`

## Khi nao can bat may dev

Can bat may dev khi:

- tao release moi
- workflow dang build

Khong can bat may dev khi:

- team dang dung plugin da cai
- team dang mo AutoCAD hang ngay
- team dang cai lai tu release da co san tren GitHub

## Diem yeu hien tai

Self-hosted runner dang chay interactive, chua la Windows Service.

He qua:

- neu may dev tat, logout, runner bi stop thi release moi se khong build
- release da co san tren GitHub van tai/cai duoc binh thuong

## Buoc nen lam tiep

Chuyen runner sang Windows Service bang quyen Administrator de CI/CD chay on dinh hon.
