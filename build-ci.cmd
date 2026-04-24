@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-ci.ps1" %*
