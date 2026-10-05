# Blender environment setup

Verified on 2026-10-04: **Blender 5.2.2 LTS**, the stable release advertised on
[Blender's download page](https://www.blender.org/download/). Its release date is
2026-09-15. The downloaded executable reports release branch `blender-v5.2-release`
and build hash `d13f752e3b9c`.

## Official artifact and verification

- Archive: https://download.blender.org/release/Blender5.2/blender-5.2.2-linux-x64.tar.xz
- Checksum manifest: https://download.blender.org/release/Blender5.2/blender-5.2.2.sha256
- Release index: https://download.blender.org/release/Blender5.2/
- SHA256: `84098912789dc450e95697c4184fb8a90acbe5111c2ba4aede3fecb57806a168`

The installer downloads with HTTPS certificate verification enabled and compares
the archive to both the official checksum manifest and the reviewed pinned hash
before extraction or execution. The official website rejected Python's default
User-Agent with HTTP 403 / error 1010; a standard browser User-Agent resolved that
without changing TLS verification. No third-party binaries or scripts were used.

## Installation and startup

Run these commands from the SignalDebt project root in the cloud workspace.

```bash
bash Tools/install-blender.sh
source Tools/blender-env.sh
blender --version
```

The installation is under `/workspace/.local/opt/blender-5.2.2-linux-x64` with a
command symlink at `/workspace/.local/bin/blender`. `/usr/bin/blender` remains the
existing Debian 4.3.2 package. Always source `blender-env.sh` or use the explicit
new binary path. The startup script puts configuration and cache directories under
`/workspace/.local`, since the cloud home directory is read-only.

Use existing project checkouts in this isolated cloud environment. Do not create a
Git worktree unless the user explicitly requests one. Keep original supplied
assets intact and write edited project outputs to a separate review location.
Open unreviewed `.blend` files with `--disable-autoexec`. Do not execute embedded
scripts or downloaded helper scripts without inspecting them first.

When saving reusable environment configuration, inline the tested installer into
the install script rather than assuming this workspace-only helper file will be
present in a new environment. Required network hostnames are `www.blender.org`
and `download.blender.org`. No credentials are required.

## Validation

```bash
source Tools/blender-env.sh
blender --background --factory-startup --disable-autoexec --python-exit-code 1 \
  --python Tools/validate-blender.py
```

Successful run: `/workspace/SignalDebt-setup/validation/blender-s8q0_xwx/`.
The local test rendered the factory cube using Cycles on CPU (128 x 128, 8 samples,
4 threads), saved `smoke.blend`, and exported `smoke.fbx`. All three passed, with
nonempty output files and exit code 0. `results.json` records the actual runtime
version and result. No additional runtime packages were needed on this machine
(Linux x86_64, glibc 2.41).

This validates headless CPU rendering and source/export operations. Interactive
desktop display, GPU rendering, project-specific assets, and Unity playback are
separate checks and were not validated by this smoke test.
