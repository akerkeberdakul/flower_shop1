@echo off
setlocal
cd /d "%~dp0"
if not exist bin\win-x64 mkdir bin\win-x64

where gcc >nul 2>nul
if %errorlevel%==0 (
  gcc -dumpmachine | findstr /i "x86_64" >nul
  if errorlevel 1 (
    echo gcc is not a 64-bit x86_64 compiler. Install MinGW-w64 x86_64 or use the x64 Native Tools Command Prompt for Visual Studio.
    exit /b 1
  )
  gcc -shared -O2 -Wall -Wextra -o bin\win-x64\flowerlib.dll flowerlib.c
  if errorlevel 1 exit /b 1
  goto built
)

where cl >nul 2>nul
if %errorlevel%==0 (
  cl /nologo /LD /O2 /W3 flowerlib.c /Fe:bin\win-x64\flowerlib.dll /Fo:bin\win-x64\
  if errorlevel 1 exit /b 1
  goto built
)

echo No C compiler found. Install MinGW-w64 (x86_64) so that gcc is on PATH, or run this script from the x64 Native Tools Command Prompt for Visual Studio.
exit /b 1

:built
echo Built native\bin\win-x64\flowerlib.dll
exit /b 0
