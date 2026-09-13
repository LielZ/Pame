// Package the supplied, unchanged PNGs and encode Windows icon sizes.
// Usage: node scripts/prepare-branding.cjs <wordmark.png> <app-icon.png>
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const sharp = require('sharp');
async function main() {
  const [logo, icon] = process.argv.slice(2);
  if (!logo || !icon) throw new Error('Provide the wordmark and app icon PNG paths.');
  const root = path.resolve(__dirname, '..');
  const assets = path.join(root, 'src/Pame.App/Assets');
  fs.mkdirSync(path.join(assets, 'Brand'), { recursive: true });
  fs.copyFileSync(logo, path.join(assets, 'Brand/wordmark.png'));
  fs.copyFileSync(icon, path.join(assets, 'Brand/app-icon.png'));
  const sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];
  const frames = await Promise.all(sizes.map(size => sharp(icon).resize(size, size).png().toBuffer()));
  const header = Buffer.alloc(6 + sizes.length * 16);
  header.writeUInt16LE(1, 2); header.writeUInt16LE(sizes.length, 4);
  let offset = header.length;
  frames.forEach((frame, i) => {
    const entry = 6 + i * 16;
    header[entry] = header[entry + 1] = sizes[i] % 256;
    header.writeUInt16LE(1, entry + 4); header.writeUInt16LE(32, entry + 6);
    header.writeUInt32LE(frame.length, entry + 8); header.writeUInt32LE(offset, entry + 12);
    offset += frame.length;
  });
  fs.writeFileSync(path.join(assets, 'pame.ico'), Buffer.concat([header, ...frames]));
  const sources = [logo, icon].map(file => ({ file: path.basename(file), sha256: crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'), source: 'Artwork supplied by the user for Pame' }));
  fs.writeFileSync(path.join(root, 'docs/BRAND_ASSET_SOURCES.json'), JSON.stringify({ sources, iconSizes: sizes }, null, 2) + '\n');
  console.log(JSON.stringify({ iconSizes: sizes, originalPngsPreserved: true }));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
