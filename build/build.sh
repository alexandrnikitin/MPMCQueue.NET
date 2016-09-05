#!/bin/bash
SCRIPT_PATH="${BASH_SOURCE[0]}";
if ([ -h "${SCRIPT_PATH}" ]) then
  while([ -h "${SCRIPT_PATH}" ]) do SCRIPT_PATH=`readlink "${SCRIPT_PATH}"`; done
fi
pushd . > /dev/null
cd `dirname ${SCRIPT_PATH}` > /dev/null
SCRIPT_PATH=`pwd`;
popd  > /dev/null

mono $SCRIPT_PATH/../.nuget/nuget.exe "restore" "$SCRIPT_PATH/packages.config" "-OutputDirectory" "$SCRIPT_PATH/../packages"
mono $SCRIPT_PATH/../packages/FAKE.4.39.0/tools/FAKE.exe BuildTests --fsiargs -d:MONO $SCRIPT_PATH/build.fsx