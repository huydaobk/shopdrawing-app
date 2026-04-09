# Deploy Runbook

Muc tieu: bien release thanh viec lap lai duoc, khong phu thuoc vao nho tay.

## Tong quan

- Team dung plugin da cai san tren may cua ho.
- GitHub Releases luu file cai va file update.
- May dev chi can online khi can build release moi, vi self-hosted runner dang nam tren may nay.
- Runner da co them user-level guard de tu bat lai sau khi user dang nhap vao Windows.

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

Hoac dung script co san:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\watch-release.ps1 -Version 0.1.7
```

Kiem tra release asset:

- `ShopDrawing.Setup.X.Y.Z.zip`
- `ShopDrawing.Installer.exe`
- `ShopDrawing.bundle.zip`
- `latest.json`

Kiem tra runner truoc khi tag neu muon chac an:

```powershell
gh api repos/huydaobk/shopdrawing-app/actions/runners
```

## File nao gui cho team

- Cai moi: gui `ShopDrawing.Setup.X.Y.Z.zip`
- Auto update: plugin tu doc `latest.json` va goi `ShopDrawing.Installer.exe`

## Runtime data cua plugin

Plugin khong luu data runtime trong repo code hay thu muc cai dat.

Data theo du an se nam trong:

```text
ProjectRoot/
|-- Drawings/
|-- ShopDrawingData/
|   |-- shopdrawing_waste.db
|   |-- panel_specs.json
|   |-- tender_projects/
|   `-- logs/
`-- .shopdrawing-project.json
```

Y nghia:

- code/release tach rieng voi data du an
- gui khach chi can nen folder `Drawings`
- team mo cung mot project Dropbox se thay cung data
- git khong bi dinh db, autosave, artifact build

Co che nhan dien `ProjectRoot`:

- plugin lay duong dan file `.dwg` dang mo
- di nguoc len cac thu muc cha de tim `.shopdrawing-project.json`
- neu tim thay thi do la root du an
- neu chua tim thay:
  - neu file dang nam trong folder `Drawings` thi lay thu muc cha cua `Drawings`
  - neu khong thi lay chinh thu muc chua file `.dwg`
- plugin se tu tao marker file va `ShopDrawingData`

Fallback AppData chi dung khi:

- file `.dwg` chua duoc save
- khong lay duoc duong dan document hien tai
- khong xac dinh duoc root du an

## Khi nao can bat may dev

Can bat may dev khi:

- tao release moi
- workflow dang build

Khong can bat may dev khi:

- team dang dung plugin da cai
- team dang mo AutoCAD hang ngay
- team dang cai lai tu release da co san tren GitHub

## Do on dinh hien tai

Self-hosted runner hien tai da duoc bo sung guard user-level qua `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

Guard nay se:

- tu mo runner sau khi user dang nhap
- giu 1 tien trinh guard chay nen
- neu `Runner.Listener.exe` tat, guard tu mo lai

Runner van chua la Windows Service.

He qua:

- neu may dev tat, release moi se khong build
- neu Windows logout hoan toan, guard chi chay lai khi user dang nhap lai
- release da co san tren GitHub van tai/cai duoc binh thuong

## Buoc nen lam tiep

Neu muon muc on dinh cao nhat, chuyen runner sang Windows Service bang quyen Administrator.

Neu chua co quyen Administrator, cai guard bang lenh:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-runner-tasks.ps1
```

Tai lieu thao tac chi tiet:

- `docs/runner-service.md`

