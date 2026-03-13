#!/usr/bin/env node
/**
 * Node.js equivalent of generate_review.py (static HTML generator only).
 * Reads the iteration-1 workspace, builds EMBEDDED_DATA, injects into viewer.html.
 */

const fs = require('fs');
const path = require('path');

const WORKSPACE = path.join(__dirname, 'iteration-1');
const VIEWER_TEMPLATE = path.join(
  __dirname, '..', '..', 'skills', 'skill-creator', 'eval-viewer', 'viewer.html'
);
const BENCHMARK_FILE = path.join(WORKSPACE, 'benchmark.json');
const OUTPUT_HTML = path.join(__dirname, 'review.html');
const SKILL_NAME = 'avalonia-design';

const TEXT_EXTENSIONS = new Set([
  '.txt', '.md', '.json', '.csv', '.py', '.js', '.ts', '.tsx', '.jsx',
  '.yaml', '.yml', '.xml', '.html', '.css', '.sh', '.rb', '.go', '.rs',
  '.java', '.c', '.cpp', '.h', '.hpp', '.sql', '.r', '.toml', '.axaml', '.cs'
]);

const METADATA_FILES = new Set(['transcript.md', 'user_notes.md', 'metrics.json']);

function embedFile(filePath) {
  const name = path.basename(filePath);
  const ext = path.extname(filePath).toLowerCase();
  if (TEXT_EXTENSIONS.has(ext)) {
    try {
      const content = fs.readFileSync(filePath, 'utf8');
      return { name, type: 'text', content };
    } catch {
      return { name, type: 'error', content: '(Error reading file)' };
    }
  }
  // Binary fallback
  try {
    const raw = fs.readFileSync(filePath);
    const b64 = raw.toString('base64');
    return { name, type: 'binary', mime: 'application/octet-stream', data_uri: `data:application/octet-stream;base64,${b64}` };
  } catch {
    return { name, type: 'error', content: '(Error reading file)' };
  }
}

function readJsonSafe(filePath) {
  try {
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch {
    return null;
  }
}

function collectOutputFiles(outputsDir) {
  const files = [];
  if (!fs.existsSync(outputsDir)) return files;
  function walk(dir) {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        walk(full);
      } else if (!METADATA_FILES.has(entry.name)) {
        files.push(embedFile(full));
      }
    }
  }
  walk(outputsDir);
  return files;
}

function buildRun(workspaceRoot, runDir) {
  // Read eval_metadata from runDir or parent
  let prompt = '(No prompt found)';
  let evalId = null;
  for (const candidate of [
    path.join(runDir, 'eval_metadata.json'),
    path.join(path.dirname(runDir), 'eval_metadata.json')
  ]) {
    if (fs.existsSync(candidate)) {
      const meta = readJsonSafe(candidate);
      if (meta) {
        prompt = meta.prompt || prompt;
        evalId = meta.eval_id;
        break;
      }
    }
  }

  const runId = path.relative(workspaceRoot, runDir).replace(/[\\/]/g, '-');
  const outputsDir = path.join(runDir, 'outputs');
  const outputs = collectOutputFiles(outputsDir);

  let grading = null;
  for (const candidate of [
    path.join(runDir, 'grading.json'),
    path.join(path.dirname(runDir), 'grading.json')
  ]) {
    if (fs.existsSync(candidate)) {
      grading = readJsonSafe(candidate);
      if (grading) break;
    }
  }

  return { id: runId, prompt, eval_id: evalId, outputs, grading };
}

function findRuns(root, current, runs) {
  if (!fs.existsSync(current)) return;
  const stat = fs.statSync(current);
  if (!stat.isDirectory()) return;

  const outputsDir = path.join(current, 'outputs');
  if (fs.existsSync(outputsDir) && fs.statSync(outputsDir).isDirectory()) {
    const run = buildRun(root, current);
    if (run) runs.push(run);
    return;
  }

  const skip = new Set(['node_modules', '.git', '__pycache__', 'skill', 'inputs', 'evals']);
  for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
    if (entry.isDirectory() && !skip.has(entry.name)) {
      findRuns(root, path.join(current, entry.name), runs);
    }
  }
}

// Main
const runs = [];
findRuns(WORKSPACE, WORKSPACE, runs);
runs.sort((a, b) => {
  const idA = a.eval_id ?? Infinity;
  const idB = b.eval_id ?? Infinity;
  if (idA !== idB) return idA - idB;
  return a.id.localeCompare(b.id);
});

const benchmark = readJsonSafe(BENCHMARK_FILE);

const embedded = {
  skill_name: SKILL_NAME,
  runs,
  previous_feedback: {},
  previous_outputs: {},
};
if (benchmark) embedded.benchmark = benchmark;

const dataJson = JSON.stringify(embedded);
const template = fs.readFileSync(VIEWER_TEMPLATE, 'utf8');
const html = template.replace('/*__EMBEDDED_DATA__*/', `const EMBEDDED_DATA = ${dataJson};`);

fs.writeFileSync(OUTPUT_HTML, html, 'utf8');
console.log('Review HTML written to:', OUTPUT_HTML);
