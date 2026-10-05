#!/usr/bin/env bash
set -euo pipefail
source /workspace/SignalDebt-setup/unity/unity-env.sh
"$SIGNAL_DEBT_UNITY" -batchmode -nographics -quit -job-worker-count 4 \
  -projectPath /workspace/SignalDebt-setup/unity/build-project \
  -logFile /workspace/SignalDebt-setup/unity/npc-key-r2-provider.log \
  -executeMethod SignalDebt.Editor.SignalDebtAiProviderChecks.RunBatch \
  -signalDebtAiProviderReport /workspace/SignalDebt-setup/unity/npc-key-r2-provider.json
python3 -c 'import json; d=json.load(open("/workspace/SignalDebt-setup/unity/npc-key-r2-provider.json")); assert d["passed"] and d["assertions"]>0; print("Provider passed:",d["assertions"])'
"$SIGNAL_DEBT_UNITY" -batchmode -nographics -quit -job-worker-count 4 \
  -projectPath /workspace/SignalDebt-setup/unity/build-project \
  -logFile /workspace/SignalDebt-setup/unity/npc-key-r2-conversation.log \
  -executeMethod SignalDebt.Editor.SignalDebtConversationChecks.RunBatch \
  -signalDebtConversationReport /workspace/SignalDebt-setup/unity/npc-key-r2-conversation.json
python3 -c 'import json; d=json.load(open("/workspace/SignalDebt-setup/unity/npc-key-r2-conversation.json")); assert d["passed"] and d["assertions"]>0; print("Conversation passed:",d["assertions"])'
