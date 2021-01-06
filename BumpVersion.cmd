@echo off

powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\bump-version.ps1""" %*"
