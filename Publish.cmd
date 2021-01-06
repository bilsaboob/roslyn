@echo off

powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\publish.ps1""" %*"