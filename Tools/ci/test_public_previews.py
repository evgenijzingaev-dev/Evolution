"""Policy/integration tests. Synthetic pixels exist ONLY in temporary test directories."""
import copy
import io
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from PIL import Image, PngImagePlugin
import public_visual_previews as pub
from public_preview_source import resolve


def fixture(pr=19, run=100, timestamp='2026-11-13T00:00:00+00:00'):
    image = Image.new('RGB', (1440, 810), (22, 80, 100))
    stream = io.BytesIO()
    image.save(stream, 'JPEG')
    jpg = stream.getvalue()
    meta = dict(schema=1, repository=pub.REPOSITORY, pr=pr, source_run=run, source_attempt=1,
                source_sha='a'*40, unity='6000.6.9f1', pipeline='UniversalRenderPipelineAsset',
                source_artifact=f'https://github.com/{pub.REPOSITORY}/actions/runs/{run}/artifacts/1',
                published_at=timestamp, images=[dict(file=n+'.jpg', width=1440, height=810,
                sha256=pub.digest(jpg), original_sha256='b'*64) for n in pub.FRAMES])
    files = {n+'.jpg': jpg for n in pub.FRAMES}
    files.update({'publication.json': pub.json_bytes(meta), 'index.md': pub.index(meta)})
    return meta, files


