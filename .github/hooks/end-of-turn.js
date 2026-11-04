#!/usr/bin/env node
// agentStop: sweep every .cs file this turn changed or created (including
// edits made through a shell command, which never reach the per-edit hook),
// then run a full solution build + the full unit/integration suite once.
// The repo has no lint/analyzer config and no "related tests" mode for xUnit,
// but the whole suite finishes in ~4s (well under the 30s "run it all" bar
// from the skill), so this sweeps everything rather than filtering by import.
'use strict';
const { spawnSync } = require('child_process');
const path = require('path');
const fs = require('fs');

function readStdin() {
  try {
    return fs.readFileSync(0, 'utf8');
  } catch {
    return '';
  }
}

function safeParse(str) {
  try {
    return JSON.parse(str);
  } catch {
    return null;
  }
}

function findRepoRoot() {
  const result = spawnSync('git', ['rev-parse', '--show-toplevel'], { encoding: 'utf8' });
  if (result.status !== 0) return null;
  return result.stdout.trim().replace(/\//g, path.sep);
}

function gitChangedFiles(root) {
  const diff = spawnSync('git', ['diff', '--name-only', 'HEAD'], { cwd: root, encoding: 'utf8' });
  const untracked = spawnSync('git', ['ls-files', '-o', '--exclude-standard'], { cwd: root, encoding: 'utf8' });
  const a = diff.status === 0 ? diff.stdout.split(/\r?\n/).filter(Boolean) : [];
  const b = untracked.status === 0 ? untracked.stdout.split(/\r?\n/).filter(Boolean) : [];
  return [...new Set([...a, ...b])];
}

function main() {
  const input = safeParse(readStdin()) || {};

  // The harness gives one retry; do not loop forever on a stubborn failure.
  if (input.stop_hook_active === true) process.exit(0);

  const cwd = input.cwd || '';
  if (cwd && fs.existsSync(cwd)) {
    try {
      process.chdir(cwd);
    } catch {
      /* ignore: fall back to the process's own cwd */
    }
  }

  const root = findRepoRoot();
  if (!root) process.exit(0); // not inside a checkout this hook can resolve

  const changed = gitChangedFiles(root);
  if (changed.length === 0) process.exit(0); // a Q&A turn: nothing changed

  const csChanged = changed.filter(
    (f) => f.toLowerCase().endsWith('.cs') && fs.existsSync(path.join(root, f)),
  );
  if (csChanged.length === 0) process.exit(0); // nothing this hook can check

  const failures = [];
  const env = { ...process.env, NO_COLOR: '1', DOTNET_NOLOGO: '1' };

  const buildResult = spawnSync(
    'dotnet',
    ['build', 'melodybooker.sln', '--configuration', 'Release', '--nologo'],
    { cwd: root, encoding: 'utf8', env },
  );
  if (buildResult.error) {
    failures.push(`dotnet build could not run (${buildResult.error.message}) - hook cannot verify the solution`);
  } else if (buildResult.status !== 0) {
    failures.push(`Solution build failed:\n${(buildResult.stdout || '') + (buildResult.stderr || '')}`);
  }

  // Only run tests if the build succeeded - a red build makes test output noise.
  if (failures.length === 0) {
    const testResult = spawnSync(
      'dotnet',
      ['test', 'melodybooker.sln', '--configuration', 'Release', '--no-build', '--nologo'],
      { cwd: root, encoding: 'utf8', env },
    );
    if (testResult.error) {
      failures.push(`dotnet test could not run (${testResult.error.message}) - hook cannot verify tests`);
    } else if (testResult.status !== 0) {
      failures.push(`Test suite failed:\n${(testResult.stdout || '') + (testResult.stderr || '')}`);
    }
  }

  if (failures.length > 0) {
    const msg = `Fix these before you finish:\n\n${failures.join('\n\n')}`;
    process.stdout.write(
      JSON.stringify({
        decision: 'block',
        reason: msg,
        hookSpecificOutput: { hookEventName: 'Stop', decision: 'block', reason: msg },
      }),
    );
  }
  process.exit(0);
}

main();
