@echo off
powershell.exe -NoProfile -File "%~dp0scripts\Start-Web.ps1"
if errorlevel 1 pause
