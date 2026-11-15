"""Plot recorded Core CSVs; this produces data charts, not Unity screenshots."""
import csv
from pathlib import Path
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.ticker import PercentFormatter

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / 'docs' / 'diagnostics'
with (DATA / 'issue-34-environments.csv').open() as file:
    experiments = list(csv.DictReader(file))
with (DATA / 'issue-34-game-world.csv').open() as file:
    game = list(csv.DictReader(file))

plt.rcParams.update({'font.family': 'DejaVu Sans', 'font.size': 9, 'svg.hashsalt': 'evolution-issue-34'})
fig, axes = plt.subplots(3, 2, figsize=(12, 10), constrained_layout=True)
colours = ['#2b9955', '#9051af', '#da7b20', '#168ca1']
traits = [('photo_share', 'Фотосинтез > 0,6'), ('organic_share', 'Органическая специализация > 0,6'),
          ('hot_share', 'Температурный оптимум > 32 °C'), ('frugal_share', 'Эффективность > 0,7')]
profiles = [('Sunlit', 'Светлая вода'), ('DarkOrganic', 'Тёмная органическая вода'),
            ('Hydrothermal', 'Горячий источник'), ('Natural', 'Непрерывная естественная среда')]
# Derive the exact recorded scenario identifier, do not silently drop an absent profile.
recorded = {r['scenario'] for r in experiments}
if 'DarkOrganic' not in recorded:
    profiles[1] = ('Dark', profiles[1][1])
for axis, (scenario, title) in zip(axes.flat, profiles):
    rows = [r for r in experiments if r['scenario'] == scenario and r['seed'] == '42']
    if len(rows) != 61:
        raise ValueError(f'Missing 0..600 s series: {scenario}')
    seconds = [float(r['seconds']) for r in rows]
    for (key, name), colour in zip(traits, colours):
        axis.plot(seconds, [float(r[key]) for r in rows], label=name, color=colour, linewidth=1.9)
    axis.set(title=f'{title} · seed 42', xlim=(0, 600), ylim=(-.02, 1.02), ylabel='Доля носителей признака')
    axis.yaxis.set_major_formatter(PercentFormatter(1))
    axis.grid(alpha=.18)
    axis.set_xlabel('Модельное время, с')
axis = axes[2, 0]
for (scenario, title), colour in zip(profiles, colours):
    for seed, style in [('42', '-'), ('7', '--')]:
        rows = [r for r in experiments if r['scenario'] == scenario and r['seed'] == seed]
        axis.plot([float(r['seconds']) for r in rows], [int(r['alive']) for r in rows],
                  color=colour, linestyle=style, label=f'{title} · {seed}', linewidth=1.5)
axis.set(title='Контрольные когорты · 48 исходных клеток', ylabel='Живые клетки', xlim=(0, 600), xlabel='Модельное время, с')
axis.grid(alpha=.18)
axis.legend(fontsize=7, loc='upper center', bbox_to_anchor=(.5, -.22), ncols=2)
axis = axes[2, 1]
for seed, colour in zip(('42', '7', '123', '2026'), colours):
    rows = [r for r in game if r['seed'] == seed]
    axis.plot([float(r['seconds']) for r in rows], [int(r['total_alive']) for r in rows], color=colour, label=f'seed {seed}', linewidth=1.8)
axis.set(title='Обычный ChunkOcean Natural · 250 исходных клеток', ylabel='Живые клетки, включая архивные',
         xlim=(0, 600), xlabel='Модельное время, с')
axis.grid(alpha=.18)
axis.legend(fontsize=8)
axes[0, 0].legend(fontsize=7, loc='center right')
fig.suptitle('EVOLUTION #34 — записанные результаты Core, 600 секунд\nДанные .NET-модели; это не кадры Unity', fontsize=15)
fig.savefig(DATA / 'issue-34-selection.svg', metadata={'Date': None, 'Creator': 'Tools/NicheDiagnostics/plot_results.py'})
# Optional local review raster, excluded from the repository.
import sys
if len(sys.argv) > 1:
    fig.savefig(sys.argv[1], dpi=140)
