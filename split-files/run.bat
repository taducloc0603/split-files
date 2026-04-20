@echo off
REM Launcher for File Splitter. Uses the Python launcher if available,
REM otherwise falls back to the `python` command on PATH.
where py >nul 2>nul
if %ERRORLEVEL%==0 (
    py -3 "%~dp0file_splitter.py"
) else (
    python "%~dp0file_splitter.py"
)
