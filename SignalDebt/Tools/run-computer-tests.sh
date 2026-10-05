#!/usr/bin/env bash
# Real computer-session policy tests; no Unity camera or rendering is exercised.
set -euo pipefail
setup_root=${SIGNAL_DEBT_SETUP_ROOT:-/workspace/SignalDebt-setup}
source_root=${1:-"$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../UnityProject" && pwd)"}
sdk_root=${DOTNET_ROOT:-/workspace/.local/opt/dotnet}
harness_root="$setup_root/computer-harness"
if [ ! -x "$sdk_root/dotnet" ]; then
    printf 'Missing .NET SDK: run SignalDebt/Tools/install-dotnet.sh first.\n' >&2
    exit 1
fi
mkdir -p "$harness_root" "$setup_root/dotnet-home" "$setup_root/nuget-packages"
export DOTNET_ROOT="$sdk_root"
export DOTNET_CLI_HOME="$setup_root/dotnet-home"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true
export NUGET_PACKAGES="$setup_root/nuget-packages"
python3 - "$source_root" "$harness_root" <<'PY'
from pathlib import Path
import json, sys, xml.etree.ElementTree as ET

root = Path(sys.argv[1]).resolve()
dest = Path(sys.argv[2])
sources = [root / 'Assets/Scripts/SignalDebtComputerSession.cs',
           root / 'Assets/Editor/SignalDebtComputerTests.cs']
for path in sources:
    if not path.is_file():
        raise SystemExit('Missing harness source: ' + str(path))
project = ET.Element('Project', Sdk='Microsoft.NET.Sdk')
props = ET.SubElement(project, 'PropertyGroup')
for name, value in {'OutputType': 'Exe', 'TargetFramework': 'net8.0',
                    'DefineConstants': 'SIGNAL_DEBT_COMPUTER_TEST',
                    'EnableDefaultCompileItems': 'false', 'ImplicitUsings': 'disable',
                    'Nullable': 'disable', 'RestoreIgnoreFailedSources': 'true'}.items():
    ET.SubElement(props, name).text = value
items = ET.SubElement(project, 'ItemGroup')
for path in sources:
    ET.SubElement(items, 'Compile', Include=str(path))
ET.indent(project)
(dest / 'SignalDebtComputerTests.csproj').write_text(ET.tostring(project, encoding='unicode') + '\n')
(dest / 'global.json').write_text(json.dumps({'sdk': {'version': '8.0.425', 'rollForward': 'disable'}}, indent=2) + '\n')
(dest / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>\n')
PY
cd "$harness_root"
"$sdk_root/dotnet" restore SignalDebtComputerTests.csproj --configfile NuGet.Config --verbosity minimal
"$sdk_root/dotnet" build SignalDebtComputerTests.csproj --no-restore --configuration Release --verbosity minimal
"$sdk_root/dotnet" bin/Release/net8.0/SignalDebtComputerTests.dll | tee "$setup_root/computer-tests.txt"
python3 - "$source_root" "$setup_root" <<'PY'
from pathlib import Path
import hashlib, json, re, sys

root = Path(sys.argv[1]).resolve()
setup = Path(sys.argv[2])
output = (setup / 'computer-tests.txt').read_text()
match = re.fullmatch(r'PASS: (\d+) computer-session assertions; no Unity runtime exercised\.\s*', output)
if not match or int(match.group(1)) == 0:
    raise SystemExit('No successful nonzero test result; inspect computer-tests.txt')
sources = [root / 'Assets/Scripts/SignalDebtComputerSession.cs',
           root / 'Assets/Editor/SignalDebtComputerTests.cs']
report = {'sdk': '8.0.425', 'target_framework': 'net8.0',
          'assertions_passed': int(match.group(1)), 'unity_runtime_exercised': False,
          'scope': 'Computer session state, input eligibility, transition timing, interruption and reset only; no camera visuals, Unity APIs or UI layout.',
          'external_package_sources': [],
          'source_sha256': {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}}
(setup / 'computer-tests.json').write_text(json.dumps(report, indent=2) + '\n')
print('Recorded ' + str(setup / 'computer-tests.json'))
PY
