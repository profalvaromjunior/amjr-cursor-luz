@echo off
rem Gera o "AMJR Cursor Menino.exe" usando o compilador que ja vem no Windows
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set ORIGEM=%~dp0
set OBRA=%TEMP%\AMJR_compilar
rem Fecha o programa se ele ja estiver aberto
taskkill /im "AMJR Cursor Menino.exe" >nul 2>&1
timeout /t 2 /nobreak >nul
taskkill /im "AMJR Cursor Menino.exe" /f >nul 2>&1
rem Compila fora do OneDrive para o antivirus/sincronizacao nao atrapalhar
if exist "%OBRA%" rd /s /q "%OBRA%"
mkdir "%OBRA%"
copy /y "%ORIGEM%menino.ico" "%OBRA%\" >nul
copy /y "%ORIGEM%menino_animado.ani" "%OBRA%\" >nul
copy /y "%ORIGEM%menino_parado.cur" "%OBRA%\" >nul
copy /y "%ORIGEM%CursorMenino.cs" "%OBRA%\" >nul
cd /d "%OBRA%"
"%CSC%" /nologo /target:winexe /optimize /win32icon:menino.ico /resource:menino_animado.ani,menino_animado.ani /resource:menino_parado.cur,menino_parado.cur /out:"AMJR Cursor Menino.exe" CursorMenino.cs
if errorlevel 1 (pause & exit /b 1)
copy /y "%OBRA%\AMJR Cursor Menino.exe" "%ORIGEM%" >nul
if errorlevel 1 (echo Nao consegui copiar o .exe de volta - o Windows protege esta pasta. & echo O programa pronto esta em: %OBRA% & start "" "%OBRA%" & pause & exit /b 1)
cd /d "%ORIGEM%"
rd /s /q "%OBRA%" >nul 2>&1
start "" "%ORIGEM%AMJR Cursor Menino.exe"
