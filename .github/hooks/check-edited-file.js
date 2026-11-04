#!/usr/bin/env node
// postToolUse: build the project containing the edited .cs file; report compiler
// diagnostics for that file only. There is no linter/analyzer configured in this
// repo (see context/foundation/test-plan.md §4), so the C# compiler's own
// diagnostics are the per-edit typecheck substitute.
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

// Recursively collect file_path / filePath / path from any nesting depth
// (multi-file edits nest paths, e.g. replacements[].filePath).
function collectPaths(obj, out) {
  if (obj == null) return;
  if (Array.isArray(obj)) {
    for (const v of obj) collectPaths(v, out);
    return;
  }
  if (typeof obj !== 'object') return;
  for (const key of ['file_path', 'filePath', 'path']) {
    if (typeof obj[key] === 'string') out.add(obj[key]);
  }
  for (const v of Object.values(obj)) collectPaths(v, out);
}

function findRepoRoot() {
  const result = spawnSync('git', ['rev-parse', '--show-toplevel'], { encoding: 'utf8' });
  if (result.status !== 0) return null;
  return result.stdout.trim().replace(/\//g, path.sep);
}

// Map a repo-relative path to the .csproj that owns it. Returns null for
// anything outside the two known projects (views, config, docs, ...).
function projectFor(relPath, root) {
  if (relPath.startsWith('src/melody/')) return path.join(root, 'src', 'melody', 'melody.csproj');
  if (relPath.startsWith('tests/melody.Tests/')) {
    return path.join(root, 'tests', 'melody.Tests', 'melody.Tests.csproj');
  }
  return null;
}

function main() {
  const input = safeParse(readStdin()) || {};

  const cwd = input.cwd || '';
  if (cwd && fs.existsSync(cwd)) {
    try {
      process.chdir(cwd);
    } catch {
      /* ignore: fall back to the process's own cwd */
    }
  }

  // VS Code ignores matchers (every tool fires this hook); skip read-only tools
  // by name so a search/view call never triggers a build.
  const toolName = String(input.tool_name || input.toolName || '').toLowerCase();
  if (/read|view|search|grep|list|glob|fetch|get_/.test(toolName)) {
    process.exit(0);
  }

  let toolInput = input.tool_input;
  if (!toolInput) {
    const toolArgs = input.toolArgs;
    toolInput = typeof toolArgs === 'string' ? safeParse(toolArgs) : toolArgs;
  }

  const paths = new Set();
  collectPaths(toolInput, paths);
  collectPaths(input, paths); // tolerate a top-level path field too

  const csFiles = [...paths].filter((p) => p.toLowerCase().endsWith('.cs'));
  if (csFiles.length === 0) process.exit(0); // nothing this hook checks

  const root = findRepoRoot();
  if (!root) process.exit(0); // not inside a checkout this hook can resolve

  let anyChecked = false;
  const failures = [];

  for (const rawPath of csFiles) {
    const abs = path.isAbsolute(rawPath) ? rawPath : path.resolve(process.cwd(), rawPath);
    if (!fs.existsSync(abs)) continue; // nonexistent path in the payload: nothing to check

    const rel = path.relative(root, abs).replace(/\\/g, '/');
    if (rel.startsWith('..')) continue; // belongs to another checkout/repo: skip

    const project = projectFor(rel, root);
    if (!project) continue; // not part of a project this hook builds

    anyChecked = true;
    const result = spawnSync(
      'dotnet',
      ['build', project, '--configuration', 'Release', '--nologo'],
      { cwd: root, encoding: 'utf8', env: { ...process.env, NO_COLOR: '1', DOTNET_NOLOGO: '1' } },
    );

    if (result.error) {
      // No silent success: a missing `dotnet` must be reported, not swallowed.
      failures.push(`dotnet build could not run (${result.error.message}) - hook cannot verify ${rel}`);
      continue;
    }

    if (result.status !== 0) {
      const output = `${result.stdout || ''}${result.stderr || ''}`;
      const base = path.basename(abs);
      const relevant = output.split(/\r?\n/).filter((line) => line.includes(base) || line.includes(rel));
      if (relevant.length > 0) {
        failures.push(`Build errors in ${rel}:\n${relevant.join('\n')}`);
      }
    }
  }

  if (!anyChecked) process.exit(0);

  if (failures.length > 0) {
    const msg = failures.join('\n\n');
    process.stdout.write(
      JSON.stringify({
        additionalContext: msg,
        hookSpecificOutput: { hookEventName: 'PostToolUse', additionalContext: msg },
      }),
    );
  }
  process.exit(0);
}

main();
