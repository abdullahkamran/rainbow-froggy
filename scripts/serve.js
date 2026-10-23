#!/usr/bin/env node
// Static file server for the WebGL build, bound to all interfaces so devices on
// the LAN can open it.   node scripts/serve.js [dir] [port]
const http = require('http');
const fs = require('fs');
const os = require('os');
const path = require('path');

const root = path.resolve(process.argv[2] || 'Builds/WebGL');
const port = Number(process.argv[3] || 8080);
const types = {
  '.html': 'text/html', '.js': 'application/javascript', '.wasm': 'application/wasm',
  '.data': 'application/octet-stream', '.json': 'application/json', '.css': 'text/css',
  '.png': 'image/png', '.jpg': 'image/jpeg', '.svg': 'image/svg+xml', '.ico': 'image/x-icon',
  '.mp3': 'audio/mpeg', '.ogg': 'audio/ogg', '.wav': 'audio/wav', '.br': 'application/octet-stream',
};

http.createServer((req, res) => {
  const rel = decodeURIComponent(req.url.split('?')[0]);
  const file = path.resolve(path.join(root, rel === '/' ? 'index.html' : rel));
  if (file !== root && !file.startsWith(root + path.sep)) { res.writeHead(403); return res.end(); }
  fs.readFile(file, (err, data) => {
    if (err) { res.writeHead(404); return res.end('Not found'); }
    // no-cache so a rebuild shows up on reload
    res.writeHead(200, { 'Content-Type': types[path.extname(file).toLowerCase()] || 'application/octet-stream', 'Cache-Control': 'no-store' });
    res.end(data);
  });
}).listen(port, '0.0.0.0', () => {
  console.log(`Serving ${root}\n  http://localhost:${port}`);
  for (const list of Object.values(os.networkInterfaces()))
    for (const i of list || [])
      if (i.family === 'IPv4' && !i.internal && !i.address.startsWith('169.254')) console.log(`  http://${i.address}:${port}`);
});
