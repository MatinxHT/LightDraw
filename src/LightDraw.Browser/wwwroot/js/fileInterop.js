export function pickSceneFile() {
  return new Promise((resolve, reject) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.lightdraw.json,.json,application/json';
    input.style.display = 'none';
    document.body.append(input);
    let settled = false;
    let focusTimer;
    const onFocus = () => {
      focusTimer = setTimeout(() => {
        if (!input.files?.length) finish(null);
      }, 500);
    };
    const cleanup = () => {
      clearTimeout(focusTimer);
      window.removeEventListener('focus', onFocus);
      input.remove();
    };
    const finish = value => {
      if (settled) return;
      settled = true;
      cleanup();
      resolve(value);
    };
    input.addEventListener('change', async () => {
      const file = input.files?.[0];
      if (!file) return finish(null);
      try {
        if (file.size > 16 * 1024 * 1024)
          throw new Error('场景文件不能超过 16 MiB。 / Scene files must not exceed 16 MiB.');
        const bytes = new Uint8Array(await file.arrayBuffer());
        let binary = '';
        for (let i = 0; i < bytes.length; i += 8192)
          binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
        finish(`${file.name}:${btoa(binary)}`);
      } catch (error) {
        settled = true;
        cleanup();
        reject(error);
      }
    }, { once: true });
    input.addEventListener('cancel', () => finish(null), { once: true });
    window.addEventListener('focus', onFocus, { once: true });
    input.click();
  });
}

export function downloadFile(fileName, mimeType, base64) {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  const url = URL.createObjectURL(new Blob([bytes], { type: mimeType }));
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 60000);
}

// Register only while dirty so clean pages remain eligible for the browser back/forward cache.
function warnBeforeUnload(event) {
  event.preventDefault();
  event.returnValue = '';
}
export function setUnsavedChanges(hasChanges) {
  window.removeEventListener('beforeunload', warnBeforeUnload);
  if (hasChanges) window.addEventListener('beforeunload', warnBeforeUnload);
}
