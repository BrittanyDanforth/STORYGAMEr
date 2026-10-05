# Source this file before Blender commands in the cloud workspace.
export PATH="/workspace/.local/bin:$PATH"
export XDG_CONFIG_HOME=/workspace/.local/config
export XDG_CACHE_HOME=/workspace/.local/cache
export XDG_DATA_HOME=/workspace/.local/share
mkdir -p "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME" "$XDG_DATA_HOME"
