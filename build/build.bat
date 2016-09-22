@echo off
cls
set encoding=utf-8
SET buildpath=%~dp0
"%buildpath%..\.nuget\nuget.exe" "restore" "%buildpath%packages.config" "-OutputDirectory" "%buildpath%..\packages"
"%buildpath%..\packages\FAKE.4.39.0\tools\Fake.exe" %buildpath%build.fsx
pause