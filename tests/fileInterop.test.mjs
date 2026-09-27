import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const listeners = new Map();
globalThis.window = {
  addEventListener(name, callback) {
    if (!listeners.has(name)) listeners.set(name, new Set());
    listeners.get(name).add(callback);
  },
  removeEventListener(name, callback) { listeners.get(name)?.delete(callback); }
};
let input;
globalThis.document = {
  body: { append() {} },
  createElement() {
    input = { style: {}, handlers: {}, removed: false,
      addEventListener(name, callback) { this.handlers[name] = callback; },
      remove() { this.removed = true; }, click() {} };
    return input;
  }
};
const source = await readFile(new URL('../src/LightDraw.Browser/wwwroot/js/fileInterop.js', import.meta.url), 'utf8');
const { setUnsavedChanges, pickSceneFile } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);

setUnsavedChanges(true); setUnsavedChanges(true);
assert.equal(listeners.get('beforeunload').size, 1);
const event = { prevented: false, preventDefault() { this.prevented = true; } };
for (const listener of listeners.get('beforeunload')) listener(event);
assert.equal(event.prevented, true); assert.equal(event.returnValue, '');
setUnsavedChanges(false); assert.equal(listeners.get('beforeunload').size, 0);

const cancelled = pickSceneFile(); input.handlers.cancel();
assert.equal(await cancelled, null); assert.equal(input.removed, true);
assert.equal(listeners.get('focus').size, 0);

const oversized = pickSceneFile();
input.files = [{ size: 16 * 1024 * 1024 + 1, arrayBuffer() { throw new Error('Must not read oversized file'); } }];
await input.handlers.change(); await assert.rejects(oversized, /16 MiB/);
assert.equal(input.removed, true); assert.equal(listeners.get('focus').size, 0);

const selected = pickSceneFile();
input.files = [{ name: 'lesson.lightdraw.json', size: 2, async arrayBuffer() { return new TextEncoder().encode('{}').buffer; } }];
await input.handlers.change(); assert.equal(await selected, 'lesson.lightdraw.json:e30=');
assert.equal(input.removed, true); assert.equal(listeners.get('focus').size, 0);
console.log('PASS browser leave warning, picker cancellation, size limit and file read');
