# Runner Service Guide

Muc tieu: chuyen self-hosted runner tren may dev thanh Windows Service de release khong phu thuoc vao viec mo cua so terminal.

## Khi nao can lam

Nen lam ngay khi:

- may nay la may chinh de build release
- muon CI/CD chay on dinh sau khi reboot
- khong muon runner bi dung khi user logout

## Hien trang

Runner dang duoc cau hinh theo kieu interactive:

- folder runner: `C:\actions-runner-shopdrawing`
- labels: `self-hosted`, `Windows`, `X64`, `autocad2026`

Kieu nay van build duoc, nhung se mat runner neu:

- dong cua so runner
- logout Windows session
- reboot may ma khong mo lai runner

## Cach chuyen sang Windows Service

Can mo PowerShell voi quyen `Run as Administrator`.

Di chuyen vao folder runner:

```powershell
cd C:\actions-runner-shopdrawing
```

Cai service:

```powershell
.\svc install
.\svc start
```

Kiem tra service:

```powershell
Get-Service | Where-Object { $_.Name -like "actions.runner*" }
```

Kiem tra runner tren GitHub:

```powershell
gh api repos/huydaobk/shopdrawing-app/actions/runners
```

## Neu can go service

```powershell
cd C:\actions-runner-shopdrawing
.\svc stop
.\svc uninstall
```

## Sau khi chuyen service

- may reboot xong runner tu len lai
- khong can mo `run.cmd` thu cong
- pipeline release on dinh hon ro ret
