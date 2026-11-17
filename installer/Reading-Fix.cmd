@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Reading-Fix.ps1"
if errorlevel 1 pause
