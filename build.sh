#!/bin/bash

VER=$1

rm -rf builds
dotnet publish MercuryModder -p:Version=$VER --runtime linux-x64 -o "./builds/MercuryModder-linux-x64"
zip -r "builds/MercuryModder-linux-x64-$VER.zip" "./builds/MercuryModder-linux-x64"
dotnet publish MercuryModder -p:Version=$VER --runtime win-x64 -o "./builds/MercuryModder-windows-x64"
zip -r "builds/MercuryModder-windows-x64-$VER.zip" "./builds/MercuryModder-windows-x64"
