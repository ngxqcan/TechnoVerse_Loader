@echo off
title TechnoVerse Loader Builder
echo ===============================================================
echo               TECHNOVERSE LOADER - BUILD MENU
echo ===============================================================
echo.
echo  [1] Build Ban Standalone (Self-Contained ~50MB, chay ngay tren moi may)
echo  [2] Build Ban Lightweight (Framework-Dependent ~500KB, can co .NET)
echo  [3] Build ca 2 ban
echo.
set /p choice="Chon phuong thuc bien dich [1-3] (Mac dinh 1): "
if "%choice%"=="" set choice=1

cd /d "%~dp0"

if "%choice%"=="1" goto build_standalone
if "%choice%"=="2" goto build_lightweight
if "%choice%"=="3" goto build_all

:build_standalone
echo.
echo [Build] Dang bien dich Standalone (Self-Contained)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o ./dist/standalone
echo Output: %~dp0dist\standalone\TechnoVerseLoader.exe
goto done

:build_lightweight
echo.
echo [Build] Dang bien dich Lightweight (Framework-Dependent)...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./dist/framework-dependent
echo Output: %~dp0dist\framework-dependent\TechnoVerseLoader.exe
goto done

:build_all
echo.
echo [Build] 1/2 Dang bien dich Standalone...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o ./dist/standalone
echo [Build] 2/2 Dang bien dich Lightweight...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./dist/framework-dependent
goto done

:done
echo.
echo ===============================================================
echo  Bien dich hoan tat thanh cong!
echo ===============================================================
pause
