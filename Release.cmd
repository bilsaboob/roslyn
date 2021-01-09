@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command ". .\eng\rsharp\release.ps1; Release %*;"