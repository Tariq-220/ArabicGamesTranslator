@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Fix-QuickMT.ps1"
if errorlevel 1 pause
