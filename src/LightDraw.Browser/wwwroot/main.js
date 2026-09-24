import createDotnetRuntime from './_framework/dotnet.js';

const loading = document.getElementById('loading');
const status = document.getElementById('loading-status');
const detail = document.getElementById('loading-detail');
const progress = document.getElementById('loading-progress');
const errorDetail = document.getElementById('loading-error');
let started = false;
let lastProgress = 0;

function showError(error) {
  if (started) return;
  status.textContent = 'LightDraw 启动失败';
  detail.textContent = '请检查浏览器控制台，然后刷新页面重试。';
  progress.hidden = true;
  errorDetail.hidden = false;
  errorDetail.textContent = error instanceof Error ? error.message : String(error);
}

globalThis.addEventListener('error', event => showError(event.error ?? event.message));
globalThis.addEventListener('unhandledrejection', event => showError(event.reason));

const observer = new MutationObserver(() => {
  if (document.querySelector('#out canvas')) {
    started = true;
    loading.hidden = true;
    observer.disconnect();
  }
});
observer.observe(document.getElementById('out'), { childList: true, subtree: true });

try {
  const runtime = await createDotnetRuntime({
    onDownloadResourceProgress(resourcesLoaded, totalResources) {
      if (totalResources <= 0) return;
      lastProgress = Math.max(lastProgress, Math.min(99, Math.floor(resourcesLoaded / totalResources * 100)));
      progress.value = lastProgress;
      detail.textContent = `正在下载资源 ${resourcesLoaded}/${totalResources}`;
    }
  });
  status.textContent = '正在启动 LightDraw…';
  detail.textContent = '资源已下载，正在初始化画布…';
  progress.removeAttribute('value');
  const config = runtime.getConfig();
  await runtime.runMain(config.mainAssemblyName, [globalThis.location.href]);
} catch (error) {
  showError(error);
}
