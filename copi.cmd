@echo off
REM Set instructions folder
set COPILOT_CUSTOM_INSTRUCTIONS_DIRS=%~dp0copilot-cli

REM Run Copilot CLI with any arguments passed
copilot --allow-all %*
