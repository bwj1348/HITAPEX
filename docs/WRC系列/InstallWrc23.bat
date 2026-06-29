@echo off

set TARGET=APEX_Clothing_x64.dll
set ORIGINAL=APEX_Clothing_x64_org.dll
set PAYLOAD=WrcInjectionPayload.dll
set BINDIR=Engine/Binaries/ThirdParty/PhysX3/Win64/VS2015/

copy "%PAYLOAD%" "%BINDIR%"
pushd "%BINDIR%"

if not exist "%TARGET%" (
	echo "%TARGET%" not found.
	echo ERROR: Unzip "wrc-telemetry.zip" in WRC's installation folder then run this file again.
	echo The installation folder is "Steam\steamapps\common\EA SPORTS WRC".
	popd
	pause
	exit
)

if not exist "%PAYLOAD%" (
	echo "%PAYLOAD%" not found.
	echo ERROR: Unzip "wrc-telemetry.zip" in WRC's installation folder then run this file again.
	echo The installation folder is "Steam\steamapps\common\EA SPORTS WRC".
	popd
	pause
	exit
)

if exist "%ORIGINAL%" (
	echo "%ORIGINAL%" found.
	echo Reverting previous patch.

	copy "%ORIGINAL%" "%TARGET%"

	if errorlevel 1 (
		echo ERROR: Is WRC running? If so, please close WRC and try again.
		popd
		pause
		exit
	)
)

echo Installing patch.

copy "%TARGET%" "%ORIGINAL%"
copy "%PAYLOAD%" "%TARGET%"

if errorlevel 1 (
	echo ERROR: Is WRC running? If so, please close WRC and try again.
	popd
	pause
	exit
)

echo Done. You may close this window.
popd
pause
