@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command ". .\eng\rsharp\publish.ps1; Publish %*;"