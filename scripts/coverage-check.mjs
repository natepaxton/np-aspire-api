// Runs a .NET test project with Coverlet (Microsoft Testing Platform), merges the results with
// ReportGenerator, and fails if coverage is below the given minimums.
//
// Run from the repository root after `dotnet build`:
//   node scripts/coverage-check.mjs --project tests/NpAspire.Api.Tests --out coverage --lines 80 --branches 75 --methods 80
//
// Output (under <out>): cobertura.xml (for Codecov), Summary.json, SummaryGithub.md (CI job summary).
// Neither coverlet.MTP nor the ReportGenerator CLI can enforce minimums, which is why this script exists.
import { spawnSync } from 'node:child_process';
import { readFileSync, renameSync, rmSync } from 'node:fs';
import { resolve } from 'node:path';
import { parseArgs } from 'node:util';

const { values } = parseArgs({
  options: {
    project: { type: 'string' },
    out: { type: 'string' },
    lines: { type: 'string' },
    branches: { type: 'string' },
    methods: { type: 'string' },
  },
});

if (!values.project || !values.out || !values.lines || !values.branches || !values.methods) {
  console.error(
    'Usage: coverage-check.mjs --project <test project dir> --out <dir> --lines <pct> --branches <pct> --methods <pct>',
  );
  process.exit(2);
}

const outDir = resolve(values.out);
const rawDir = resolve(outDir, 'raw');

function run(command, args) {
  const result = spawnSync(command, args, { stdio: 'inherit' });
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}

rmSync(outDir, { recursive: true, force: true });

run('dotnet', ['test', '--project', values.project, '--no-build', '--coverlet', '--results-directory', rawDir]);

run('dotnet', [
  'tool',
  'run',
  'reportgenerator',
  `-reports:${rawDir}/*.cobertura*.xml`,
  `-targetdir:${outDir}`,
  '-reporttypes:Cobertura;JsonSummary;MarkdownSummaryGithub',
  '-verbosity:Warning',
]);

// Keep only the merged report so Codecov doesn't upload the raw files as well.
rmSync(rawDir, { recursive: true, force: true });
// Codecov's file search is case-sensitive and only matches lowercase `cobertura.xml`.
renameSync(resolve(outDir, 'Cobertura.xml'), resolve(outDir, 'cobertura.xml'));

const { summary } = JSON.parse(readFileSync(resolve(outDir, 'Summary.json'), 'utf8'));
const checks = [
  ['lines', summary.linecoverage, Number(values.lines)],
  ['branches', summary.branchcoverage, Number(values.branches)],
  ['methods', summary.methodcoverage, Number(values.methods)],
];

let failed = false;
for (const [name, actual, minimum] of checks) {
  // ReportGenerator reports null when there is nothing to cover (e.g. no branches).
  const pct = actual ?? 100;
  const ok = pct >= minimum;
  failed ||= !ok;
  console.log(`${ok ? '✔' : '✖'} ${name.padEnd(8)} ${String(pct).padStart(6)}%  (minimum ${minimum}%)`);
}

if (failed) {
  console.error(`Coverage is below the minimum for ${values.project}.`);
  process.exit(1);
}
