@echo off
REM run-windows.bat - Aliskanlik Takibi'ni Windows'ta ayaga kaldirir.
REM
REM Kullanim:
REM   run-windows.bat              tam yigin: PostgreSQL + API + frontend
REM   run-windows.bat --frontend   SADECE frontend testleri + derleme
REM                                (Docker ve .NET gerekmez - en hizli dogrulama)
REM
REM Gereksinimler (tam yigin): Docker Desktop, .NET 8 SDK, Node 20+

setlocal
cd /d "%~dp0"

where node >nul 2>&1
if errorlevel 1 (
  echo HATA: Node.js bulunamadi. https://nodejs.org adresinden 20 LTS kurun.
  pause & exit /b 1
)
echo ==^> Node.js: & node -v

if "%1"=="--frontend" goto frontend

where docker >nul 2>&1
if errorlevel 1 (
  echo HATA: Docker bulunamadi. Docker Desktop kurun.
  echo   Sadece frontend icin: run-windows.bat --frontend
  pause & exit /b 1
)
docker info >nul 2>&1
if errorlevel 1 (
  echo HATA: Docker kurulu ama calismiyor. Docker Desktop'i acin.
  pause & exit /b 1
)
echo ==^> Docker calisiyor

where dotnet >nul 2>&1
if errorlevel 1 (
  echo HATA: .NET SDK bulunamadi.
  echo   https://dotnet.microsoft.com/download/dotnet/8.0
  echo   Sadece frontend icin: run-windows.bat --frontend
  pause & exit /b 1
)
echo ==^> .NET SDK: & dotnet --version

if not exist ".env" (
  copy ".env.example" ".env" >nul
  echo ==^> .env olusturuldu.
)

echo ==^> PostgreSQL baslatiliyor...
call docker compose up -d
if errorlevel 1 goto hata
timeout /t 8 /nobreak >nul

echo ==^> Migration + demo veri...
call dotnet run --project backend/src/HabitTracker.Api -- seed
if errorlevel 1 goto hata
echo     Demo giris: demo@habittracker.local / Demo1234!

if not exist "frontend\node_modules" (
  echo ==^> Frontend bagimliliklari kuruluyor...
  pushd frontend & call npm install --no-audit --no-fund & popd
)

echo ==^> API ayri pencerede baslatiliyor ^(http://localhost:5000^)...
start "HabitTracker API" cmd /k dotnet run --project backend/src/HabitTracker.Api --urls http://localhost:5000
timeout /t 6 /nobreak >nul

echo.
echo ==^> Frontend: http://localhost:3000
echo     Demo giris: demo@habittracker.local / Demo1234!
echo     ^(Veritabanini kapatmak icin: docker compose down^)
echo.
start "" "http://localhost:3000"
cd frontend
call npm run dev
exit /b 0

:frontend
echo Yalnizca frontend - Docker ve .NET gerekmez.
echo.
cd frontend
if not exist "node_modules" (
  echo ==^> npm install...
  call npm install --no-audit --no-fund
  if errorlevel 1 goto hata
)
echo ==^> Birim testleri...
call npm test
if errorlevel 1 goto hata
echo ==^> Uretim derlemesi...
call npm run build
if errorlevel 1 goto hata
echo.
echo Frontend dogrulandi. Tam yigin icin: run-windows.bat
pause
exit /b 0

:hata
echo.
echo HATA olustu. Yukaridaki ciktiya bakin.
pause
exit /b 1
