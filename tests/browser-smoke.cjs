// Runs in a fresh browser context with synthetic data only.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { spawn } = require('node:child_process');

(async () => {
  fs.mkdirSync('artifacts', { recursive: true });
  const host = spawn(process.env.PYTHON || 'python', ['scripts/serve-pages.py', 'artifacts/publish/wwwroot', '--port', '5159'], { stdio: ['ignore', 'pipe', 'pipe'] });
  const base = 'http://127.0.0.1:5159/PhilosophyLab/';
  let browser;
  try {
    await new Promise((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error('Static host did not start')), 10000);
      host.stdout.once('data', () => { clearTimeout(timeout); resolve(); });
      host.once('error', reject);
      host.once('exit', code => { if (code) reject(new Error('Static host failed: ' + code)); });
    });
    browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, colorScheme: 'light', reducedMotion: 'reduce' });
    const page = await context.newPage();
    const errors = [];
    const external = [];
    page.on('pageerror', e => errors.push(e.message));
    page.on('request', r => { if (!r.url().startsWith('http://127.0.0.1:5159/') && !r.url().startsWith('data:') && !r.url().startsWith('blob:')) external.push(r.url()); });
    async function ready() { await page.getByRole('link', { name: 'Creative revision', exact: true }).waitFor(); }
    async function a11y(label) {
      await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') });
      const result = await page.evaluate(async () => axe.run(document, { runOnly: ['wcag2a', 'wcag2aa', 'wcag21aa'] }));
      assert.deepEqual(result.violations.map(v => ({ id: v.id, nodes: v.nodes.map(n => n.target) })), [], label + ' accessibility');
    }
    await page.goto(base + '?lab=behavior'); await ready();
    await a11y('library');
    await page.screenshot({ path: 'artifacts/behavior-desktop.png', fullPage: true });
    await page.keyboard.press('Tab');
    // Check a direct deep link uses the custom 404 shell but still mounts the app.
    const direct = await page.goto(base + 'saved'); assert.equal(direct.status(), 404);
    await page.getByRole('heading', { name: 'Saved on this device' }).waitFor();
    await page.getByRole('textbox', { name: "New person's name" }).fill('QA A');
    await page.getByRole('button', { name: 'Add person', exact: true }).click();
    await page.getByRole('combobox', { name: "Who's answering" }).selectOption({ label: 'QA A' });
    await page.getByRole('textbox', { name: "New person's name" }).fill('QA B');
    await page.getByRole('button', { name: 'Add person', exact: true }).click();
    await page.getByRole('combobox', { name: "Who's answering" }).selectOption({ label: 'QA A' });
    await a11y('saved');

    async function complete(id, switchOwner = false) {
      await page.goto(base + 'test/' + id);
      if (id === 'creativity') {
        await page.getByRole('button', { name: 'Begin', exact: true }).waitFor();
        await page.getByRole('link', { name: 'Skip to content' }).focus();
        await page.keyboard.press('Enter');
        assert.equal(new URL(page.url()).pathname, '/PhilosophyLab/test/creativity', 'skip link stays on the current route');
        await page.waitForFunction(() => document.activeElement?.id === 'main');
      }
      await page.getByRole('button', { name: 'Begin', exact: true }).click();
      let questions = 0;
      while (true) {
        const question = page.locator('.question');
        await question.waitFor();
        const heading = await question.locator('h2').innerText();
        if (await question.locator('.allocation').count()) {
          const input = question.locator('input[type=number]').first();
          await input.fill(await input.getAttribute('max')); await input.press('Tab');
        } else if (await question.locator('.assign-row').count()) {
          for (const row of await question.locator('.assign-row').all()) await row.getByRole('radio').first().check();
        } else if (await question.getByRole('slider').count()) {
          await question.getByRole('slider').press('Home'); await question.getByRole('slider').press('ArrowRight');
        } else if (await question.getByRole('checkbox').count()) {
          const hint = await question.locator('p.hint').innerText();
          const count = Number(hint.match(/Choose (\d+)/)[1]);
          for (let i = 0; i < count; i++) await question.getByRole('checkbox').nth(i).check();
        } else {
          await question.getByRole('radio').first().check();
        }
        if (await question.locator('textarea').count()) {
          await question.locator('textarea').fill('Synthetic QA explanation');
          await question.locator('textarea').press('Tab');
        }
        if (id === 'creativity' && questions === 0) {
          await page.getByRole('button', { name: 'Next', exact: true }).click();
          await page.getByText('You followed your sketch.', { exact: false }).waitFor();
          await page.getByRole('button', { name: 'Back', exact: true }).click();
          await page.getByRole('radio', { name: 'Build a small prototype and see what happens', exact: true }).check();
          await page.getByRole('button', { name: 'Next', exact: true }).click();
          await page.getByText('Your quick prototype exposed', { exact: false }).waitFor();
          await page.getByRole('radio', { name: 'Change one joint and test it again', exact: true }).check();
          await a11y('connected question');
          await page.screenshot({ path: 'artifacts/creativity-sequence.png', fullPage: true });
          if (switchOwner) await page.getByRole('combobox', { name: "Who's answering" }).selectOption({ label: 'QA B' });
          questions++;
        }
        const save = page.getByRole('button', { name: 'Save my answers', exact: true });
        if (await save.count()) {
          await save.click(); await page.waitForURL('**/results/*'); break;
        }
        await page.getByRole('button', { name: 'Next', exact: true }).click();
        await page.waitForFunction(previous => document.querySelector('.question h2')?.textContent !== previous, heading);
        if (++questions > 40) throw new Error('Runner did not finish ' + id);
      }
      await page.getByRole('heading', { name: 'Your answers', exact: true }).waitFor();
      assert.equal(await page.locator('#blazor-error-ui').isVisible(), false, id + ' no framework error');
      console.log('Browser completed:', id);
    }
    await complete('creativity', true);
    assert.match(await page.locator('.result-head').innerText(), /QA A/);
    const resultUrl = page.url();
    await page.reload(); await page.getByRole('heading', { name: 'Your answers', exact: true }).waitFor();
    assert.equal(page.url(), resultUrl);
    await page.goto(base + 'saved');
    await page.getByRole('heading', { name: 'Saved on this device' }).waitFor();
    await page.getByText('Nothing saved yet.', { exact: false }).waitFor();
    await page.getByRole('combobox', { name: "Who's answering" }).selectOption({ label: 'QA A' });
    await page.getByRole('link', { name: 'Creative revision', exact: false }).waitFor();
    const manifest = JSON.parse(fs.readFileSync('wwwroot/data/tests/index.json', 'utf8'));
    for (const file of manifest.tests) {
      const test = JSON.parse(fs.readFileSync(path.join('wwwroot/data/tests', file), 'utf8'));
      if (test.id !== 'creativity') await complete(test.id);
    }
    await page.goto(base + 'profile?lab=behavior');
    await page.getByRole('heading', { name: 'Choices behind the summary' }).waitFor();
    await a11y('profile');
    await page.screenshot({ path: 'artifacts/profile-evidence.png', fullPage: true });
    await page.goto(base + 'growth');
    await page.getByRole('heading', { name: 'Your challenge runs' }).waitFor();
    await a11y('growth');
    await page.goto(base + 'saved');
    await page.getByRole('button', { name: 'Export backup', exact: true }).waitFor();
    const downloadPromise = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Export backup', exact: true }).click();
    const download = await downloadPromise;
    await download.saveAs('artifacts/qa-backup.json');
    const backup = JSON.parse(fs.readFileSync('artifacts/qa-backup.json', 'utf8'));
    assert.equal(backup.results.length, manifest.tests.length);
    assert.ok(backup.results.every(r => r.definition && r.baseline && r.profileId === backup.people.find(p => p.name === 'QA A').id));
    await page.locator('input[type=file]').setInputFiles('artifacts/qa-backup.json');
    await page.getByText('Backup imported. 0 new results added.', { exact: true }).waitFor();
    await page.locator('input[type=file]').setInputFiles({ name: 'invalid.json', mimeType: 'application/json', buffer: Buffer.from('{"schemaVersion":1,"people":null,"results":[]}') });
    await page.getByText('Import failed:', { exact: false }).waitFor();
    await page.goto(base + '?lab=behavior'); await ready();
    await page.goBack(); await page.getByRole('heading', { name: 'Saved on this device' }).waitFor();
    await page.goForward(); await ready();
    for (const colorScheme of ['light', 'dark']) {
      await page.setViewportSize({ width: 390, height: 844 });
      await page.emulateMedia({ colorScheme, reducedMotion: 'reduce' });
      await a11y('mobile ' + colorScheme);
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'mobile has no horizontal overflow');
      await page.screenshot({ path: 'artifacts/behavior-mobile-' + colorScheme + '.png', fullPage: true });
    }
    assert.deepEqual(errors, [], 'no browser runtime errors');
    assert.deepEqual(external, [], 'no external requests');
    console.log('PASS: all 20 tests, deep links, refresh, query strings, history, owner switching, export/import, light/dark mobile, and axe checks.');
  } finally {
    if (browser) await browser.close();
    host.kill();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
