@echo off
setlocal
cd /d "%~dp0"

if exist "DropOrb.exe" del /q "DropOrb.exe"
powershell.exe -NoProfile -Command "$sources = Get-ChildItem -LiteralPath '.\src' -Filter '*.cs' | ForEach-Object FullName; Add-Type -Path $sources -ReferencedAssemblies 'System','System.Core','System.Windows.Forms','System.Drawing','System.Web.Extensions','System.IO.Compression','System.IO.Compression.FileSystem' -OutputAssembly '.\DropOrb.exe' -OutputType WindowsApplication"
if errorlevel 1 exit /b 1

echo Built %~dp0DropOrb.exe
exit /b 0
