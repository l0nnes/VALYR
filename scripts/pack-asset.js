const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const tar = require('tar');

function getGuidFromMeta(metaPath) {
  if (!fs.existsSync(metaPath)) return null;
  const content = fs.readFileSync(metaPath, 'utf8');
  const match = content.match(/guid:\s*([a-f0-9]{32})/i);
  return match ? match[1] : null;
}

function collectAssets(dir, list = []) {
  if (!fs.existsSync(dir)) return list;
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);
    if (entry.name.endsWith('.meta')) continue;
    list.push(fullPath);
    if (entry.isDirectory()) {
      collectAssets(fullPath, list);
    }
  }
  return list;
}

async function packAsset() {
  const rootPkgPath = path.resolve('package.json');
  const rootPkg = JSON.parse(fs.readFileSync(rootPkgPath, 'utf8'));
  const version = process.argv[2] || process.env.SEMANTIC_RELEASE_VERSION || rootPkg.version || '0.0.0';
  const outDir = path.resolve('dist');

  console.log(`📦 Packaging VALYR UPM packages and distributions (version: ${version})...`);

  // 1. Sync version in Packages/com.valyr.core/package.json
  const corePkgPath = path.resolve('Packages/com.valyr.core/package.json');
  if (fs.existsSync(corePkgPath)) {
    const corePkg = JSON.parse(fs.readFileSync(corePkgPath, 'utf8'));
    corePkg.version = version;
    fs.writeFileSync(corePkgPath, JSON.stringify(corePkg, null, 2) + '\n', 'utf8');
    console.log(`✅ Updated ${corePkgPath} version to ${version}`);
  }

  // 2. Sync version and dependency in Packages/com.valyr.multiplayer/package.json
  const multiPkgPath = path.resolve('Packages/com.valyr.multiplayer/package.json');
  if (fs.existsSync(multiPkgPath)) {
    const multiPkg = JSON.parse(fs.readFileSync(multiPkgPath, 'utf8'));
    multiPkg.version = version;
    if (multiPkg.dependencies && multiPkg.dependencies['com.valyr.core']) {
      multiPkg.dependencies['com.valyr.core'] = `https://github.com/l0nnes/VALYR.git?path=Packages/com.valyr.core#v${version}`;
    }
    fs.writeFileSync(multiPkgPath, JSON.stringify(multiPkg, null, 2) + '\n', 'utf8');
    console.log(`✅ Updated ${multiPkgPath} version to ${version}`);
  }

  // 3. Prepare output directory
  if (!fs.existsSync(outDir)) {
    fs.mkdirSync(outDir, { recursive: true });
  } else {
    for (const file of fs.readdirSync(outDir)) {
      fs.rmSync(path.join(outDir, file), { recursive: true, force: true });
    }
  }

  // 4. Create UPM .tgz tarballs via npm pack
  console.log('📦 Creating UPM tarball for com.valyr.core...');
  execSync(`npm pack ./Packages/com.valyr.core --pack-destination ./dist`, { stdio: 'inherit' });

  console.log('📦 Creating UPM tarball for com.valyr.multiplayer...');
  execSync(`npm pack ./Packages/com.valyr.multiplayer --pack-destination ./dist`, { stdio: 'inherit' });

  // 5. Create .unitypackage distributions for Core, Multiplayer, and All
  console.log('📦 Creating unitypackages for Core, Multiplayer, and All...');
  await createUnityPackage(['Packages/com.valyr.core'], `VALYR-Core-v${version}.unitypackage`, outDir);
  await createUnityPackage(['Packages/com.valyr.multiplayer'], `VALYR-Multiplayer-v${version}.unitypackage`, outDir);
  await createUnityPackage(['Packages/com.valyr.core', 'Packages/com.valyr.multiplayer', 'Assets/Settings'], `VALYR-All-v${version}.unitypackage`, outDir);

  console.log(`✨ Package build completed successfully.`);
}

async function createUnityPackage(targetDirs, packageName, outDir) {
  const tempStaging = path.resolve(`.unitypackage-temp-${packageName}`);
  if (fs.existsSync(tempStaging)) {
    fs.rmSync(tempStaging, { recursive: true, force: true });
  }
  fs.mkdirSync(tempStaging, { recursive: true });

  const allAssetPaths = [];
  for (const target of targetDirs) {
    if (fs.existsSync(target)) {
      allAssetPaths.push(target);
      collectAssets(target, allAssetPaths);
    }
  }

  const uniquePaths = Array.from(new Set(allAssetPaths));
  const guidDirs = [];

  for (const relPath of uniquePaths) {
    const normalizedPath = relPath.replace(/\\/g, '/');
    const metaPath = relPath + '.meta';
    const guid = getGuidFromMeta(metaPath);
    if (!guid) continue;

    const itemStagingDir = path.join(tempStaging, guid);
    if (!fs.existsSync(itemStagingDir)) {
      fs.mkdirSync(itemStagingDir, { recursive: true });
    }

    fs.writeFileSync(path.join(itemStagingDir, 'pathname'), normalizedPath + '\n', 'utf8');
    fs.copyFileSync(metaPath, path.join(itemStagingDir, 'asset.meta'));

    const stat = fs.statSync(relPath);
    if (!stat.isDirectory()) {
      fs.copyFileSync(relPath, path.join(itemStagingDir, 'asset'));
    }
    guidDirs.push(guid);
  }

  if (guidDirs.length > 0) {
    const unityPackagePath = path.join(outDir, packageName);
    await tar.c({ gzip: true, file: unityPackagePath, cwd: tempStaging }, guidDirs);
    console.log(`🎉 Created unitypackage: ${unityPackagePath} (${(fs.statSync(unityPackagePath).size / 1024).toFixed(1)} KB)`);
  }

  fs.rmSync(tempStaging, { recursive: true, force: true });
}

packAsset().catch((err) => {
  console.error('❌ Failed to pack asset:', err);
  process.exit(1);
});
