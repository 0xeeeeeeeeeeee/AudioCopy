@echo off
set ASPNETCORE_URLS=http://+:23456
set ASPNETCORE_ENVIRONMENT=Development
set AudioCopy_hostToken=abcd
taskkill /t /f /im "libAudioCopy-Backend.exe"
.\libAudioCopy-Backend-standalone\libAudioCopy-Backend.exe
pause