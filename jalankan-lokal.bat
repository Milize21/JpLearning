@echo off
rem NomaX JP Learn - menjalankan aplikasi secara lokal (Windows).
rem
rem   jalankan-lokal.bat                 nyalakan semuanya lalu buka browser
rem   jalankan-lokal.bat status          lihat apa yang hidup
rem   jalankan-lokal.bat stop            matikan API, Gateway, web (Docker tetap jalan)
rem   jalankan-lokal.bat restart api     restart satu layanan: api ^| gateway ^| web
rem
rem Pekerjaan sebenarnya dilakukan scripts\dev-live.mjs (lihat infra\README.md, "Sistem live").
setlocal EnableExtensions
cd /d "%~dp0"

set "CMD=%~1"
if "%CMD%"=="" set "CMD=start"

if /i "%CMD%"=="status"  goto passthrough
if /i "%CMD%"=="stop"    goto passthrough
if /i "%CMD%"=="restart" goto passthrough
if /i "%CMD%"=="start"   goto start
echo Perintah tidak dikenal: %CMD%
echo Pakai: jalankan-lokal.bat [start^|status^|stop^|restart api^|gateway^|web]
exit /b 1

:passthrough
call :need node "Node.js 22+ (https://nodejs.org)" || exit /b 1
node scripts\dev-live.mjs %*
exit /b %errorlevel%

:start
echo == NomaX JP Learn: menyiapkan sistem lokal ==

call :need node "Node.js 22+ (https://nodejs.org)" || goto fail
call :need dotnet ".NET 10 SDK (https://dotnet.microsoft.com)" || goto fail
call :need docker "Docker Desktop (https://www.docker.com/products/docker-desktop)" || goto fail

if not exist "infra\.env" (
  echo [!] infra\.env belum ada.
  echo     Salin infra\.env.example menjadi infra\.env lalu ganti semua "change-me".
  goto fail
)

dotnet user-secrets list --project src\Nomax.Api 2>nul | findstr /b /c:"ConnectionStrings:Postgres" >nul
if errorlevel 1 (
  echo [!] Rahasia API belum diisi. Ikuti langkah 3 di infra\README.md:
  echo     dotnet user-secrets set "ConnectionStrings:Postgres" "..." --project src\Nomax.Api
  echo     dotnet user-secrets set "Keycloak:AdminClientSecret" "..." --project src\Nomax.Api
  goto fail
)

if not exist "web\node_modules" (
  echo -- Memasang paket frontend, sekali saja ^(npm ci^)...
  call npm --prefix web ci
  if errorlevel 1 goto fail
)

rem dev-live menyalakan Docker Desktop dan "docker compose up -d" sendiri bila perlu.
node scripts\dev-live.mjs start
if errorlevel 1 goto fail

echo -- Menunggu API dan web siap. Start pertama bisa 1-3 menit ^(build, migrasi, seed, Keycloak^)...
set /a WAITED=0
:wait
call :up http://localhost:5080/health/live && call :up http://localhost:8080/health/live && call :up http://localhost:5173/ && goto ready
if %WAITED% geq 300 goto slow
timeout /t 5 /nobreak >nul
set /a WAITED+=5
goto wait

:ready
echo.
node scripts\dev-live.mjs status
echo.
echo == Siap: http://localhost:5173 ==
echo    Akun dev: admin.dev / sensei.dev / murid1.dev ^(password di infra\README.md^)
echo    Matikan:  jalankan-lokal.bat stop
start "" http://localhost:5173
exit /b 0

:slow
echo.
echo [!] Belum siap setelah 5 menit. Status saat ini:
node scripts\dev-live.mjs status
echo     Cek log di .live\logs ^(build.log, api.log, supervisor.log^).
echo     Sistem tetap berjalan di latar belakang dan terus mencoba.
exit /b 1

:fail
echo.
echo Gagal menyalakan. Perbaiki hal di atas lalu jalankan lagi.
pause
exit /b 1

rem ---------------------------------------------------------------- helpers ----

:need
where %1 >nul 2>nul
if not errorlevel 1 exit /b 0
echo [!] %1 tidak ditemukan. Pasang %~2.
exit /b 1

:up
for /f %%c in ('curl.exe -s -o nul -w "%%{http_code}" --max-time 3 %1 2^>nul') do if "%%c"=="200" exit /b 0
exit /b 1
