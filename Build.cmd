@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command ". .\eng\build.ps1; RunBuild -build %*;"
