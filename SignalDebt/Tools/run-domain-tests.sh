#!/usr/bin/env bash
# Compile/run the existing Unity-independent harness without changing source.
set -euo pipefail
setup_root=/workspace/SignalDebt-setup
source_root=${1:-/workspace/STORYGAMEr/SignalDebt/UnityProject}
sdk_root=/workspace/.local/opt/dotnet
harness_root="$setup_root/domain-harness"
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
import json,sys,xml.etree.ElementTree as ET
root=Path(sys.argv[1]).resolve(); dest=Path(sys.argv[2])
sources=[root/'Assets/Scripts/SignalDebtSaveData.cs',root/'Assets/Editor/SignalDebtDomainTests.cs']
for path in sources:
    if not path.is_file(): raise SystemExit('Missing harness source: '+str(path))
project=ET.Element('Project',Sdk='Microsoft.NET.Sdk')
props=ET.SubElement(project,'PropertyGroup')
for name,value in {'OutputType':'Exe','TargetFramework':'net8.0','DefineConstants':'SIGNAL_DEBT_PORTABLE_TEST','EnableDefaultCompileItems':'false','ImplicitUsings':'disable','Nullable':'disable','RestoreIgnoreFailedSources':'true'}.items():
    ET.SubElement(props,name).text=value
items=ET.SubElement(project,'ItemGroup')
for path in sources: ET.SubElement(items,'Compile',Include=str(path))
ET.indent(project)
(dest/'SignalDebtDomainTests.csproj').write_text(ET.tostring(project,encoding='unicode')+'\n')
(dest/'global.json').write_text(json.dumps({'sdk':{'version':'8.0.425','rollForward':'disable'}},indent=2)+'\n')
(dest/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>\n')
PY
cd "$harness_root"
"$sdk_root/dotnet" restore SignalDebtDomainTests.csproj --configfile NuGet.Config --verbosity minimal > "$setup_root/domain-restore.log" 2>&1
"$sdk_root/dotnet" build SignalDebtDomainTests.csproj --no-restore --configuration Release --verbosity minimal > "$setup_root/domain-build.log" 2>&1
"$sdk_root/dotnet" bin/Release/net8.0/SignalDebtDomainTests.dll | tee "$setup_root/domain-tests.txt"
python3 - "$source_root" "$setup_root" <<'PY'
from pathlib import Path
import hashlib,json,sys,re
root=Path(sys.argv[1]).resolve(); setup=Path(sys.argv[2])
output=(setup/'domain-tests.txt').read_text()
match=re.search(r'^PASS: (\d+) domain assertions; no Unity runtime exercised\.$',output.strip())
if not match: raise SystemExit('Unexpected domain test result; inspect domain-tests.txt')
report={'sdk':'8.0.425','runtime':'8.0.31','target_framework':'net8.0','assertions_passed':int(match.group(1)),'unity_runtime_exercised':False,'external_package_sources':[],'source_sha256':{str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [root/'Assets/Scripts/SignalDebtSaveData.cs',root/'Assets/Editor/SignalDebtDomainTests.cs']}}
(setup/'domain-tests.json').write_text(json.dumps(report,indent=2)+'\n')
print('Recorded '+str(setup/'domain-tests.json'))
PY
