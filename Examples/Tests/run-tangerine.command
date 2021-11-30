#!/bin/bash
DIR=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
cd "${DIR}"
./../../Orange/Launcher/bin/Mac/Release/Launcher.app/Contents/MacOS/Launcher -b Tangerine/Tangerine.Mac.sln -r Tangerine/Tangerine/bin/Release/Tangerine.app
