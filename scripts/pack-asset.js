const fs = require('fs');
const path = require('path');
const tar = require('tar');

// Target directories to include in the standalone VALYR asset package
const TARGET_DIRS = [
  'Assets/Scripts/VALYR',
  'Assets/Settings'
];

function getGuidFromMeta(metaPath) {
  if (!fs.existsSync(metaPath)) {
    return null;
  }
  const content = fs.readFileSync(metaPath, 'utf8');
  const match = content.match(/guid:\s*([a-f0-9]{32})/i);
  return match ? match[1] : null;
}

function collectAssets(dir, list = []) {
  if (!fs.existsSync(dir)) return list;

  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.name.endsWith('.meta')) {
      continue; // Processed alongside the target file/folder
    }

    list.push(fullPath);
    if (entry.isDirectory()) {
      collectAssets(fullPath, list);
    }
  }
  return list;
}

async function packAsset() {
  const pkg = JSON.parse(fs.readFileSync('package.json', 'utf8'));
  const version = process.argv[2] || process.env.SEMANTIC_RELEASE_VERSION || pkg.version || '0.0.0';
  const outDir = path.resolve('dist');

  if (!fs.existsSync(outDir)) {
    fs.mkdirSync(outDir, { recursive: true });
  } else {
    // Clear out previous build artifacts
    for (const file of fs.readdirSync(outDir)) {
      fs.rmSync(path.join(outDir, file), { recursive: true, force: true });
    }
  }

  const tempStaging = path.resolve('.unitypackage-temp');
  if (fs.existsSync(tempStaging)) {
    fs.rmSync(tempStaging, { recursive: true, force: true });
  }
  fs.mkdirSync(tempStaging, { recursive: true });

  console.log(`📦 Packing VALYR asset package (version: ${version})...`);

  // Include top-level folders in the package structure
  const allAssetPaths = [];
  
  // Ensure parent directories with .meta files are registered
  const parents = ['Assets/Scripts', 'Assets/Scripts/VALYR', 'Assets/Settings'];
  for (const p of parents) {
    if (fs.existsSync(p)) {
      allAssetPaths.push(p);
    }
  }

  for (const target of TARGET_DIRS) {
    collectAssets(target, allAssetPaths);
  }

  // Deduplicate and process
  const uniquePaths = Array.from(new Set(allAssetPaths));
  const guidDirs = [];

  for (const relPath of uniquePaths) {
    const normalizedPath = relPath.replace(/\\/g, '/');
    const metaPath = relPath + '.meta';
    const guid = getGuidFromMeta(metaPath);

    if (!guid) {
      console.warn(`⚠️ Warning: No .meta file or GUID found for: ${normalizedPath}`);
      continue;
    }

    const itemStagingDir = path.join(tempStaging, guid);
    if (!fs.existsSync(itemStagingDir)) {
      fs.mkdirSync(itemStagingDir, { recursive: true });
    }

    // 1. pathname file (Unix line endings)
    fs.writeFileSync(path.join(itemStagingDir, 'pathname'), normalizedPath + '\n', 'utf8');

    // 2. asset.meta file
    fs.copyFileSync(metaPath, path.join(itemStagingDir, 'asset.meta'));

    // 3. asset file (if it's a file, not a directory)
    const stat = fs.statSync(relPath);
    if (!stat.isDirectory()) {
      fs.copyFileSync(relPath, path.join(itemStagingDir, 'asset'));
    }

    guidDirs.push(guid);
  }

  console.log(`✅ Processed ${guidDirs.length} assets with valid GUIDs.`);

  // Create .unitypackage (gzipped tar archive)
  const unityPackageName = `VALYR-v${version}.unitypackage`;
  const unityPackagePath = path.join(outDir, unityPackageName);

  await tar.c(
    {
      gzip: true,
      file: unityPackagePath,
      cwd: tempStaging
    },
    guidDirs
  );

  console.log(`🎉 Successfully created unitypackage: ${unityPackagePath} (${(fs.statSync(unityPackagePath).size / 1024).toFixed(1)} KB)`);

  // Also create clean source distribution tar.gz / zip
  const archiveName = `VALYR-v${version}.tar.gz`;
  const archivePath = path.join(outDir, archiveName);

  await tar.c(
    {
      gzip: true,
      file: archivePath
    },
    ['Assets/Scripts/VALYR', 'Assets/Settings', 'README.md', 'LICENSE']
  );

  console.log(`🎉 Successfully created archive: ${archivePath} (${(fs.statSync(archivePath).size / 1024).toFixed(1)} KB)`);

  // Clean up temporary staging directory
  fs.rmSync(tempStaging, { recursive: true, force: true });
  console.log(`✨ Build completed.`);
}

packAsset().catch((err) => {
  console.error('❌ Failed to pack asset:', err);
  process.exit(1);
});
