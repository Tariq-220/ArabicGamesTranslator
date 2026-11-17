@echo off
setlocal
rem ASCII launcher, Windows CRLF line endings.
cd /d "%~dp0"
set "MODEL=quickmt"
if "%~1"=="jisr-msa" set "MODEL=jisr-msa"
if "%~1"=="jisr-saudi" set "MODEL=jisr-saudi"
if "%~1"=="hy-mt" set "MODEL=hy-mt"
if exist "python\python.exe" goto portable
if exist ".venv\Scripts\python.exe" goto venv
where py >nul 2>nul
if errorlevel 1 goto usepython
py -3 -m venv .venv
if errorlevel 1 goto failed
goto venv
:usepython
python -m venv .venv
if errorlevel 1 goto failed
:venv
set "PYTHON=.venv\Scripts\python.exe"
goto dependencies
:portable
set "PYTHON=python\python.exe"
if exist "python\Lib\site-packages\pip" goto dependencies
"%PYTHON%" get-pip.py --no-warn-script-location
if errorlevel 1 goto failed
:dependencies
rem Repair existing installations too, before checking base-ready.
"%PYTHON%" -c "import pkg_resources" >nul 2>nul
if not errorlevel 1 goto basecheck
echo Repairing setuptools compatibility for CTranslate2...
"%PYTHON%" -m pip install --no-warn-script-location setuptools==75.8.0
if errorlevel 1 goto failed
:basecheck
if exist "base-ready" goto optional
"%PYTHON%" -m pip install -r requirements.txt
if errorlevel 1 goto failed
type nul > "base-ready"
:optional
if "%MODEL%"=="hy-mt" goto run
if "%MODEL%"=="quickmt" goto run
if exist "jisr-ready" goto run
"%PYTHON%" -m pip install torch==2.5.1 --index-url https://download.pytorch.org/whl/cpu
if errorlevel 1 goto failed
"%PYTHON%" -m pip install -r requirements-jisr.txt
if errorlevel 1 goto failed
type nul > "jisr-ready"
:run
echo Downloading or loading model. Wait for Ready. Keep this window open.
"%PYTHON%" server.py --host 127.0.0.1 --model "%MODEL%"
if errorlevel 1 goto failed
goto end
:failed
echo Setup failed. Check the error above, internet and free disk space.
echo Portable Python requires Windows 10 or 11, 64-bit.
echo Take a screenshot of the error before pressing a key.
pause
exit /b 1
:end
echo Local engine stopped.
pause
endlocal
