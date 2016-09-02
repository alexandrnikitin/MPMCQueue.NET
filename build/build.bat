@echo off
cls
set encoding=utf-8
"..\.nuget\nuget.exe" "restore" "packages.config" "-OutputDirectory" "..\packages"
"..\packages\FAKE.4.39.0\tools\Fake.exe" build.fsx
pause