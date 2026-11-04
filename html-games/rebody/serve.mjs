import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';

// Loopback-only preview. The delivered index.html also works directly via file://.
const port = Number(process.env.PORT || 4173);
const file = new URL('./index.html', import.meta.url);
const server = createServer(async (req, res) => {
  if (req.url === '/favicon.ico') { res.writeHead(204); res.end(); return; }
  if (req.url !== '/' && req.url !== '/index.html') { res.writeHead(404); res.end('Not found'); return; }
  try {
    const html = await readFile(file);
    res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' });
    res.end(html);
  } catch { res.writeHead(500); res.end('Unable to load game'); }
});
server.listen(port, '127.0.0.1', () => console.log(`RE:BODY preview: http://127.0.0.1:${port}`));
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => server.close(() => process.exit(0)));
