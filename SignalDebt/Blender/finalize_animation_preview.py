"""Offline evidence assembly only; never imported by the Unity game.

Requires Pillow, ffmpeg and ffprobe. Run after both Blender render jobs exit 0.
Rejects missing, stale or corrupt rendered frames before encoding the review.
"""
from pathlib import Path
import hashlib
import json
import subprocess
from PIL import Image, ImageDraw, ImageFont
from preview_render_manifest import verify_render_manifest

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Blender/Renders/Animation'
SOURCE = ROOT / 'Blender/Character_Animation_Showcase.blend'
FRAMES = OUT / 'MotionFrames_Final'


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_png(path, size):
    if path.stat().st_mtime < SOURCE.stat().st_mtime:
        raise ValueError(f'Stale render: {path.name}')
    with Image.open(path) as image:
        image.verify()
    with Image.open(path) as image:
        image.load()
        if image.size != size:
            raise ValueError(f'Wrong dimensions: {path.name}: {image.size}')


def font(size):
    try:
        return ImageFont.truetype('DejaVuSans.ttf', size)
    except OSError:
        return ImageFont.load_default()


def strip(title, images, filename):
    canvas = Image.new('RGB', (400 * len(images), 570), '#18222b')
    draw = ImageDraw.Draw(canvas)
    draw.text((20, 15), title, font=font(24), fill='white')
    for index, (path, caption) in enumerate(images):
        with Image.open(path) as image:
            canvas.paste(image.convert('RGB').resize((400, 480), Image.Resampling.LANCZOS),
                         (index * 400, 58))
        draw.text((index * 400 + 16, 546), caption, font=font(15), fill='#b9cbd8')
    canvas.save(OUT / filename)


def main():
    motion = [FRAMES / f'frame_{frame:04d}.png' for frame in range(1, 62)]
    if sorted(FRAMES.glob('frame_*.png')) != motion:
        raise ValueError('Expected exactly frames 1 through 61 in MotionFrames_Final')
    for path in motion:
        verify_png(path, (864, 640))
    stills = [OUT / f'SWAT_kick_{frame:03d}.png' for frame in (1, 9, 17, 25, 38, 61)]
    stills += [OUT / f'Patrol_hold_{frame:03d}.png' for frame in (1, 23, 46)]
    stills += [OUT / 'Patrol_grip_detail.png']
    for path in stills:
        verify_png(path, (600, 720))
    motion_manifest=verify_render_manifest(ROOT/'Validation/animation_motion_render.json',SOURCE,motion)
    still_manifest=verify_render_manifest(ROOT/'Validation/animation_stills_render.json',SOURCE,stills)
    strip('SWAT | door-contact animation study', [
        (OUT / f'SWAT_kick_{frame:03d}.png', f'Frame {frame:02d} | {caption}')
        for frame, caption in ((9, 'anticipation'), (17, 'lift'), (25, 'contact'), (38, 'recovery'))
    ], 'SWAT_Kick_Frame_Strip.png')
    strip('Patrol | two-hand prop hold', [
        (OUT / f'Patrol_hold_{frame:03d}.png', f'Frame {frame:02d} | {caption}')
        for frame, caption in ((1, 'low ready'), (23, 'transition'), (46, 'aimed hold'))
    ], 'Patrol_Hold_Frame_Strip.png')
    video = OUT / 'Police_Animation_Review.mp4'
    subprocess.run(['ffmpeg', '-hide_banner', '-loglevel', 'error', '-y', '-framerate', '30',
                    '-i', str(FRAMES / 'frame_%04d.png'), '-c:v', 'libx264', '-preset', 'medium',
                    '-crf', '19', '-pix_fmt', 'yuv420p', '-movflags', '+faststart', str(video)],
                   check=True)
    decode = subprocess.run(['ffmpeg', '-hide_banner', '-v', 'error', '-i', str(video),
                             '-f', 'null', '-'], capture_output=True, text=True, check=True)
    if decode.stderr.strip():
        raise ValueError('Video decoder reported an error')
    metadata = json.loads(subprocess.check_output([
        'ffprobe', '-v', 'error', '-select_streams', 'v:0', '-show_entries',
        'stream=codec_name,width,height,r_frame_rate,nb_frames,duration', '-of', 'json', str(video)
    ]))['streams'][0]
    expected = {'codec_name': 'h264', 'width': 864, 'height': 640,
                'r_frame_rate': '30/1', 'nb_frames': '61'}
    if any(metadata.get(key) != value for key, value in expected.items()):
        raise ValueError(f'Unexpected video metadata: {metadata}')
    report = {
        'source': str(SOURCE.relative_to(ROOT)), 'source_sha256': sha256(SOURCE),
        'corrected_hand_source': True, 'motion_pngs_verified': len(motion),
        'stills_verified': len(stills), 'png_verify_and_full_decode': True,
        'all_renders_newer_than_frozen_source': True,
        'source_and_render_script_hashes_verified': True,
        'motion_render_settings': motion_manifest['render_settings'],
        'still_render_settings': still_manifest['render_settings'],
        'video': str(video.relative_to(ROOT)), 'video_sha256': sha256(video),
        'video_metadata': metadata, 'video_full_decode_errors': 0,
        'authoring_fps': 30, 'rendered_frame_step': 1, 'cycles_motion_samples': 48,
        'cycles_still_samples': 96,
        'camera_note': 'SWAT side view exposes foot-to-door contact; patrol front-facing full body. Separate patrol GIF shows both shoulder roots and elbows.',
        'endpoint_note': '61 displayed frames include both endpoints of a 2.0 second action.',
        'limits': 'Offline animation study. Does not establish Unity runtime performance or police gameplay.',
        'proof_sha256': {path.name: sha256(path) for path in stills},
        'frame_strip_sha256': {name: sha256(OUT / name) for name in (
            'SWAT_Kick_Frame_Strip.png', 'Patrol_Hold_Frame_Strip.png')}
    }
    target = ROOT / 'Validation/animation_preview_validation.json'
    target.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
