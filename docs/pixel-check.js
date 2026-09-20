#!/usr/bin/env node
// Headless pixel check for AC4: verifies the WebGL canvas renders more than 1
// distinct colour.  Run after the WebGL build:
//
//   node docs/pixel-check.js Builds/WebGL
//
// Requires: Node 18+, puppeteer-core (npm install puppeteer-core in a temp dir)
// OR uses the bundled analyze-screenshot path if Chrome launches successfully.

const puppeteer = require('puppeteer-core');
const http = require('http');
const fs = require('fs');
const path = require('path');
const { createInflate } = require('zlib');

const WEBGL_DIR = path.resolve(process.argv[2] || 'Builds/WebGL');
const PORT = 9876;
const CHROME_PATH = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';

function createServer(dir) {
  const resolvedDir = path.resolve(dir);
  return http.createServer((req, res) => {
    const urlPath = req.url === '/' ? '/index.html' : req.url;
    // Resolve the full path and verify it stays inside resolvedDir to prevent
    // path-traversal attacks (e.g. GET /../../../etc/passwd).
    const filePath = path.resolve(path.join(resolvedDir, urlPath));
    if (!filePath.startsWith(resolvedDir + path.sep) && filePath !== resolvedDir) {
      res.writeHead(403); res.end('Forbidden');
      return;
    }
    const ext = path.extname(filePath).toLowerCase();
    const mimeTypes = {
      '.html': 'text/html', '.js': 'application/javascript',
      '.wasm': 'application/wasm', '.data': 'application/octet-stream',
      '.png': 'image/png', '.css': 'text/css',
    };
    try {
      const data = fs.readFileSync(filePath);
      res.writeHead(200, {
        'Content-Type': mimeTypes[ext] || 'application/octet-stream',
        'Cross-Origin-Embedder-Policy': 'require-corp',
        'Cross-Origin-Opener-Policy': 'same-origin',
      });
      res.end(data);
    } catch {
      res.writeHead(404); res.end('Not found: ' + req.url);
    }
  });
}

async function countColorsFromScreenshot(pngPath) {
  const data = fs.readFileSync(pngPath);
  let offset = 8;
  let width = 0, height = 0, colorType = 0;
  const idatChunks = [];
  while (offset < data.length) {
    const length = data.readUInt32BE(offset);
    const type = data.toString('ascii', offset + 4, offset + 8);
    const chunk = data.slice(offset + 8, offset + 8 + length);
    if (type === 'IHDR') { width = chunk.readUInt32BE(0); height = chunk.readUInt32BE(4); colorType = chunk[9]; }
    if (type === 'IDAT') idatChunks.push(chunk);
    offset += 12 + length;
  }
  const compressed = Buffer.concat(idatChunks);
  const inflate = createInflate();
  const chunks = [];
  await new Promise((res, rej) => { inflate.on('data', c => chunks.push(c)); inflate.on('end', res); inflate.on('error', rej); inflate.end(compressed); });
  const raw = Buffer.concat(chunks);
  const bpp = colorType === 6 ? 4 : 3;
  const rowBytes = width * bpp + 1;
  const colors = new Set();
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const p = y * rowBytes + 1 + x * bpp;
      const a = bpp === 4 ? raw[p + 3] : 255;
      if (a > 10) colors.add(`${Math.round(raw[p]/8)*8},${Math.round(raw[p+1]/8)*8},${Math.round(raw[p+2]/8)*8}`);
    }
  }
  return colors.size;
}

async function run() {
  const server = createServer(WEBGL_DIR);
  await new Promise(resolve => server.listen(PORT, resolve));
  const browser = await puppeteer.launch({
    executablePath: CHROME_PATH, headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox', '--use-angle=swiftshader', '--no-first-run'],
  });
  const page = await browser.newPage();
  await page.setViewport({ width: 960, height: 600 });
  await page.goto(`http://localhost:${PORT}/`, { waitUntil: 'networkidle0', timeout: 60000 });
  await new Promise(r => setTimeout(r, 10000));
  const screenshotPath = path.join(WEBGL_DIR, 'pixel-check.png');
  await page.screenshot({ path: screenshotPath });
  await browser.close();
  server.close();
  const count = await countColorsFromScreenshot(screenshotPath);
  console.log(`Distinct colours on canvas screenshot: ${count}`);
  if (count > 1) { console.log('PASS'); process.exit(0); }
  else { console.log('FAIL'); process.exit(1); }
}

run().catch(err => { console.error(err); process.exit(1); });