class ImageTests(unittest.TestCase):
    def test_derivative_strips_metadata_and_does_not_change_original(self):
        out = io.BytesIO()
        info = PngImagePlugin.PngInfo()
        info.add_text('private_source_path', '/private/license')
        Image.new('RGBA', (1920, 1080), (30, 80, 110, 255)).save(out, 'PNG', pnginfo=info)
        original = out.getvalue()
        jpg, size = pub.encode_frame(original)
        self.assertEqual(size, (1440, 810))
        self.assertLess(len(jpg), pub.MAX_IMAGE)
        self.assertNotIn(b'/private/license', jpg)
        self.assertEqual(out.getvalue(), original)
        with Image.open(io.BytesIO(jpg)) as image:
            self.assertEqual(image.format, 'JPEG')
            self.assertFalse(image.getexif())
            self.assertNotIn('icc_profile', image.info)

    def test_wrong_size_and_wrong_format_rejected(self):
        for size, fmt in [((100, 100), 'PNG'), ((1920, 1080), 'JPEG')]:
            out = io.BytesIO()
            Image.new('RGB', size).save(out, fmt)
            with self.assertRaises(ValueError):
                pub.encode_frame(out.getvalue())

    def test_unexpected_file_and_symlink_rejected_before_processing(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for name in pub.FRAMES:
                (root/(name+'.png')).write_bytes(b'not a capture')
            (root/'manifest.json').write_text('{}')
            (root/'UnityLicense.ulf').write_text('not published')
            with self.assertRaisesRegex(ValueError, 'exactly 12'):
                pub.prepare(root, {})
            (root/'UnityLicense.ulf').unlink()
            (root/'01-ocean.png').unlink()
            (root/'01-ocean.png').symlink_to(root/'02-cell-close.png')
            with self.assertRaisesRegex(ValueError, 'exactly 12'):
                pub.prepare(root, {})

    def test_metadata_allowlist(self):
        meta, _ = fixture()
        pub.check_meta(meta)
        for key, value in [('secret', 'never'), ('source_sha', 'bad'), ('source_artifact', 'https://evil.example/1')]:
            bad = dict(meta, **{key: value})
            with self.assertRaises(ValueError):
                pub.check_meta(bad)

    def test_all_12_anonymous_urls_verified_by_content(self):
        meta, files = fixture()
        seen = []
        def fetch(url):
            seen.append(url)
            return files[url.rsplit('/', 1)[1]]
        pub.verify_public(meta, deadline=0, fetch=fetch)
        self.assertEqual(set(seen), set(pub.urls(meta)))
        self.assertEqual(len(seen), 12)

    def test_destination_must_be_the_public_image_repository(self):
        pub.check_destination(lambda: {'full_name':pub.PUBLIC_REPOSITORY, 'private':False})
        for repo in [{'full_name':pub.PUBLIC_REPOSITORY, 'private':True},
                     {'full_name':pub.REPOSITORY, 'private':False}]:
            with self.assertRaises(ValueError):
                pub.check_destination(lambda: repo)
        meta, _ = fixture()
        self.assertTrue(all('/Evolution-visual-previews/visual-previews/' in url for url in pub.urls(meta)))

    def test_wrong_content_or_unavailable_url_fails(self):
        meta, _ = fixture()
        for fetch in [lambda url: b'placeholder', lambda url: (_ for _ in ()).throw(OSError('404'))]:
            with self.assertRaisesRegex(RuntimeError, 'Public'):
                pub.verify_public(meta, deadline=0, fetch=fetch)


class RetentionTests(unittest.TestCase):
    def test_other_pr_preserved_retention_bounded(self):
        existing = {}
        for pr in range(1, 11):
            meta, files = fixture(pr, pr, f'2026-11-{pr:02d}T00:00:00+00:00')
            before = {k: v for k, v in existing.items() if k.startswith(f'pr-{pr-1}/')}
            existing, _, _ = pub.combine(existing, meta, files)
            self.assertTrue(all(existing[k] == v for k, v in before.items()))
        self.assertEqual(len(pub.groups_from(existing)), 8)
        self.assertNotIn('pr-1/index.md', existing)
        self.assertLess(sum(map(len, existing.values())), pub.MAX_TOTAL)

    def test_stale_run_and_repeat_do_not_rewrite(self):
        meta, files = fixture(run=200)
        existing, _, _ = pub.combine({}, meta, files)
        _, _, status = pub.combine(existing, meta, files)
        self.assertEqual(status, 'unchanged')
        old, oldfiles = fixture(run=199)
        selected, _, status = pub.combine(existing, old, oldfiles)
        self.assertEqual(status, 'superseded')
        self.assertEqual(selected, existing)

    def test_real_git_snapshot_has_no_binary_history(self):
        with tempfile.TemporaryDirectory() as temp:
            remote = str(Path(temp)/'preview.git')
            subprocess.run(['git', 'init', '--bare', '--quiet', remote], check=True)
            for pr in [19, 20, 19]:
                meta, files = fixture(pr, 100+pr)
                pub.publish_snapshot(meta, files, remote=remote)
            result = subprocess.check_output(['git', '--git-dir', remote, 'rev-list', '--count', pub.REF]).strip()
            self.assertEqual(result, b'1')
            with tempfile.TemporaryDirectory() as reader:
                pub.git(reader, 'init', '--bare', '--quiet')
                _, snapshot = pub.read_snapshot(reader, remote)
                self.assertEqual(set(pub.groups_from(snapshot)), {'pr-19', 'pr-20'})

    def test_concurrent_fetch_is_retried(self):
        meta, files = fixture()
        snapshot, _, _ = pub.combine({}, meta, files)
        with patch.object(pub, 'read_snapshot', side_effect=[pub.SnapshotChanged(), ('a'*40, snapshot)]) as read:
            selected, status = pub.publish_snapshot(meta, files, remote='not-used')
        self.assertEqual(read.call_count, 2)
        self.assertEqual(status, 'unchanged')
        self.assertEqual(selected['source_run'], meta['source_run'])

    def test_corrupt_existing_image_not_republished(self):
        meta, files = fixture()
        existing, _, _ = pub.combine({}, meta, files)
        existing['pr-19/01-ocean.jpg'] = b'bad'
        with self.assertRaises(ValueError):
            pub.combine(existing, meta, files)


class SourceTests(unittest.TestCase):
    def setUp(self):
        self.data = {
            '': {'full_name': pub.REPOSITORY, 'private': False},
            '/actions/runs/100': dict(path='.github/workflows/visual-preview.yml',
                head_repository={'full_name': pub.REPOSITORY}, head_branch='codex/example',
                head_sha='a'*40, event='pull_request', run_attempt=1, pull_requests=[{'number':19}]),
            '/actions/runs/100/jobs?filter=latest&per_page=100': {'jobs': [dict(name='render', conclusion='success',
                steps=[{'name':'Upload validated visual evidence', 'conclusion':'success'}])]},
            '/pulls/19': {'head': {'repo': {'full_name':pub.REPOSITORY}, 'ref':'codex/example', 'sha':'a'*40}},
            '/actions/runs/100/artifacts?per_page=100': {'artifacts':[
                {'id':1, 'name':'water-visual-preview-100-1', 'expired':False}]}}

    def test_verified_source(self):
        plan = resolve(self.data.__getitem__, 100)
        self.assertEqual(plan['pr'], 19)
        self.assertEqual(plan['artifact_id'], 1)
        self.assertEqual(plan['source_sha'], 'a'*40)

    def test_fork_or_untrusted_branch_rejected(self):
        for field, value in [('head_repository', {'full_name':'attacker/fork'}), ('head_branch', 'untrusted'), ('path','.github/workflows/other.yml')]:
            data = copy.deepcopy(self.data)
            data['/actions/runs/100'][field] = value
            with self.assertRaises(ValueError):
                resolve(data.__getitem__, 100, 19)

    def test_failed_render_or_upload_rejected(self):
        for upload in [False, True]:
            data = copy.deepcopy(self.data)
            job = data['/actions/runs/100/jobs?filter=latest&per_page=100']['jobs'][0]
            if upload:
                job['steps'][0]['conclusion'] = 'failure'
            else:
                job['conclusion'] = 'failure'
            with self.assertRaises(ValueError):
                resolve(data.__getitem__, 100, 19)

    def test_wrong_repo_fork_pr_expired_artifact_rejected(self):
        for case in range(3):
            data = copy.deepcopy(self.data)
            if case == 0:
                data['']['full_name'] = 'wrong/repository'
            elif case == 1:
                data['/pulls/19']['head']['repo']['full_name'] = 'fork/repo'
            else:
                data['/actions/runs/100/artifacts?per_page=100']['artifacts'][0]['expired'] = True
            with self.assertRaises(ValueError):
                resolve(data.__getitem__, 100, 19)

    def test_unrelated_manual_run_rejected(self):
        self.data['/actions/runs/100']['pull_requests'] = []
        self.data['/pulls/19']['head']['sha'] = 'c'*40
        self.data['/pulls/19/commits?per_page=100&page=1'] = [{'sha':'d'*40}]
        with self.assertRaises(ValueError):
            resolve(self.data.__getitem__, 100, 19)


if __name__ == '__main__':
    unittest.main()
