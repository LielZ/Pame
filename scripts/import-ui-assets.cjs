const fs = require('fs');
const path = require('path');
// Install sharp in your development environment, or expose it through NODE_PATH.
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const assets = path.join(root, 'src/Pame.App/Assets');
const manifest = [];
async function fetchFile(url, out) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status}: ${url}`);
  const data = Buffer.from(await response.arrayBuffer());
  fs.mkdirSync(path.dirname(out), {recursive: true}); fs.writeFileSync(out, data);
  return data;
}
async function icon(name, kind) {
  const url = kind === 'Stores'
    ? `https://raw.githubusercontent.com/simple-icons/simple-icons/11.15.0/icons/${name}.svg`
    : `https://raw.githubusercontent.com/lucide-icons/lucide/0.468.0/icons/${name}.svg`;
  const svg = await fetchFile(url, path.join(root, 'research/ui-assets/svg', kind, name+'.svg'));
  let source = svg.toString().replace(/currentColor/g, '#E5F1FF');
  if(kind==='Stores') source=source.replace('<svg ', '<svg fill="#FFFFFF" ');
  const out=path.join(assets, kind, name+'.png'); fs.mkdirSync(path.dirname(out),{recursive:true});
  await sharp(Buffer.from(source), {density:384}).resize(192,192,{fit:'contain',background:'#00000000'}).png().toFile(out);
  manifest.push({file:kind+'/'+name+'.png',source:url});
}
(async()=>{
  const jobs=[];
  for(const name of ['steam','epicgames','gogdotcom','ea','ubisoft','battledotnet','xbox','riotgames','rockstargames']) jobs.push([name,'Stores']);
  for(const name of ['house','gamepad-2','shopping-bag','download','settings','cpu','circuit-board','memory-stick','hard-drive','activity','clock','battery-full','battery-charging','battery-low','bluetooth','wifi','volume-2','volume-x','monitor','play','search','heart','users','plus','layout-grid','arrow-right','chevron-right','ellipsis','refresh-cw','power','camera','headphones','sliders-horizontal','check','x','folder','log-out','moon','pause','shield-check','zap','trash-2','thermometer','sparkles']) jobs.push([name,'Icons']);
  const results=await Promise.allSettled(jobs.map(([name,kind])=>icon(name,kind)));
  for(let i=0;i<results.length;i++)if(results[i].status==='rejected')console.error(jobs[i],results[i].reason.message);
  await Promise.all([
    fetchFile('https://raw.githubusercontent.com/simple-icons/simple-icons/11.15.0/LICENSE.md',path.join(root,'licenses/SimpleIcons.txt')),
    fetchFile('https://raw.githubusercontent.com/lucide-icons/lucide/0.468.0/LICENSE',path.join(root,'licenses/Lucide.txt')),
    fetchFile('https://raw.githubusercontent.com/rsms/inter/v4.1/LICENSE.txt',path.join(root,'licenses/Inter.txt'))
  ]);
  fs.writeFileSync(path.join(root,'docs/UI_ASSET_SOURCES.json'),JSON.stringify(manifest.sort((a,b)=>a.file.localeCompare(b.file)),null,2));
  console.log('Imported',manifest.length,'icons');
  if(results.some(x=>x.status==='rejected'))process.exitCode=1;
})();
