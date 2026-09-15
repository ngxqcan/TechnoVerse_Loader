@echo off
title Publish Loader to TechnoVerse Bot
echo ===============================================================
echo            TECHNOVERSE LOADER - BOT PUBLISHER
echo ===============================================================
echo.

cd /d "%~dp0"
set "BOT_DIR=..\TechnoVerse-bot\backend\data\loader_files\_main_loader"

echo [1/3] Dang bien dich Release Lightweight EXE (~560KB, Framework-Dependent)...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./dist/framework-dependent

if %errorlevel% neq 0 (
    echo [ERROR] Bien dich that bai!
    pause
    exit /b %errorlevel%
)

echo.
echo [2/3] Dang chuan bi thu muc dich: %BOT_DIR%
if not exist "%BOT_DIR%" mkdir "%BOT_DIR%"

echo [3/3] Dang copy TechnoVerseLoader.exe vao kho du lieu Bot...
copy /y ".\dist\framework-dependent\TechnoVerseLoader.exe" "%BOT_DIR%\TechnoVerseLoader.exe"

if %errorlevel% equ 0 (
    echo.
    echo ===============================================================
    echo  [THANH CONG] File Loader chinh da duoc cap nhat vao Bot!
    echo.
    echo  Vi tri luu tru:
    echo  %BOT_DIR%\TechnoVerseLoader.exe
    echo.
    echo  Khach hang tren Discord hoac Web Dashboard gio day co the
    echo  tai truc tiep Loader moi nhat qua:
    echo  https://technoverse-backend-production.up.railway.app/api/loader/download
    echo ===============================================================
) else (
    echo [ERROR] Khong the copy file vao thu muc Bot.
)

pause
