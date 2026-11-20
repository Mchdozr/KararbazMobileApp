# Nereye: Docker yığınını (Postgres, Redis, migrate, API, yönetim paneli) açar ve Expo'yu telefonda
# görülecek şekilde başlatır. Repo kökünde çalıştır:  powershell -ExecutionPolicy Bypass -File tools\calistir.ps1
# Ön koşul: Docker Desktop açık, Node.js 22+, telefon ve bilgisayar aynı Wi-Fi'de.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "1/4 Docker kontrol ediliyor..." -ForegroundColor Cyan
docker info *> $null
if ($LASTEXITCODE -ne 0) { throw "Docker çalışmıyor. Docker Desktop'ı açıp tekrar dene." }

Write-Host "2/4 Docker yığını derleniyor ve açılıyor (ilk sefer birkaç dakika sürer)..." -ForegroundColor Cyan
docker compose up -d --build
if ($LASTEXITCODE -ne 0) { throw "docker compose up başarısız." }

Write-Host "3/4 API bekleniyor (http://localhost:5079/health)..." -ForegroundColor Cyan
$ok = $false
for ($i = 0; $i -lt 60; $i++) {
  try { if ((Invoke-WebRequest -UseBasicParsing -TimeoutSec 3 http://localhost:5079/health).StatusCode -eq 200) { $ok = $true; break } } catch { }
  Start-Sleep -Seconds 2
}
if (-not $ok) { docker compose logs --tail 40 api; throw "API 2 dakikada açılmadı; yukarıdaki günlüğe bak." }

# Telefonun ulaşacağı adres: varsayılan ağ geçidi olan arayüzün IPv4 adresi (Wi-Fi / Ethernet).
$ip = (Get-NetIPConfiguration | Where-Object { $_.IPv4DefaultGateway -ne $null -and $_.NetAdapter.Status -eq 'Up' } |
  Select-Object -First 1).IPv4Address.IPAddress
if (-not $ip) { throw "Yerel ağ IP adresi bulunamadı. Wi-Fi'ye bağlı mısın?" }
$api = "http://$($ip):5079"
Write-Host "    Telefon API'ye şu adresten bağlanacak: $api" -ForegroundColor Green
Write-Host "    Telefon bağlanamazsa Windows Güvenlik Duvarı'nda 5079 ve 8081 portlarına izin ver." -ForegroundColor DarkYellow

Write-Host "4/4 Expo başlatılıyor. Expo Go ile QR kodu okut (iPhone: Kamera uygulaması)." -ForegroundColor Cyan
Set-Location (Join-Path $root 'apps\nereye-go')
if (-not (Test-Path node_modules) -or ((Get-Item package-lock.json).LastWriteTime -gt (Get-Item node_modules).LastWriteTime)) {
  npm install
}
$env:EXPO_PUBLIC_API_URL = $api
npx expo start --lan --clear
