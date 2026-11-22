@echo off
rem Gera o "AMJR Cursor Luz.exe" usando o compilador que ja vem no Windows
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set ORIGEM=%~dp0
set OBRA=%TEMP%\AMJR_compilar
rem Fecha o programa se ele ja estiver aberto
taskkill /im "AMJR Cursor Luz.exe" >nul 2>&1
timeout /t 2 /nobreak >nul
taskkill /im "AMJR Cursor Luz.exe" /f >nul 2>&1
rem Compila fora do OneDrive para o antivirus/sincronizacao nao atrapalhar
if exist "%OBRA%" rd /s /q "%OBRA%"
mkdir "%OBRA%"
copy /y "%ORIGEM%cursorluz.ico" "%OBRA%\" >nul
copy /y "%ORIGEM%CursorLuz.cs" "%OBRA%\" >nul
cd /d "%OBRA%"
"%CSC%" /nologo /target:winexe /optimize /win32icon:cursorluz.ico /out:"AMJR Cursor Luz.exe" CursorLuz.cs
if errorlevel 1 (pause & exit /b 1)
copy /y "%OBRA%\AMJR Cursor Luz.exe" "%ORIGEM%" >nul
if errorlevel 1 (echo Nao consegui copiar o .exe de volta - o Windows protege esta pasta. & echo O programa pronto esta em: %OBRA% & start "" "%OBRA%" & pause & exit /b 1)
cd /d "%ORIGEM%"
rd /s /q "%OBRA%" >nul 2>&1
start "" "%ORIGEM%AMJR Cursor Luz.exe"
