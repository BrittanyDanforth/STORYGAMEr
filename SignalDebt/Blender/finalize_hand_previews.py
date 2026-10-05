"""Assemble offline animated GIF reviews from completed Blender renders.

Requires Pillow and ffmpeg. First run finalize_animation_preview.py after the
motion and still render jobs finish, then run this file after the patrol grip
render finishes. No network access or game runtime imports are used.
"""
from pathlib import Path
import hashlib
import json
import subprocess
from PIL import Image, ImageDraw, ImageFont
from preview_render_manifest import verify_render_manifest

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Blender/Renders/Animation"
SOURCE = ROOT / "Blender/Character_Animation_Showcase.blend"
GRIP_FRAMES = OUT / "MotionFrames_Grip"


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_png(path, source_mtime):
    if path.stat().st_mtime < source_mtime:
        raise ValueError(f"Stale grip render: {path.name}")
    with Image.open(path) as image:
        image.verify()
    with Image.open(path) as image:
        image.load()
        if image.format != "PNG" or image.size != (640, 480):
            raise ValueError(f"Wrong grip render dimensions or format: {path.name}")


def encode_gif(inputs, preprocessing, path):
    temporary = path.with_name(path.stem + ".pending.gif")
    filters = (
        preprocessing + ",split[palette_source][frames];"
        "[palette_source]palettegen=stats_mode=full[palette];"
        "[frames][palette]paletteuse=dither=sierra2_4a"
    )
    subprocess.run(
        ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y",
         "-filter_complex_threads", "1", *inputs,
         "-filter_complex", filters, "-loop", "0", str(temporary)],
        check=True,
    )
    return temporary


def verify_gif(path, expected_frames, expected_size):
    durations = []
    with Image.open(path) as image:
        if image.format != "GIF" or image.size != expected_size:
            raise ValueError(f"Unexpected GIF dimensions or format: {path.name}")
        if image.info.get("loop") != 0 or image.n_frames != expected_frames:
            raise ValueError(f"Unexpected GIF loop or frame count: {path.name}")
        for index in range(image.n_frames):
            image.seek(index)
            image.load()
            image.convert("RGB").load()
            durations.append(image.info.get("duration", 0))
    duration_ms = sum(durations)
    if any(duration <= 0 for duration in durations):
        raise ValueError(f"Missing GIF frame duration: {path.name}")
    if abs(duration_ms - expected_frames * 1000 / 30) > 30:
        raise ValueError(f"Unexpected GIF duration: {path.name}: {duration_ms} ms")
    decoded = subprocess.run(
        ["ffmpeg", "-hide_banner", "-v", "error", "-i", str(path),
         "-f", "null", "-"], capture_output=True, text=True, check=True,
    )
    if decoded.stderr.strip():
        raise ValueError(f"GIF decoder reported an error: {path.name}")
    return {
        "frame_count": expected_frames,
        "width": expected_size[0], "height": expected_size[1],
        "duration_ms": duration_ms, "duration_seconds": duration_ms / 1000,
        "frame_duration_ms_values": sorted(set(durations)),
        "loop": 0, "all_frames_fully_decoded": True,
        "ffmpeg_full_decode_errors": 0, "sha256": sha256(path),
    }


