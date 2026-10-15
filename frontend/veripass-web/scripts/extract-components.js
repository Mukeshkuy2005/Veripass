const fs = require('fs');
const path = require('path');

function getFiles(dir, match) {
  let results = [];
  const list = fs.readdirSync(dir);
  list.forEach(file => {
    file = path.join(dir, file);
    const stat = fs.statSync(file);
    if (stat && stat.isDirectory()) results = results.concat(getFiles(file, match));
    else if (match.test(file)) results.push(file);
  });
  return results;
}

const root = path.resolve(__dirname, '../src/app');
const compFiles = getFiles(root, /\.component\.ts$/).filter(f => !f.endsWith('app.component.ts'));

console.log(`Discovered ${compFiles.length} component.ts files to refactor.`);

const regex = /template\s*:\s*`([\s\S]*?)`\s*,\s*styles\s*:\s*\[\s*`([\s\S]*?)`\s*\]/;

const stats = {
  processed: 0,
  htmlCreated: 0,
  cssCreated: 0,
  tsModified: 0,
  failed: []
};

compFiles.forEach((tsPath) => {
  const content = fs.readFileSync(tsPath, 'utf8');
  const match = content.match(regex);

  if (!match) {
    stats.failed.push({ file: path.relative(root, tsPath), reason: 'Regex mismatch' });
    return;
  }

  const rawTemplate = match[1];
  const rawStyles = match[2];

  // Trim excess leading/trailing whitespace/newlines for clean files
  let templateContent = rawTemplate;
  // If template starts with a newline, strip only the first newline
  if (templateContent.startsWith('\r\n')) templateContent = templateContent.substring(2);
  else if (templateContent.startsWith('\n')) templateContent = templateContent.substring(1);
  // Ensure template ends with single newline
  templateContent = templateContent.trimEnd() + '\n';

  let stylesContent = rawStyles;
  if (stylesContent.startsWith('\r\n')) stylesContent = stylesContent.substring(2);
  else if (stylesContent.startsWith('\n')) stylesContent = stylesContent.substring(1);
  stylesContent = stylesContent.trimEnd() + '\n';

  const dir = path.dirname(tsPath);
  const baseName = path.basename(tsPath, '.ts'); // e.g. 'home.component'
  const htmlPath = path.join(dir, `${baseName}.html`);
  const cssPath = path.join(dir, `${baseName}.css`);

  // Write HTML file
  fs.writeFileSync(htmlPath, templateContent, 'utf8');
  stats.htmlCreated++;

  // Write CSS file
  fs.writeFileSync(cssPath, stylesContent, 'utf8');
  stats.cssCreated++;

  // Replace metadata in TS file
  const replacement = `templateUrl: './${baseName}.html',\n  styleUrl: './${baseName}.css'`;
  const updatedTs = content.replace(match[0], replacement);

  fs.writeFileSync(tsPath, updatedTs, 'utf8');
  stats.tsModified++;
  stats.processed++;
});

console.log('--- Refactoring Complete ---');
console.log(`Processed components: ${stats.processed}`);
console.log(`HTML files created: ${stats.htmlCreated}`);
console.log(`CSS files created: ${stats.cssCreated}`);
console.log(`TS files modified: ${stats.tsModified}`);
if (stats.failed.length > 0) {
  console.error('Failed components:', stats.failed);
  process.exit(1);
} else {
  console.log('All components refactored successfully!');
}
