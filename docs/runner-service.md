# Runner Stability Guide

Muc tieu: giu self-hosted runner on dinh cho release. Co 2 muc:

- muc 1: user-level autostart guard, khong can Administrator
- muc 2: Windows Service, on dinh nhat nhung can Administrator

## Muc 1: User-level autostart guard khong can Administrator

Chay trong root repo:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-runner-tasks.ps1
```

Tac dung:

- user dang nhap Windows la runner tu len
- co 1 guard chay nen trong user session
- neu runner khong chay thi guard tu mo lai

Go autostart:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install-runner-tasks.ps1 -Remove
```

## Muc 2: Windows Service

Nen lam khi may nay la build host chinh va co quyen Administrator.

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

Cai service truc tiep ngay trong qua trinh config:

```powershell
.\config.cmd remove
.\config.cmd --url https://github.com/huydaobk/shopdrawing-app --token <registration-token> --labels autocad2026 --name NGOC_HUY-shopdrawing --runasservice
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
.\config.cmd remove
```

## Sau khi chuyen service

- may reboot xong runner tu len lai ma khong can user login
- khong can mo `run.cmd` thu cong
- pipeline release on dinh nhat