def grip_contact_sheet():
    """Readable key poses from the same complete 91-frame source sequence."""
    try:
        heading = ImageFont.truetype('DejaVuSans.ttf', 22)
        caption = ImageFont.truetype('DejaVuSans.ttf', 14)
    except OSError:
        heading = caption = ImageFont.load_default()
    canvas = Image.new('RGB', (1280, 610), '#18222b')
    draw = ImageDraw.Draw(canvas)
    draw.text((18, 14), 'Patrol | shoulder, elbow and grip continuity', font=heading, fill='white')
    indices = ((1, 'low ready'), (16, 'raising'), (31, 'raising'), (46, 'aimed hold'),
               (47, 'lowering begins'), (61, 'lowering'), (76, 'lowering'), (91, 'low ready'))
    for index, (frame, label) in enumerate(indices):
        x, y = (index % 4) * 320, 56 + (index // 4) * 274
        with Image.open(GRIP_FRAMES / f'frame_{frame:04d}.png') as image:
            canvas.paste(image.convert('RGB').resize((320, 240), Image.Resampling.LANCZOS), (x, y))
        draw.text((x + 10, y + 249), f'Frame {frame:02d} | {label}', font=caption, fill='#b9cbd8')
    path = OUT / 'Patrol_UpperBody_Contact_Sheet.png'
    canvas.save(path)
    return path


def main():
    source_hash = sha256(SOURCE)
    source_mtime = SOURCE.stat().st_mtime
    video = OUT / "Police_Animation_Review.mp4"
    video_report = json.loads((ROOT / "Validation/animation_preview_validation.json").read_text())
    if video_report["source_sha256"] != source_hash:
        raise ValueError("Animation preview report belongs to a different Blender source")
    if video_report["video_sha256"] != sha256(video):
        raise ValueError("Animation preview video changed after validation")
    if video.stat().st_mtime < source_mtime:
        raise ValueError("Animation preview video predates the Blender source")

    frames = [GRIP_FRAMES / f"frame_{index:04d}.png" for index in range(1, 92)]
    if sorted(GRIP_FRAMES.glob("frame_*.png")) != frames:
        raise ValueError("Expected exactly grip frames 1 through 91")
    for path in frames:
        verify_png(path, source_mtime)
    grip_manifest=verify_render_manifest(ROOT/'Validation/patrol_grip_render.json',SOURCE,frames)

    swat = OUT / "SWAT_Door_Kick.gif"
    grip = OUT / "Patrol_Grip_Review.gif"
    swat_pending = encode_gif(["-i", str(video)], "crop=464:620:360:20,fps=30", swat)
    swat_metadata = verify_gif(swat_pending, 61, (464, 620))
    grip_pending = encode_gif(
        ["-framerate", "30", "-i", str(GRIP_FRAMES / "frame_%04d.png")],
        "fps=30", grip,
    )
    grip_metadata = verify_gif(grip_pending, 91, (640, 480))
    if sha256(SOURCE) != source_hash:
        raise ValueError("Blender source changed during preview assembly")
    swat_pending.replace(swat)
    grip_pending.replace(grip)
    contact_sheet=grip_contact_sheet()
    report = {
        "source": str(SOURCE.relative_to(ROOT)), "source_sha256": source_hash,
        "video": str(video.relative_to(ROOT)), "video_sha256": sha256(video),
        "grip_pngs_verified": len(frames), "grip_png_size": [640, 480],
        "png_verify_and_full_decode": True,
        "all_grip_renders_newer_than_frozen_source": True,
        "source_and_render_script_hashes_verified": True,
        "grip_render_settings": grip_manifest['render_settings'],
        "grip_frame_sha256": {path.name: sha256(path) for path in frames},
        "contact_sheet": str(contact_sheet.relative_to(ROOT)),
        "contact_sheet_sha256": sha256(contact_sheet),
        "gifs": {
            str(swat.relative_to(ROOT)): swat_metadata,
            str(grip.relative_to(ROOT)): grip_metadata,
        },
        "authoring_fps": 30,
        "cycles_grip_samples": 64,
        "grip_camera_note": "Wider front three-quarter view includes both shoulders, elbows, wrists and prop across raise/lower.",
        "timing_note": "GIF stores centiseconds; 30 fps is represented by 30/40 ms frame delays.",
        "source_note": "Both GIFs use actual Blender renders of the frozen animation source.",
        "limits": "Offline animation review; does not establish Unity runtime behavior or performance.",
    }
    target = ROOT / "Validation/hand_preview_validation.json"
    target.write_text(json.dumps(report, indent=2) + "\n")
    print("HAND_PREVIEWS_COMPLETE", json.dumps({
        "source_sha256": source_hash, "grip_pngs_verified": len(frames),
        "gifs": report["gifs"],
    }))


if __name__ == "__main__":
    main()
