namespace Luma.Core.Lan;

public static class LanWebAssets
{
    public const string IndexHtml = """
<!doctype html>
<html lang="zh-Hans">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>Luma</title>
  <link rel="stylesheet" href="/app.css"/>
</head>
<body>
  <header>
    <strong>Luma</strong>
    <nav>
      <button data-page="record">录制</button>
      <button data-page="library">我的视频</button>
      <button data-page="settings">设置</button>
    </nav>
  </header>
  <main id="record" class="page">
    <p>选择模式后即可开始录制。网页不能关闭局域网或修改端口。</p>
    <div class="row">
      <button data-action="start">开始</button>
      <button data-action="pause">暂停</button>
      <button data-action="stop">停止</button>
    </div>
    <pre id="session"></pre>
  </main>
  <main id="library" class="page hidden">
    <ul id="library-list"></ul>
  </main>
  <main id="settings" class="page hidden">
    <label>主题 <select id="theme"><option>Dark</option><option>Light</option></select></label>
    <label>保存目录 <input id="saveFolder"/></label>
    <button id="save-settings">保存</button>
    <p class="hint">局域网开关、端口和访问密钥只能在桌面设置中修改。</p>
  </main>
  <script src="/app.js"></script>
</body>
</html>
""";

    public const string AppCss = """
body { font-family: Segoe UI, sans-serif; margin: 0; background: #1b1b1b; color: #f3f3f3; }
header { display: flex; gap: 16px; align-items: center; padding: 12px 16px; background: #111; }
nav button { margin-right: 8px; }
main { padding: 16px; }
.hidden { display: none; }
.row button { margin-right: 8px; }
label { display: block; margin: 8px 0; }
.hint { opacity: .7; }
""";

    public const string AppJs = """
const pages = document.querySelectorAll('.page');
document.querySelectorAll('nav button').forEach(btn => {
  btn.addEventListener('click', () => {
    pages.forEach(p => p.classList.toggle('hidden', p.id !== btn.dataset.page));
  });
});

async function api(path, options) {
  const res = await fetch(path, Object.assign({ headers: { 'Content-Type': 'application/json' } }, options));
  return res.json();
}

document.querySelectorAll('[data-action]').forEach(btn => {
  btn.addEventListener('click', async () => {
    const data = await api('/api/v1/session/' + btn.dataset.action, { method: 'POST', body: '{}' });
    document.getElementById('session').textContent = JSON.stringify(data, null, 2);
  });
});

async function refresh() {
  const settings = await api('/api/v1/settings');
  document.getElementById('theme').value = settings.theme || 'Dark';
  document.getElementById('saveFolder').value = settings.saveFolder || '';
  const items = await api('/api/v1/library');
  const list = document.getElementById('library-list');
  list.innerHTML = (items || []).map(i => '<li>' + i.name + '</li>').join('') || '<li>还没有成片</li>';
}

document.getElementById('save-settings').addEventListener('click', async () => {
  await api('/api/v1/settings', {
    method: 'PATCH',
    body: JSON.stringify({
      theme: document.getElementById('theme').value,
      saveFolder: document.getElementById('saveFolder').value
    })
  });
  refresh();
});

refresh();
""";
}
