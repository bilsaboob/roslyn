@echo off

powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\package.ps1""" %*"
