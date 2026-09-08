@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\dev.ps1" -Action glass
if errorlevel 1 pause
