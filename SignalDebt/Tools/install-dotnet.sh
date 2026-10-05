#!/usr/bin/env bash
# Official Microsoft SDK for the portable Signal Debt domain harness only.
set -euo pipefail
setup_root=/workspace/SignalDebt-setup
sdk_root=/workspace/.local/opt/dotnet
sdk_version=8.0.425
sdk_url=https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.425/dotnet-sdk-8.0.425-linux-x64.tar.gz
sdk_sha512=934b8060a7190e5909ad1fd0785db542f487b3bbf6cdd14826b02095fdd0d0394298b1634085eff302928fccc33f7c1a7253e9b87df555fc36fce819bcd2e798
archive="$setup_root/downloads/dotnet-sdk-$sdk_version-linux-x64.tar.gz"
mkdir -p "$setup_root/downloads" "$sdk_root" "$setup_root/dotnet-home" "$setup_root/nuget-packages"
export DOTNET_ROOT="$sdk_root"
export DOTNET_CLI_HOME="$setup_root/dotnet-home"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true
export NUGET_PACKAGES="$setup_root/nuget-packages"
# The SHA-512 is pinned from Microsoft's HTTPS release metadata for .NET 8.
if [ ! -f "$archive" ]; then
    curl --fail --silent --show-error --location --proto '=https' --proto-redir '=https' --connect-timeout 20 --max-time 300 "$sdk_url" -o "$archive.pending"
    printf '%s  %s\n' "$sdk_sha512" "$archive.pending" | sha512sum --check --status
    mv "$archive.pending" "$archive"
fi
printf '%s  %s\n' "$sdk_sha512" "$archive" | sha512sum --check --status
if [ ! -f "$sdk_root/sdk/$sdk_version/dotnet.dll" ]; then
    tar -xzf "$archive" -C "$sdk_root" --no-same-owner
fi
"$sdk_root/dotnet" --list-sdks
