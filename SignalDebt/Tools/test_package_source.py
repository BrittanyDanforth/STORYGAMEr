"""Exercise real Git/ZIP packaging using disposable source repositories only."""
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from zipfile import ZipFile


SPEC = importlib.util.spec_from_file_location('package_source', Path(__file__).with_name('package-source.py'))
PACKAGE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PACKAGE)


class PackageSourceTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.repository = self.root / 'repository'
        self.repository.mkdir()
        self.project = self.repository / 'SignalDebt'
        self.project.mkdir()
        self.output = self.root / 'output'
        self.git('init', '--quiet')
        self.git('config', 'user.name', 'Packaging Tests')
        self.git('config', 'user.email', 'packaging-tests@example.invalid')
        self.write('README.md', 'committed source\n')
        self.write('remove.txt', 'deleted in the snapshot\n')
        self.write('.gitignore', 'AI/.env\nAI/data/\nignored/\n*.pyc\n')
        self.write('AI/.env.example', 'OPENAI_API_KEY=\n')
        self.commit()

    def git(self, *arguments):
        return subprocess.check_output(['git', '-C', str(self.repository), *arguments], stderr=subprocess.PIPE)

    def write(self, relative, content):
        path = self.project / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)
        return path

    def commit(self):
        self.git('add', '--all')
        self.git('commit', '--quiet', '-m', 'Fixture source')

    def package(self, working_tree=False, name=PACKAGE.DEFAULT_NAME):
        return PACKAGE.package_source(self.project, self.output, name, working_tree)

    def verify_archives(self, report):
        manifest = json.loads((self.output / 'package-manifest.json').read_text())
        self.assertEqual(manifest, report)
        contents = {}
        for index, archive in enumerate(report['archives']):
            path = self.output / archive['path']
            self.assertEqual(path.stat().st_size, archive['bytes'])
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), archive['sha256'])
            with ZipFile(path) as zipped:
                self.assertIsNone(zipped.testzip())
                entries = {item.filename: zipped.read(item) for item in zipped.infolist() if not item.is_dir()}
            if index == 0:
                full = entries
            else:
                self.assertLess(path.stat().st_size, 32 * 1024 * 1024)
                self.assertFalse(contents.keys() & entries.keys(), 'Transfer parts must contain distinct files')
                contents.update(entries)
        self.assertEqual(contents, full)
        self.assertEqual(report['file_count'], len(full))
        self.assertEqual({entry['path'] for entry in report['files']}, full.keys())
        for entry in report['files']:
            self.assertEqual(entry['bytes'], len(full[entry['path']]))
            self.assertEqual(entry['sha256'], hashlib.sha256(full[entry['path']]).hexdigest())
        return full

    def test_default_preserves_committed_revision_and_names(self):
        report = self.package()
        self.assertEqual(report['snapshot_kind'], 'committed_revision')
        self.assertEqual(report['base_git_commit'], self.git('rev-parse', 'HEAD').decode().strip())
        self.assertEqual(report['git_commit'], report['base_git_commit'])
        self.assertFalse(report['dirty'])
        self.assertEqual(report['archives'][0]['path'], 'SignalDebt_Quality_Pass.zip')
        contents = self.verify_archives(report)
        self.assertEqual(contents['SignalDebt/README.md'], b'committed source\n')

    def test_default_rejects_both_edited_and_new_files(self):
        self.write('README.md', 'edited\n')
        with self.assertRaisesRegex(ValueError, 'Commit the SignalDebt changes'):
            self.package()
        self.git('checkout', '--', 'SignalDebt/README.md')
        self.write('new.txt', 'new source\n')
        with self.assertRaisesRegex(ValueError, 'Commit the SignalDebt changes'):
            self.package()
        self.assertFalse(self.output.exists())

    def test_working_tree_packages_exact_current_source_and_preserves_edits(self):
        self.write('README.md', 'edited current source\n')
        self.write('UnityProject/Assets/Scripts/New.cs', 'new source\n')
        self.write('new file with spaces.txt', 'new with spaces\n')
        (self.project / 'remove.txt').unlink()
        self.write('AI/.env', 'ignored private value\n')
        self.write('AI/data/local.sqlite', 'ignored saved data\n')
        self.write('ignored/private.txt', 'ignored private file\n')
        self.write('__pycache__/scratch.txt', 'cache even without an ignore rule\n')
        self.write('UnityProject/Library/local.txt', 'Unity cache\n')
        self.write('saves/slot.json', 'local save\n')
        self.write('exports/old.zip', 'earlier output\n')
        self.write('.env.production', 'untracked live environment\n')
        (self.repository / 'other-game.txt').write_text('not SignalDebt\n')
        before = self.git('status', '--porcelain', '--untracked-files=all')
        report = self.package(working_tree=True, name='SignalDebt_Master')
        self.assertEqual(before, self.git('status', '--porcelain', '--untracked-files=all'))
        self.assertTrue(report['dirty'])
        self.assertEqual(report['snapshot_kind'], 'working_tree')
        self.assertIsNone(report['git_commit'])
        self.assertEqual(report['base_git_commit'], self.git('rev-parse', 'HEAD').decode().strip())
        self.assertEqual(report['archives'][0]['path'], 'SignalDebt_Master.zip')
        contents = self.verify_archives(report)
        self.assertEqual(set(contents), {
            'SignalDebt/README.md', 'SignalDebt/.gitignore', 'SignalDebt/AI/.env.example',
            'SignalDebt/UnityProject/Assets/Scripts/New.cs', 'SignalDebt/new file with spaces.txt',
        })
        self.assertEqual(contents['SignalDebt/README.md'], b'edited current source\n')
        self.assertEqual(contents['SignalDebt/UnityProject/Assets/Scripts/New.cs'], b'new source\n')

    def test_staged_deletion_is_absent_from_working_snapshot(self):
        self.git('rm', '--quiet', 'SignalDebt/remove.txt')
        contents = self.verify_archives(self.package(working_tree=True))
        self.assertNotIn('SignalDebt/remove.txt', contents)

    def test_working_tree_omits_nonignored_portable_userdata(self):
        paths = ('UserData/ai-settings.json', 'UnityProject/uSeRdAtA/desktop.json')
        for relative in paths:
            self.write(relative, 'synthetic private-data fixture\n')
        status = self.git('status', '--porcelain', '--untracked-files=all')
        for relative in paths:
            self.assertIn(('?? SignalDebt/' + relative + '\n').encode(), status)
        contents = self.verify_archives(self.package(working_tree=True))
        for relative in paths:
            self.assertNotIn('SignalDebt/' + relative, contents)
            self.assertEqual((self.project / relative).read_text(), 'synthetic private-data fixture\n')
        self.assertEqual(status, self.git('status', '--porcelain', '--untracked-files=all'))

    def test_committed_snapshot_rejects_portable_userdata_case_insensitively(self):
        self.write('uSeRdAtA/ai-settings.json', 'synthetic private-data fixture\n')
        self.commit()
        before = self.git('status', '--porcelain', '--untracked-files=all')
        with self.assertRaisesRegex(ValueError, 'tracked generated or local data'):
            self.package()
        self.assertEqual(before, self.git('status', '--porcelain', '--untracked-files=all'))
        self.assertFalse((self.output / 'SignalDebt_Quality_Pass.zip').exists())
        self.assertFalse((self.output / 'SignalDebt_Quality_Pass_Parts').exists())
        self.assertFalse((self.output / 'package-manifest.json').exists())

    def test_unrelated_dirty_repository_does_not_block_committed_project(self):
        (self.repository / 'other-game.txt').write_text('unrelated work\n')
        self.assertFalse(self.package()['dirty'])

    def test_refuses_output_inside_project_including_through_symlink(self):
        for output in (self.project, self.project / 'exports'):
            with self.subTest(output=output), self.assertRaisesRegex(ValueError, 'outside the source project'):
                PACKAGE.package_source(self.project, output)
        link = self.root / 'linked-project'
        link.symlink_to(self.project, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, 'outside the source project'):
            PACKAGE.package_source(self.project, link / 'exports')

    def test_refuses_overwrite_of_archives_parts_or_manifest(self):
        self.output.mkdir()
        for name in ('SignalDebt_Quality_Pass.zip', 'SignalDebt_Quality_Pass_Parts', 'package-manifest.json'):
            with self.subTest(name=name):
                existing = self.output / name
                existing.write_text('preserve existing output\n')
                with self.assertRaisesRegex(ValueError, 'Output already exists'):
                    self.package()
                self.assertEqual(existing.read_text(), 'preserve existing output\n')
                existing.unlink()

    def test_rejects_unsafe_archive_names(self):
        for name in ('../escape', '/tmp/escape', '', '.', 'two names'):
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, 'Archive name'):
                self.package(name=name)

    def test_tracked_live_environment_files_rejected_in_both_modes(self):
        self.write('AI/.env', 'never distribute\n')
        self.git('add', '--force', 'SignalDebt/AI/.env')
        self.commit()
        for working_tree in (False, True):
            with self.subTest(working_tree=working_tree):
                with self.assertRaisesRegex(ValueError, 'tracked live environment file'):
                    self.package(working_tree=working_tree)
                self.assertFalse((self.output / 'SignalDebt_Quality_Pass.zip').exists())

    def test_tracked_symlink_rejected_in_both_modes(self):
        (self.project / 'linked.txt').symlink_to('README.md')
        self.commit()
        for working_tree in (False, True):
            with self.subTest(working_tree=working_tree), self.assertRaisesRegex(ValueError, 'symlink'):
                self.package(working_tree=working_tree)

    def test_working_tree_rejects_a_replaced_parent_directory_symlink(self):
        path = self.write('folder/data.txt', 'original source\n')
        self.commit()
        path.unlink()
        path.parent.rmdir()
        outside = self.root / 'outside'
        outside.mkdir()
        (outside / 'data.txt').write_text('outside data\n')
        path.parent.symlink_to(outside, target_is_directory=True)
        with self.assertRaisesRegex(ValueError, 'symlink'):
            self.package(working_tree=True)

    def test_transfer_parts_split_and_reassemble_without_duplicates(self):
        self.write('one.txt', 'one\n')
        self.write('two.txt', 'two\n')
        self.commit()
        budget = PACKAGE.PART_BUDGET
        try:
            PACKAGE.PART_BUDGET = 1400
            report = self.package()
        finally:
            PACKAGE.PART_BUDGET = budget
        self.assertGreater(len(report['archives']), 2)
        self.verify_archives(report)


if __name__ == '__main__':
    unittest.main()
