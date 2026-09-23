const $ = (id) => document.getElementById(id);
const api = (path, opts = {}) => fetch("/api/v1" + path, {
  headers: { "Content-Type": "application/json", ...(opts.headers || {}) },
  keepalive: true,
  ...opts
}).then(async (res) => {
  const json = await res.json().catch(() => ({ ok: false, error: t("api.bad") }));
  if (!json.ok) throw new Error(json.error || t("api.fail"));
  return json.data;
});

const state = {
  page: "record",
  mode: "fullscreen",
  settings: null,
  session: "idle",
  target: null,
  displays: [],
  windows: [],
  games: [],
  region: null,
  windowId: null,
  displayId: "0",
  items: [],
  selectedIds: [],
  selectedId: null,
  selectAnchor: null,
  previewId: null,
  lastSaved: null,
  countTimer: 0,
  poll: 0
};

const I18N = {
  en: {
    "page.record": "Record", "page.library": "Library", "page.settings": "Settings",
    "nav.library": "Library", "nav.settings": "Settings", "nav.back": "Back",
    "mode.fullscreen": "Display", "mode.region": "Region", "mode.window": "Window",
    "mode.game": "Game", "mode.audio": "Audio", "mode.shot": "Screenshot",
    "shot.save": "Save", "shot.copy": "Copy", "shot.cancel": "Cancel",
    "shot.region": "Region", "shot.window": "Window", "shot.display": "Display",
    "shot.save.fail": "Couldn't save the screenshot.",
    "shot.copy.fail": "Couldn't copy the screenshot.",
    "shot.copy.hint": "Right-click the picture to copy it.",
    "shot.window.empty": "No windows to capture.",
    "shot.load.fail": "Couldn't capture the screen.",
    "shot.pen": "Pen", "shot.box": "Rectangle", "shot.circle": "Circle", "shot.arrow": "Arrow",
    "shot.text": "Text", "shot.text.hint": "Type", "shot.color": "Color", "shot.ocr": "Recognize text",
    "shot.ocr.copied": "Text copied", "shot.ocr.empty": "No text found",
    "shot.ocr.fail": "Couldn't recognize text", "shot.ocr.unavailable": "Text recognition isn't available",
    "home.start": "Start", "home.pause": "Pause", "home.stop": "Stop",
    "home.system": "System", "home.mic": "Mic", "home.quality": "Quality",
    "sec.appearance": "Appearance", "settings.theme": "Theme", "settings.theme.desc": "Dark by default",
    "settings.theme.light": "Light", "settings.theme.dark": "Dark",
    "settings.language": "Language", "settings.language.desc": "Default: system",
    "settings.lang.system": "System", "settings.lang.en": "English",
    "settings.lang.zhHans": "简体中文", "settings.lang.zhHant": "繁體中文",
    "settings.lang.ja": "日本語", "settings.lang.ko": "한국어",
    "sec.files": "Files", "settings.save": "Save folder",
    "home.resume": "Resume", "home.preview": "Preview", "page.preview": "Preview",
    "sec.audio": "Audio", "settings.audio.system": "System audio", "settings.audio.mic": "Microphone",
    "settings.audio.micdev": "Mic device", "settings.audio.micdev.ph": "Default mic",
    "settings.audio.only": "Audio only", "settings.audio.only.desc": "Export audio, no video",
    "sec.quality": "Quality", "settings.quality": "Quality", "settings.quality.desc": "Capped by display",
    "settings.fps": "Frame rate", "settings.hw": "Hardware accel", "settings.monitor": "Display",
    "sec.camera": "Camera", "settings.camera": "Camera overlay", "settings.camera.dev": "Device",
    "settings.camera.dev.ph": "Default camera", "settings.pip": "Picture-in-picture",
    "settings.pip.hint": "Drag camera or watermark", "settings.overlay.camera": "Camera",
    "settings.overlay.mark": "Mark", "settings.pos.x": "Horizontal", "settings.pos.y": "Vertical",
    "settings.size.w": "Width", "settings.size.h": "Height",
    "sec.watermark": "Watermark", "settings.stamp": "Timestamp", "settings.wm.text": "Text mark",
    "settings.wm.text.value": "Text", "settings.wm.image.file": "Image path",
    "sec.auto": "Automation", "settings.seg": "Split files", "settings.seg.min": "Split minutes",
    "settings.launchTray": "Start in tray",
    "settings.launchTray.desc": "Start at sign-in in the tray, without recording",
    "sec.system": "System", "sec.hotkeys": "Shortcuts", "settings.hotkey": "Global hotkeys",
    "settings.hotkey.start": "Start",
    "settings.hotkey.press.desc": "Click, then press keys", "settings.hotkey.pause": "Pause",
    "settings.hotkey.stop": "Stop", "settings.hotkey.shot": "Screenshot", "settings.tray": "Close to tray",
    "settings.hideTray": "Hide tray icon", "settings.hideTray.desc": "Start Luma again to show the window",
    "settings.bar": "Recording toolbar", "settings.bar.desc": "Pause, stop, and mic",
    "settings.silent": "Silent mode", "settings.silent.desc": "No popups on start/stop",
    "sec.reset": "Reset", "settings.reset": "Reset", "settings.reset.header": "Reset defaults",
    "sec.about": "About", "about.tagline": "Local screen recorder", "about.privacy": "Privacy",
    "about.terms": "Terms", "about.project": "GitHub",
    "about.docs": "API docs",
    "about.skill": "SKILL",
    "settings.reset.desc": "Theme, hotkeys, audio, and toolbar",
    "settings.reset.confirm": "Theme, hotkeys, audio, and the toolbar go back to factory values.",
    "common.ok": "OK", "common.cancel": "Cancel", "common.close": "Close", "common.save": "Save",
    "sum.display": "Display {0}", "sum.region": "Region {0}×{1}", "sum.region.none": "Region · none",
    "sum.window.none": "Window · none", "sum.window": "Window · {0}", "sum.game.none": "Game · none",
    "sum.game": "Game · {0}", "sum.pick": "Pick a target", "sum.audio": "Audio only",
    "sum.sys.on": "Sys on", "sum.sys.off": "Sys off", "sum.mic.on": "Mic on", "sum.mic.off": "Mic off",
    "quality.sd": "720p", "quality.hd": "1080p", "quality.qhd": "1440p", "quality.uhd": "4K",
    "rec.need.target": "Pick a target first.", "rec.need.region": "Pick a region first.",
    "rec.processing": "Processing", "rec.saved": "Saved", "modes.aria": "Capture mode",
    "lib.preview": "Preview", "lib.rename": "Rename", "lib.delete": "Delete", "lib.folder": "Open folder",
    "lib.more": "More", "lib.repair": "Repair", "lib.merge": "Merge", "lib.subtitle": "Captions",
    "lib.music": "Music", "lib.refresh": "Refresh", "lib.empty": "No videos yet",
    "lib.empty.desc": "Finished recordings appear here", "lib.col.name": "Name", "lib.col.size": "Size",
    "lib.col.duration": "Time", "lib.col.date": "Date", "lib.fullscreen": "Full screen",
    "lib.trim": "Trim", "lib.compress": "Compress", "lib.rename.invalid": "Enter a valid name",
    "lib.rename.fail": "Could not rename", "lib.delete.title": "Delete files",
    "lib.delete.one": "Delete {0}?", "lib.delete.many": "Delete {0} files?",
    "lib.compress.body": "Export a new file.", "lib.compress.export": "Export new",
    "lib.trim.start": "Start (sec)", "lib.trim.end": "End (sec)", "lib.export": "Export",
    "lib.repairing": "Repairing…", "lib.compressing": "Compressing…", "lib.trimming": "Trimming…",
    "lib.repaired": "Repaired", "lib.compress.exported": "Exported compressed file",
    "lib.trim.exported": "Exported trim", "lib.merge.need": "Select at least two files with Ctrl.",
    "lib.merge.web": "Merge on the PC.", "job.fail": "Job failed.", "job.timeout": "Job did not finish.",
    "api.bad": "Could not parse the response.", "api.fail": "Request failed."
  },
  "zh-Hans": {
    "page.record": "录制", "page.library": "我的视频", "page.settings": "设置",
    "nav.library": "我的视频", "nav.settings": "设置", "nav.back": "返回",
    "mode.fullscreen": "显示器", "mode.region": "区域", "mode.window": "窗口",
    "mode.game": "游戏", "mode.audio": "录音", "mode.shot": "截图",
    "shot.save": "保存", "shot.copy": "复制", "shot.cancel": "取消",
    "shot.region": "区域", "shot.window": "窗口", "shot.display": "整屏",
    "shot.save.fail": "无法保存截图。",
    "shot.copy.fail": "无法复制截图。",
    "shot.copy.hint": "右键图片即可复制。",
    "shot.window.empty": "没有可截取的窗口。",
    "shot.load.fail": "无法截取屏幕。",
    "shot.pen": "画笔", "shot.box": "矩形", "shot.circle": "圆形", "shot.arrow": "箭头",
    "shot.text": "文字", "shot.text.hint": "输入文字", "shot.color": "颜色", "shot.ocr": "识别文字",
    "shot.ocr.copied": "已复制识别出的文字", "shot.ocr.empty": "没有识别到文字",
    "shot.ocr.fail": "无法识别文字", "shot.ocr.unavailable": "没有可用的文字识别",
    "home.start": "开始录制", "home.pause": "暂停", "home.stop": "停止",
    "home.system": "系统声", "home.mic": "麦", "home.quality": "清晰度",
    "sec.appearance": "外观", "settings.theme": "应用主题", "settings.theme.desc": "默认深色，也可改成浅色",
    "settings.theme.light": "浅色", "settings.theme.dark": "深色",
    "settings.language": "界面语言", "settings.language.desc": "默认跟随系统",
    "settings.lang.system": "跟随系统", "settings.lang.en": "English",
    "settings.lang.zhHans": "简体中文", "settings.lang.zhHant": "繁體中文",
    "settings.lang.ja": "日本語", "settings.lang.ko": "한국어",
    "sec.files": "文件", "settings.save": "保存位置",
    "home.resume": "继续", "home.preview": "预览", "page.preview": "预览",
    "sec.audio": "声音", "settings.audio.system": "电脑播放声", "settings.audio.mic": "麦克风",
    "settings.audio.micdev": "麦克风设备", "settings.audio.micdev.ph": "默认麦克风",
    "settings.audio.only": "只录声音", "settings.audio.only.desc": "不录画面，导出音频文件",
    "sec.quality": "画质", "settings.quality": "清晰度", "settings.quality.desc": "最高清晰度受当前显示器限制",
    "settings.fps": "帧率", "settings.hw": "硬件加速", "settings.monitor": "显示器",
    "sec.camera": "摄像头", "settings.camera": "嵌入摄像头", "settings.camera.dev": "设备",
    "settings.camera.dev.ph": "默认摄像头", "settings.pip": "画中画预览",
    "settings.pip.hint": "直接拖动摄像头或水印方块", "settings.overlay.camera": "摄像头",
    "settings.overlay.mark": "水印", "settings.pos.x": "水平位置", "settings.pos.y": "垂直位置",
    "settings.size.w": "宽度", "settings.size.h": "高度",
    "sec.watermark": "水印", "settings.stamp": "时间戳", "settings.wm.text": "文字水印",
    "settings.wm.text.value": "文字", "settings.wm.image.file": "图片水印路径",
    "sec.auto": "自动化", "settings.seg": "分段录制", "settings.seg.min": "分段分钟",
    "settings.launchTray": "登录后进托盘",
    "settings.launchTray.desc": "登录后启动并进入托盘，不自动开录",
    "sec.system": "系统", "sec.hotkeys": "快捷键", "settings.hotkey": "启用全局热键",
    "settings.hotkey.start": "开始",
    "settings.hotkey.press.desc": "点击框后按下组合键", "settings.hotkey.pause": "暂停",
    "settings.hotkey.stop": "停止", "settings.hotkey.shot": "截图", "settings.tray": "关闭时最小化到托盘",
    "settings.hideTray": "隐藏托盘图标", "settings.hideTray.desc": "再开一次即可找回窗口",
    "settings.bar": "录制时显示浮动工具栏", "settings.bar.desc": "屏幕上方的暂停、停止和麦克风",
    "settings.silent": "静默模式", "settings.silent.desc": "网页开始或停止时，不弹出主窗口、浮动工具条和处理完成页",
    "sec.reset": "重置", "settings.reset": "恢复默认", "settings.reset.header": "恢复默认设置",
    "sec.about": "关于", "about.tagline": "映录 · 本地录屏", "about.privacy": "隐私协议",
    "about.terms": "使用条款", "about.project": "项目主页",
    "about.docs": "API 文档",
    "about.skill": "SKILL",
    "settings.reset.desc": "主题、热键、声音和工具栏等都会回到初始值",
    "settings.reset.confirm": "主题、热键、声音和工具栏等都会回到初始值，确定恢复默认？",
    "common.ok": "确定", "common.cancel": "取消", "common.close": "关闭", "common.save": "保存",
    "sum.display": "显示器 {0}", "sum.region": "区域 {0}×{1}", "sum.region.none": "区域 · 尚未选择",
    "sum.window.none": "窗口 · 尚未选择", "sum.window": "窗口 · {0}", "sum.game.none": "游戏 · 尚未选择",
    "sum.game": "游戏 · {0}", "sum.pick": "选择目标", "sum.audio": "只录音 · 不录画面",
    "sum.sys.on": "系统声开", "sum.sys.off": "系统声关", "sum.mic.on": "麦开", "sum.mic.off": "麦关",
    "quality.sd": "标清 720p", "quality.hd": "高清 1080p", "quality.qhd": "超清 1440p", "quality.uhd": "蓝光 4K",
    "rec.need.target": "请先选择要录制的目标。", "rec.need.region": "请先选择要录制的区域。",
    "rec.processing": "正在处理中", "rec.saved": "已保存", "modes.aria": "采集模式",
    "lib.preview": "预览", "lib.rename": "重命名", "lib.delete": "删除", "lib.folder": "打开目录",
    "lib.more": "更多", "lib.repair": "修复", "lib.merge": "合并", "lib.subtitle": "字幕",
    "lib.music": "配乐", "lib.refresh": "刷新", "lib.empty": "还没有成片",
    "lib.empty.desc": "录完的视频会出现在这里", "lib.col.name": "名称", "lib.col.size": "大小",
    "lib.col.duration": "时长", "lib.col.date": "日期", "lib.fullscreen": "全屏",
    "lib.trim": "剪切", "lib.compress": "压缩", "lib.rename.invalid": "请输入有效的文件名",
    "lib.rename.fail": "无法重命名", "lib.delete.title": "删除文件",
    "lib.delete.one": "确定删除 {0} ?", "lib.delete.many": "确定删除 {0} 个文件?",
    "lib.compress.body": "导出为新文件。", "lib.compress.export": "导出新文件",
    "lib.trim.start": "开始秒", "lib.trim.end": "结束秒", "lib.export": "导出",
    "lib.repairing": "正在修复…", "lib.compressing": "正在压缩…", "lib.trimming": "正在剪切…",
    "lib.repaired": "已修复", "lib.compress.exported": "已导出压缩文件",
    "lib.trim.exported": "已导出剪切文件", "lib.merge.need": "请在列表中按住 Ctrl 多选至少两个文件再合并。",
    "lib.merge.web": "合并请在电脑端使用。", "job.fail": "作业失败。", "job.timeout": "作业未结束。",
    "api.bad": "无法解析响应。", "api.fail": "请求失败。"
  },
  "zh-Hant": {
    "page.record": "錄製", "page.library": "我的影片", "page.settings": "設定",
    "nav.library": "我的影片", "nav.settings": "設定", "nav.back": "返回",
    "mode.fullscreen": "顯示器", "mode.region": "區域", "mode.window": "視窗",
    "mode.game": "遊戲", "mode.audio": "錄音", "mode.shot": "截圖",
    "shot.save": "儲存", "shot.copy": "複製", "shot.cancel": "取消",
    "shot.region": "區域", "shot.window": "視窗", "shot.display": "整屏",
    "shot.save.fail": "無法儲存截圖。",
    "shot.copy.fail": "無法複製截圖。",
    "shot.copy.hint": "右鍵圖片即可複製。",
    "shot.window.empty": "沒有可截取的視窗。",
    "shot.load.fail": "無法截取畫面。",
    "shot.pen": "畫筆", "shot.box": "矩形", "shot.circle": "圓形", "shot.arrow": "箭頭",
    "shot.text": "文字", "shot.text.hint": "輸入文字", "shot.color": "顏色", "shot.ocr": "識別文字",
    "shot.ocr.copied": "已複製識別出的文字", "shot.ocr.empty": "沒有識別到文字",
    "shot.ocr.fail": "無法識別文字", "shot.ocr.unavailable": "沒有可用的文字識別",
    "home.start": "開始錄製", "home.pause": "暫停", "home.stop": "停止",
    "home.system": "系統聲", "home.mic": "麥", "home.quality": "清晰度",
    "sec.appearance": "外觀", "settings.theme": "應用主題", "settings.theme.desc": "預設深色",
    "settings.theme.light": "淺色", "settings.theme.dark": "深色",
    "settings.language": "介面語言", "settings.language.desc": "預設跟隨系統",
    "settings.lang.system": "跟隨系統", "settings.lang.en": "English",
    "settings.lang.zhHans": "简体中文", "settings.lang.zhHant": "繁體中文",
    "settings.lang.ja": "日本語", "settings.lang.ko": "한국어",
    "sec.files": "檔案", "settings.save": "儲存位置",
    "home.resume": "繼續", "home.preview": "預覽", "page.preview": "預覽",
    "sec.audio": "聲音", "settings.audio.system": "電腦播放聲", "settings.audio.mic": "麥克風",
    "settings.audio.micdev": "麥克風裝置", "settings.audio.micdev.ph": "預設麥克風",
    "settings.audio.only": "只錄音", "settings.audio.only.desc": "不錄畫面，匯出音訊檔",
    "sec.quality": "畫質", "settings.quality": "清晰度", "settings.quality.desc": "最高清晰度受目前顯示器限制",
    "settings.fps": "幀率", "settings.hw": "硬體加速", "settings.monitor": "顯示器",
    "sec.camera": "攝影機", "settings.camera": "嵌入攝影機", "settings.camera.dev": "裝置",
    "settings.camera.dev.ph": "預設攝影機", "settings.pip": "子母畫面預覽",
    "settings.pip.hint": "直接拖曳攝影機或浮水印", "settings.overlay.camera": "攝影機",
    "settings.overlay.mark": "浮水印", "settings.pos.x": "水平位置", "settings.pos.y": "垂直位置",
    "settings.size.w": "寬度", "settings.size.h": "高度",
    "sec.watermark": "浮水印", "settings.stamp": "時間戳", "settings.wm.text": "文字浮水印",
    "settings.wm.text.value": "文字", "settings.wm.image.file": "圖片浮水印路徑",
    "sec.auto": "自動化", "settings.seg": "分段錄製", "settings.seg.min": "分段分鐘",
    "settings.launchTray": "登入後進系統匣",
    "settings.launchTray.desc": "登入後啟動並進入系統匣，不自動開錄",
    "sec.system": "系統", "sec.hotkeys": "快捷鍵", "settings.hotkey": "啟用全域熱鍵",
    "settings.hotkey.start": "開始",
    "settings.hotkey.press.desc": "點擊框後按下組合鍵", "settings.hotkey.pause": "暫停",
    "settings.hotkey.stop": "停止", "settings.hotkey.shot": "截圖", "settings.tray": "關閉時最小化到工作列",
    "settings.hideTray": "隱藏系統匣圖示", "settings.hideTray.desc": "再開一次即可找回視窗",
    "settings.bar": "錄製時顯示浮動工具列", "settings.bar.desc": "螢幕上方的暫停、停止和麥克風",
    "settings.silent": "靜默模式", "settings.silent.desc": "開始或停止時不跳出主視窗、浮動工具列和處理完成頁",
    "sec.reset": "重設", "settings.reset": "還原預設", "settings.reset.header": "還原預設設定",
    "sec.about": "關於", "about.tagline": "映錄 · 本機錄影", "about.privacy": "隱私權政策",
    "about.terms": "使用條款", "about.project": "專案首頁",
    "about.docs": "API 文件",
    "about.skill": "SKILL",
    "settings.reset.desc": "主題、熱鍵、聲音和工具列等都會回到初始值",
    "settings.reset.confirm": "主題、熱鍵、聲音和工具列等都會回到初始值，確定還原預設？",
    "common.ok": "確定", "common.cancel": "取消", "common.close": "關閉", "common.save": "儲存",
    "sum.display": "顯示器 {0}", "sum.region": "區域 {0}×{1}", "sum.region.none": "區域 · 尚未選擇",
    "sum.window.none": "視窗 · 尚未選擇", "sum.window": "視窗 · {0}", "sum.game.none": "遊戲 · 尚未選擇",
    "sum.game": "遊戲 · {0}", "sum.pick": "選擇目標", "sum.audio": "只錄音 · 不錄畫面",
    "sum.sys.on": "系統聲開", "sum.sys.off": "系統聲關", "sum.mic.on": "麥開", "sum.mic.off": "麥關",
    "quality.sd": "標清 720p", "quality.hd": "高清 1080p", "quality.qhd": "超清 1440p", "quality.uhd": "藍光 4K",
    "rec.need.target": "請先選擇要錄製的目標。", "rec.need.region": "請先選擇要錄製的區域。",
    "rec.processing": "正在處理中", "rec.saved": "已儲存", "modes.aria": "擷取模式",
    "lib.preview": "預覽", "lib.rename": "重新命名", "lib.delete": "刪除", "lib.folder": "開啟目錄",
    "lib.more": "更多", "lib.repair": "修復", "lib.merge": "合併", "lib.subtitle": "字幕",
    "lib.music": "配樂", "lib.refresh": "重新整理", "lib.empty": "還沒有成片",
    "lib.empty.desc": "錄完的影片會出現在這裡", "lib.col.name": "名稱", "lib.col.size": "大小",
    "lib.col.duration": "時長", "lib.col.date": "日期", "lib.fullscreen": "全螢幕",
    "lib.trim": "剪輯", "lib.compress": "壓縮", "lib.rename.invalid": "請輸入有效的檔名",
    "lib.rename.fail": "無法重新命名", "lib.delete.title": "刪除檔案",
    "lib.delete.one": "確定刪除 {0} ?", "lib.delete.many": "確定刪除 {0} 個檔案?",
    "lib.compress.body": "匯出為新檔案。", "lib.compress.export": "匯出新檔案",
    "lib.trim.start": "開始秒", "lib.trim.end": "結束秒", "lib.export": "匯出",
    "lib.repairing": "正在修復…", "lib.compressing": "正在壓縮…", "lib.trimming": "正在剪輯…",
    "lib.repaired": "已修復", "lib.compress.exported": "已匯出壓縮檔",
    "lib.trim.exported": "已匯出剪輯檔", "lib.merge.need": "請在清單中按住 Ctrl 多選至少兩個檔案再合併。",
    "lib.merge.web": "合併請在電腦端使用。", "job.fail": "作業失敗。", "job.timeout": "作業未結束。",
    "api.bad": "無法解析回應。", "api.fail": "請求失敗。"
  },
  ja: {
    "page.record": "録画", "page.library": "ライブラリ", "page.settings": "設定",
    "nav.library": "ライブラリ", "nav.settings": "設定", "nav.back": "戻る",
    "mode.fullscreen": "画面", "mode.region": "範囲", "mode.window": "ウィンドウ",
    "mode.game": "ゲーム", "mode.audio": "音声", "mode.shot": "スクリーンショット",
    "shot.save": "保存", "shot.copy": "コピー", "shot.cancel": "キャンセル",
    "shot.region": "範囲", "shot.window": "ウィンドウ", "shot.display": "画面全体",
    "shot.save.fail": "スクリーンショットを保存できません。",
    "shot.copy.fail": "スクリーンショットをコピーできません。",
    "shot.copy.hint": "画像を右クリックしてコピーできます。",
    "shot.window.empty": "取り込めるウィンドウがありません。",
    "shot.load.fail": "画面を取り込めません。",
    "shot.pen": "ペン", "shot.box": "四角", "shot.circle": "円", "shot.arrow": "矢印",
    "shot.text": "文字", "shot.text.hint": "入力", "shot.color": "色", "shot.ocr": "文字認識",
    "shot.ocr.copied": "認識した文字をコピーしました", "shot.ocr.empty": "文字が見つかりません",
    "shot.ocr.fail": "文字を認識できません", "shot.ocr.unavailable": "文字認識を利用できません",
    "home.start": "開始", "home.pause": "一時停止", "home.stop": "停止",
    "home.system": "システム", "home.mic": "マイク", "home.quality": "画質",
    "sec.appearance": "外観", "settings.theme": "テーマ", "settings.theme.desc": "既定はダーク",
    "settings.theme.light": "ライト", "settings.theme.dark": "ダーク",
    "settings.language": "表示言語", "settings.language.desc": "既定：システム",
    "settings.lang.system": "システム", "settings.lang.en": "English",
    "settings.lang.zhHans": "简体中文", "settings.lang.zhHant": "繁體中文",
    "settings.lang.ja": "日本語", "settings.lang.ko": "한국어",
    "sec.files": "ファイル", "settings.save": "保存先",
    "home.resume": "再開", "home.preview": "プレビュー", "page.preview": "プレビュー",
    "sec.audio": "音声", "settings.audio.system": "システム音声", "settings.audio.mic": "マイク",
    "settings.audio.micdev": "マイク機器", "settings.audio.micdev.ph": "既定マイク",
    "settings.audio.only": "音声のみ", "settings.audio.only.desc": "映像なし、音声を書き出す",
    "sec.quality": "画質", "settings.quality": "画質", "settings.quality.desc": "解像度は画面に依存",
    "settings.fps": "フレームレート", "settings.hw": "ハードウェア加速", "settings.monitor": "画面",
    "sec.camera": "カメラ", "settings.camera": "カメラ重ね", "settings.camera.dev": "機器",
    "settings.camera.dev.ph": "既定カメラ", "settings.pip": "ピクチャインピクチャ",
    "settings.pip.hint": "カメラや透かしをドラッグ", "settings.overlay.camera": "カメラ",
    "settings.overlay.mark": "透かし", "settings.pos.x": "水平", "settings.pos.y": "垂直",
    "settings.size.w": "幅", "settings.size.h": "高さ",
    "sec.watermark": "透かし", "settings.stamp": "タイムスタンプ", "settings.wm.text": "文字透かし",
    "settings.wm.text.value": "文字", "settings.wm.image.file": "画像のパス",
    "sec.auto": "自動化", "settings.seg": "分割録画", "settings.seg.min": "分割（分）",
    "settings.launchTray": "トレイで起動",
    "settings.launchTray.desc": "サインイン時にトレイで起動（録画しない）",
    "sec.system": "システム", "sec.hotkeys": "ショートカット", "settings.hotkey": "グローバルホットキー",
    "settings.hotkey.start": "開始",
    "settings.hotkey.press.desc": "枠をクリックしてキーを押す", "settings.hotkey.pause": "一時停止",
    "settings.hotkey.stop": "停止", "settings.hotkey.shot": "スクショ", "settings.tray": "トレイにしまう",
    "settings.hideTray": "トレイを隠す", "settings.hideTray.desc": "もう一度起動すると窓が戻る",
    "settings.bar": "録画ツールバー", "settings.bar.desc": "一時停止・停止・マイク",
    "settings.silent": "サイレント", "settings.silent.desc": "開始・停止時に窓を出さない",
    "sec.reset": "リセット", "settings.reset": "初期化", "settings.reset.header": "初期設定に戻す",
    "sec.about": "情報", "about.tagline": "ローカル画面録画", "about.privacy": "プライバシー",
    "about.terms": "利用規約", "about.project": "プロジェクト",
    "about.docs": "API ドキュメント",
    "about.skill": "SKILL",
    "settings.reset.desc": "テーマ、ホットキー、音声、ツールバー",
    "settings.reset.confirm": "テーマ、ホットキー、音声、ツールバーを初期値に戻します。",
    "common.ok": "OK", "common.cancel": "キャンセル", "common.close": "閉じる", "common.save": "保存",
    "sum.display": "画面 {0}", "sum.region": "範囲 {0}×{1}", "sum.region.none": "範囲 · 未選択",
    "sum.window.none": "ウィンドウ · 未選択", "sum.window": "ウィンドウ · {0}", "sum.game.none": "ゲーム · 未選択",
    "sum.game": "ゲーム · {0}", "sum.pick": "対象を選択", "sum.audio": "音声のみ",
    "sum.sys.on": "システムオン", "sum.sys.off": "システムオフ", "sum.mic.on": "マイクオン", "sum.mic.off": "マイクオフ",
    "quality.sd": "720p", "quality.hd": "1080p", "quality.qhd": "1440p", "quality.uhd": "4K",
    "rec.need.target": "先に対象を選んでください。", "rec.need.region": "先に範囲を選んでください。",
    "rec.processing": "処理中", "rec.saved": "保存しました", "modes.aria": "取り込みモード",
    "lib.preview": "プレビュー", "lib.rename": "名前変更", "lib.delete": "削除", "lib.folder": "フォルダを開く",
    "lib.more": "その他", "lib.repair": "修復", "lib.merge": "結合", "lib.subtitle": "字幕",
    "lib.music": "BGM", "lib.refresh": "更新", "lib.empty": "まだ映像がありません",
    "lib.empty.desc": "録画した映像がここに出ます", "lib.col.name": "名前", "lib.col.size": "サイズ",
    "lib.col.duration": "時間", "lib.col.date": "日付", "lib.fullscreen": "全画面",
    "lib.trim": "切り取り", "lib.compress": "圧縮", "lib.rename.invalid": "有効な名前を入力",
    "lib.rename.fail": "名前を変更できません", "lib.delete.title": "ファイルを削除",
    "lib.delete.one": "{0} を削除しますか?", "lib.delete.many": "{0} 個のファイルを削除しますか?",
    "lib.compress.body": "新しいファイルに書き出します。", "lib.compress.export": "書き出す",
    "lib.trim.start": "開始（秒）", "lib.trim.end": "終了（秒）", "lib.export": "書き出す",
    "lib.repairing": "修復中…", "lib.compressing": "圧縮中…", "lib.trimming": "切り取り中…",
    "lib.repaired": "修復しました", "lib.compress.exported": "圧縮を書き出し",
    "lib.trim.exported": "切り取りを書き出し", "lib.merge.need": "Ctrl で 2 件以上選んで結合。",
    "lib.merge.web": "結合は PC で行ってください。", "job.fail": "ジョブに失敗しました。", "job.timeout": "ジョブが終わりません。",
    "api.bad": "応答を解析できません。", "api.fail": "リクエストに失敗しました。"
  },
  ko: {
    "page.record": "녹화", "page.library": "보관함", "page.settings": "설정",
    "nav.library": "보관함", "nav.settings": "설정", "nav.back": "뒤로",
    "mode.fullscreen": "화면", "mode.region": "영역", "mode.window": "창",
    "mode.game": "게임", "mode.audio": "오디오", "mode.shot": "스크린샷",
    "shot.save": "저장", "shot.copy": "복사", "shot.cancel": "취소",
    "shot.region": "영역", "shot.window": "창", "shot.display": "전체 화면",
    "shot.save.fail": "스크린샷을 저장할 수 없습니다.",
    "shot.copy.fail": "스크린샷을 복사할 수 없습니다.",
    "shot.copy.hint": "그림을 마우스 오른쪽 단추로 클릭해 복사하세요.",
    "shot.window.empty": "캡처할 창이 없습니다.",
    "shot.load.fail": "화면을 캡처할 수 없습니다.",
    "shot.pen": "펜", "shot.box": "사각형", "shot.circle": "원", "shot.arrow": "화살표",
    "shot.text": "텍스트", "shot.text.hint": "입력", "shot.color": "색", "shot.ocr": "텍스트 인식",
    "shot.ocr.copied": "인식한 텍스트를 복사했습니다", "shot.ocr.empty": "텍스트를 찾지 못했습니다",
    "shot.ocr.fail": "텍스트를 인식하지 못했습니다", "shot.ocr.unavailable": "텍스트 인식을 사용할 수 없습니다",
    "home.start": "시작", "home.pause": "일시정지", "home.stop": "중지",
    "home.system": "시스템", "home.mic": "마이크", "home.quality": "화질",
    "sec.appearance": "모양", "settings.theme": "테마", "settings.theme.desc": "기본은 어두운 테마",
    "settings.theme.light": "밝게", "settings.theme.dark": "어둡게",
    "settings.language": "표시 언어", "settings.language.desc": "기본: 시스템",
    "settings.lang.system": "시스템", "settings.lang.en": "English",
    "settings.lang.zhHans": "简体中文", "settings.lang.zhHant": "繁體中文",
    "settings.lang.ja": "日本語", "settings.lang.ko": "한국어",
    "sec.files": "파일", "settings.save": "저장 위치",
    "home.resume": "계속", "home.preview": "미리보기", "page.preview": "미리보기",
    "sec.audio": "소리", "settings.audio.system": "시스템 소리", "settings.audio.mic": "마이크",
    "settings.audio.micdev": "마이크 장치", "settings.audio.micdev.ph": "기본 마이크",
    "settings.audio.only": "오디오만", "settings.audio.only.desc": "화면 없이 오디오 내보내기",
    "sec.quality": "화질", "settings.quality": "화질", "settings.quality.desc": "해상도는 화면에 따름",
    "settings.fps": "프레임", "settings.hw": "하드웨어 가속", "settings.monitor": "화면",
    "sec.camera": "카메라", "settings.camera": "카메라 오버레이", "settings.camera.dev": "장치",
    "settings.camera.dev.ph": "기본 카메라", "settings.pip": "화면 속 화면",
    "settings.pip.hint": "카메라나 워터마크를 드래그", "settings.overlay.camera": "카메라",
    "settings.overlay.mark": "마크", "settings.pos.x": "가로", "settings.pos.y": "세로",
    "settings.size.w": "너비", "settings.size.h": "높이",
    "sec.watermark": "워터마크", "settings.stamp": "타임스탬프", "settings.wm.text": "텍스트 마크",
    "settings.wm.text.value": "텍스트", "settings.wm.image.file": "이미지 경로",
    "sec.auto": "자동화", "settings.seg": "분할 녹화", "settings.seg.min": "분할(분)",
    "settings.launchTray": "트레이로 시작",
    "settings.launchTray.desc": "로그인 시 트레이로 시작, 녹화 없음",
    "sec.system": "시스템", "sec.hotkeys": "단축키", "settings.hotkey": "전역 단축키",
    "settings.hotkey.start": "시작",
    "settings.hotkey.press.desc": "상자를 클릭한 뒤 키 입력", "settings.hotkey.pause": "일시정지",
    "settings.hotkey.stop": "중지", "settings.hotkey.shot": "캡처", "settings.tray": "트레이로 닫기",
    "settings.hideTray": "트레이 아이콘 숨기기", "settings.hideTray.desc": "다시 실행하면 창이 나타납니다",
    "settings.bar": "녹화 도구 모음", "settings.bar.desc": "일시정지, 중지, 마이크",
    "settings.silent": "무음 모드", "settings.silent.desc": "시작·중지 시 창 없음",
    "sec.reset": "초기화", "settings.reset": "기본값 복원", "settings.reset.header": "기본값으로 재설정",
    "sec.about": "정보", "about.tagline": "로컬 화면 녹화", "about.privacy": "개인정보",
    "about.terms": "이용약관", "about.project": "프로젝트",
    "about.docs": "API 문서",
    "about.skill": "SKILL",
    "settings.reset.desc": "테마, 단축키, 소리, 도구 모음",
    "settings.reset.confirm": "테마, 단축키, 소리, 도구 모음을 초기값으로 되돌릴까요?",
    "common.ok": "확인", "common.cancel": "취소", "common.close": "닫기", "common.save": "저장",
    "sum.display": "화면 {0}", "sum.region": "영역 {0}×{1}", "sum.region.none": "영역 · 미선택",
    "sum.window.none": "창 · 미선택", "sum.window": "창 · {0}", "sum.game.none": "게임 · 미선택",
    "sum.game": "게임 · {0}", "sum.pick": "대상 선택", "sum.audio": "오디오만",
    "sum.sys.on": "시스템 켜짐", "sum.sys.off": "시스템 꺼짐", "sum.mic.on": "마이크 켜짐", "sum.mic.off": "마이크 꺼짐",
    "quality.sd": "720p", "quality.hd": "1080p", "quality.qhd": "1440p", "quality.uhd": "4K",
    "rec.need.target": "먼저 대상을 선택하세요.", "rec.need.region": "먼저 영역을 선택하세요.",
    "rec.processing": "처리 중", "rec.saved": "저장됨", "modes.aria": "캡처 모드",
    "lib.preview": "미리보기", "lib.rename": "이름 바꾸기", "lib.delete": "삭제", "lib.folder": "폴더 열기",
    "lib.more": "더보기", "lib.repair": "복구", "lib.merge": "병합", "lib.subtitle": "자막",
    "lib.music": "배경음", "lib.refresh": "새로고침", "lib.empty": "아직 영상이 없습니다",
    "lib.empty.desc": "녹화한 영상이 여기에 나타납니다", "lib.col.name": "이름", "lib.col.size": "크기",
    "lib.col.duration": "길이", "lib.col.date": "날짜", "lib.fullscreen": "전체 화면",
    "lib.trim": "자르기", "lib.compress": "압축", "lib.rename.invalid": "올바른 이름을 입력하세요",
    "lib.rename.fail": "이름을 바꿀 수 없습니다", "lib.delete.title": "파일 삭제",
    "lib.delete.one": "{0}을(를) 삭제할까요?", "lib.delete.many": "파일 {0}개를 삭제할까요?",
    "lib.compress.body": "새 파일로 내보냅니다.", "lib.compress.export": "새로 내보내기",
    "lib.trim.start": "시작(초)", "lib.trim.end": "끝(초)", "lib.export": "내보내기",
    "lib.repairing": "복구 중…", "lib.compressing": "압축 중…", "lib.trimming": "자르는 중…",
    "lib.repaired": "복구됨", "lib.compress.exported": "압축 내보냄",
    "lib.trim.exported": "자른 파일 내보냄", "lib.merge.need": "Ctrl로 파일을 두 개 이상 선택한 뒤 병합하세요.",
    "lib.merge.web": "병합은 PC에서 하세요.", "job.fail": "작업 실패.", "job.timeout": "작업이 끝나지 않음.",
    "api.bad": "응답을 해석할 수 없습니다.", "api.fail": "요청 실패."
  }
};

function uiLang() {
  return state.settings?.resolvedLanguage || "zh-Hans";
}

function t(key) {
  const pack = I18N[uiLang()] || I18N.en;
  return pack[key] || I18N.en[key] || key;
}

function tf(key, ...args) {
  return t(key).replace(/\{(\d+)\}/g, (_, i) => args[i] ?? "");
}

function applyChromeI18n() {
  document.documentElement.lang = uiLang();
  document.title = "Luma";
  document.querySelectorAll("[data-i18n]").forEach((el) => {
    el.textContent = t(el.dataset.i18n);
  });
  document.querySelectorAll("[data-i18n-aria]").forEach((el) => {
    el.setAttribute("aria-label", t(el.dataset.i18nAria));
  });
  document.querySelectorAll("[data-i18n-title]").forEach((el) => {
    el.title = t(el.dataset.i18nTitle);
    el.setAttribute("aria-label", t(el.dataset.i18nTitle));
  });
  const page = state.page || "record";
  if ($("pageLabel")) $("pageLabel").textContent = page === "library" && state.previewId ? t("page.preview") : t("page." + page);
  document.querySelectorAll("[data-go='library']").forEach((el) => {
    el.title = t("nav.library");
    el.setAttribute("aria-label", t("nav.library"));
  });
  document.querySelectorAll("[data-go='settings']").forEach((el) => {
    el.title = t("nav.settings");
    el.setAttribute("aria-label", t("nav.settings"));
  });
  if ($("backBtn")) {
    $("backBtn").title = t("nav.back");
    $("backBtn").setAttribute("aria-label", t("nav.back"));
  }
  document.querySelectorAll(".mode[data-mode]").forEach((btn) => {
    const label = btn.querySelector("span:last-child");
    if (label) label.textContent = t("mode." + btn.dataset.mode);
  });
  if ($("startBtn")) $("startBtn").textContent = t("home.start");
  if ($("pauseBtn")) $("pauseBtn").textContent = state.session === "paused" ? t("home.resume") : t("home.pause");
  if ($("stopBtn")) $("stopBtn").textContent = t("home.stop");
  document.querySelectorAll("label.toggle").forEach((label) => {
    const input = label.querySelector("input");
    if (!input) return;
    if (input.id === "homeSystem") label.childNodes[0].textContent = t("home.system") + " ";
    if (input.id === "homeMic") label.childNodes[0].textContent = t("home.mic") + " ";
  });
  if ($("homeQuality") && $("homeQuality").closest(".field")?.querySelector("span")) {
    $("homeQuality").closest(".field").querySelector("span").textContent = t("home.quality");
  }
  if (typeof updateIdleSummary === "function") updateIdleSummary();
  applyShotLabels();
}

function themeName(value) {
  return value === 1 || value === "Light" || value === "light" ? "light" : "dark";
}

const QUALITY = [
  { value: 0, short: "720p", long: "标清 720p", res: [1280, 720] },
  { value: 1, short: "1080p", long: "高清 1080p", res: [1920, 1080] },
  { value: 2, short: "1440p", long: "超清 1440p", res: [2560, 1440] },
  { value: 3, short: "4K", long: "蓝光 4K", res: [3840, 2160] }
];

function qualityLabel(level) {
  return t(["quality.sd", "quality.hd", "quality.qhd", "quality.uhd"][level] || "quality.hd");
}

function qualityLevel(value) {
  if (value === 0 || value === "0" || value === "Sd") return 0;
  if (value === 2 || value === "2" || value === "ExtraHd") return 2;
  if (value === 3 || value === "3" || value === "FourK") return 3;
  return 1;
}

function maxQualityLevel() {
  const display = (state.displays || []).find((item) => item.id === state.displayId) || (state.displays || [])[0];
  const size = parseSize(display?.title);
  const displayMax = Math.max(size.w, size.h);
  const displayMin = Math.min(size.w, size.h);
  let max = 0;
  QUALITY.forEach((item) => {
    const wide = Math.max(item.res[0], item.res[1]);
    const tall = Math.min(item.res[0], item.res[1]);
    if (wide <= displayMax && tall <= displayMin) max = item.value;
  });
  return max;
}

function fillQuality(select, selected, longName) {
  if (!select) return;
  const max = maxQualityLevel();
  let level = qualityLevel(selected);
  if (level > max) level = max;
  const items = QUALITY.filter((item) => item.value <= max);
  select.innerHTML = items.map((item) =>
    `<option value="${item.value}">${longName ? qualityLabel(item.value) : item.short}</option>`
  ).join("");
  select.value = String(level);
  if (select._syncCombo) select._syncCombo();
}

function syncQualityBoxes() {
  if ($("homeQuality")) fillQuality($("homeQuality"), $("homeQuality").value || state.settings?.quality?.level);
  if ($("sQ")) fillQuality($("sQ"), $("sQ").value || state.settings?.quality?.level, true);
}

function enhanceCombo(select) {
  if (!select || select._syncCombo) return;
  let box = select.closest(".combo-box");
  if (!box) {
    box = document.createElement("div");
    box.className = "combo-box";
    select.parentNode.insertBefore(box, select);
    box.append(select);
  }
  select.classList.add("combo-native");
  select.setAttribute("tabindex", "-1");
  select.setAttribute("aria-hidden", "true");
  let button = box.querySelector(".combo-btn");
  let list = box.querySelector(".combo-list");
  if (!button) {
    button = document.createElement("button");
    button.type = "button";
    button.className = "combo-btn";
    button.setAttribute("aria-haspopup", "listbox");
    box.append(button);
  }
  if (!list) {
    list = document.createElement("ul");
    list.className = "combo-list";
    list.hidden = true;
    list.setAttribute("role", "listbox");
    box.append(list);
  }
  const sync = () => {
    const opt = select.selectedOptions[0];
    button.textContent = opt ? opt.text : "";
    button.disabled = select.disabled;
    list.innerHTML = [...select.options].map((item) =>
      `<li role="option" data-value="${item.value}" class="${item.value === select.value ? "on" : ""}">${item.text}</li>`
    ).join("");
  };
  const close = () => {
    list.hidden = true;
    button.setAttribute("aria-expanded", "false");
  };
  button.onclick = (ev) => {
    ev.stopPropagation();
    const willOpen = list.hidden;
    document.querySelectorAll(".combo-list").forEach((el) => { el.hidden = true; });
    document.querySelectorAll(".combo-btn").forEach((el) => el.setAttribute("aria-expanded", "false"));
    list.hidden = !willOpen;
    button.setAttribute("aria-expanded", willOpen ? "true" : "false");
  };
  list.onclick = (ev) => {
    ev.stopPropagation();
    const item = ev.target.closest("[data-value]");
    if (!item) return;
    select.value = item.dataset.value;
    select.dispatchEvent(new Event("change", { bubbles: true }));
    sync();
    close();
  };
  if (!window._comboDocBound) {
    window._comboDocBound = true;
    document.addEventListener("click", () => {
      document.querySelectorAll(".combo-list").forEach((el) => { el.hidden = true; });
      document.querySelectorAll(".combo-btn").forEach((el) => el.setAttribute("aria-expanded", "false"));
    });
  }
  select._syncCombo = sync;
  sync();
}

function formatSize(bytes) {
  const n = Number(bytes);
  if (!Number.isFinite(n) || n <= 0) return "";
  if (n >= 1024 * 1024) return (n / (1024 * 1024)).toFixed(1) + " MB";
  return Math.max(1, Math.round(n / 1024)) + " KB";
}

function toggleControl(id, checked, locked) {
  return `<label class="toggle-ui"><input id="${id}" type="checkbox"${checked ? " checked" : ""}${locked ? " disabled" : ""}><i></i></label>`;
}

function sliderControl(id, min, max) {
  return `<input id="${id}" class="slider" type="range" min="${min}" max="${max}">`;
}

function syncSliderFill(el) {
  if (!el) return;
  const min = Number(el.min || 0);
  const max = Number(el.max || 100);
  const value = Number(el.value || min);
  el.style.setProperty("--pct", ((max === min ? 0 : (value - min) / (max - min)) * 100) + "%");
}

function toast(text) {
  const el = $("toast");
  el.hidden = false;
  el.textContent = text;
  clearTimeout(el._t);
  el._t = setTimeout(() => { el.hidden = true; }, 2400);
}

function setPage(page, previewId) {
  if (page === "library" && previewId) {
    location.hash = "#library/" + encodeURIComponent(previewId);
  } else {
    location.hash = "#" + page;
  }
}

function readHash() {
  const raw = (location.hash || "#record").slice(1);
  if (raw.startsWith("library/")) {
    return { page: "library", previewId: decodeURIComponent(raw.slice(8)) };
  }
  if (raw === "library" || raw === "settings" || raw === "record") {
    return { page: raw, previewId: null };
  }
  return { page: "record", previewId: null };
}

function showPage() {
  const { page, previewId } = readHash();
  if (state.page === "settings" && page !== "settings") {
    saveSettingsFromForm().catch((err) => toast(err.message));
  }
  state.page = page;
  state.previewId = previewId;
  $("recordPage").hidden = page !== "record";
  $("libraryPage").hidden = page !== "library";
  $("settingsPage").hidden = page !== "settings";
  $("pageLabel").textContent = t("page." + page);
  $("backBtn").hidden = page === "record";
  if ($("shell")) {
    $("shell").classList.toggle("library", page === "library");
    $("shell").classList.toggle("preview", page === "library" && !!previewId);
  }
  applyChromeI18n();
  if (page === "record") refreshSession();
  if (page === "library") loadLibrary();
  if (page === "settings") renderSettings();
}

function applyTheme() {
  document.documentElement.dataset.theme = themeName(state.settings?.theme);
}

async function loadSettings() {
  state.settings = await api("/settings");
  applyTheme();
  applyChromeI18n();
  $("homeSystem").checked = !!state.settings.audio?.captureSystem;
  $("homeMic").checked = !!state.settings.audio?.captureMicrophone;
  fillQuality($("homeQuality"), state.settings.quality?.level);
  enhanceCombo($("homeQuality"));
}

async function patch(body) {
  const prevLang = state.settings?.resolvedLanguage;
  state.settings = await api("/settings", { method: "PATCH", body: JSON.stringify(body) });
  applyTheme();
  applyChromeI18n();
  if (prevLang !== state.settings?.resolvedLanguage) {
    renderTargets();
    syncQualityBoxes();
    if (state.page === "settings") renderSettings();
  }
}

function currentAudio() {
  const a = { ...(state.settings?.audio || {}) };
  a.captureSystem = $("homeSystem").checked;
  a.captureMicrophone = $("homeMic").checked;
  return a;
}

function currentQuality() {
  const level = qualityLevel($("homeQuality").value);
  const q = { ...(state.settings?.quality || {}) };
  q.level = level;
  if (!q.frameRate) q.frameRate = 30;
  q.bitrateKbps = [4000, 8000, 16000, 35000][level] || 8000;
  return q;
}

async function saveHomeQuick() {
  await patch({ audio: currentAudio(), quality: currentQuality() });
  updateIdleSummary();
}

function selectMode(mode) {
  state.mode = mode;
  document.querySelectorAll(".mode").forEach((btn) => btn.classList.toggle("on", btn.dataset.mode === mode));
  renderTargets();
  updateIdleSummary();
}

function idleSummary() {
  const system = $("homeSystem").checked ? t("sum.sys.on") : t("sum.sys.off");
  const mic = $("homeMic").checked ? t("sum.mic.on") : t("sum.mic.off");
  let target = t("sum.pick");
  if (state.mode === "audio") {
    target = t("sum.audio");
  } else if (state.mode === "fullscreen") {
    const display = (state.displays || []).find((item) => item.id === state.displayId) || (state.displays || [])[0];
    if (display) {
      const size = String(display.title || "").replace("×", "x");
      const n = Number(display.id) + 1;
      target = tf("sum.display", `${Number.isFinite(n) && n > 0 ? n : 1}  ${size}`);
    } else {
      target = t("mode.fullscreen");
    }
  } else if (state.mode === "region") {
    target = state.region && state.region.width > 1 && state.region.height > 1
      ? tf("sum.region", state.region.width, state.region.height)
      : t("sum.region.none");
  } else if (state.mode === "window") {
    target = state.windowTitle ? tf("sum.window", state.windowTitle) : t("sum.window.none");
  } else if (state.mode === "game") {
    target = state.windowTitle ? tf("sum.game", state.windowTitle) : t("sum.game.none");
  }
  return target + "  ·  " + system + " · " + mic;
}

function updateIdleSummary() {
  if (state.session === "recording" || state.session === "paused") return;
  $("status").hidden = false;
  $("status").textContent = idleSummary();
}

function parseSize(title) {
  const m = String(title || "").match(/(\d+)\s*[×x]\s*(\d+)/i);
  return m ? { w: Number(m[1]), h: Number(m[2]) } : { w: 1920, h: 1080 };
}

function regionFromPreview(display, box, img) {
  const size = parseSize(display.title);
  const r = img.getBoundingClientRect();
  const x = Math.round((box.x / r.width) * size.w);
  const y = Math.round((box.y / r.height) * size.h);
  const width = Math.round((box.w / r.width) * size.w);
  const height = Math.round((box.h / r.height) * size.h);
  return { x, y, width, height };
}

function renderTargets() {
  const pane = $("targetPane");
  pane.innerHTML = "";
  if (state.mode === "fullscreen") {
    pane.append(cardList(state.displays, "display", (item) => { state.displayId = item.id; syncQualityBoxes(); updateIdleSummary(); }));
    return;
  }
  if (state.mode === "audio") {
    pane.innerHTML = `<p class="muted">${t("sum.audio")}</p>`;
    return;
  }
  if (state.mode === "region") {
    const display = state.displays.find((d) => d.id === state.displayId) || state.displays[0];
    if (!display) return;
    const stage = document.createElement("div");
    stage.className = "stage";
    stage.id = "regionStage";
    const img = document.createElement("img");
    img.alt = display.title || t("mode.fullscreen");
    img.draggable = false;
    if (display.preview) img.src = display.preview;
    const rubber = document.createElement("div");
    rubber.className = "rubber";
    rubber.hidden = true;
    stage.append(img, rubber);
    let drag = null;
    const pos = (ev) => {
      const r = img.getBoundingClientRect();
      return { x: Math.min(Math.max(ev.clientX - r.left, 0), r.width), y: Math.min(Math.max(ev.clientY - r.top, 0), r.height) };
    };
    stage.onpointerdown = (ev) => {
      if (ev.button !== 0) return;
      ev.preventDefault();
      const p = pos(ev);
      drag = { x: p.x, y: p.y };
      stage.setPointerCapture(ev.pointerId);
    };
    stage.onpointermove = (ev) => {
      if (!drag) return;
      const p = pos(ev);
      const box = { x: Math.min(drag.x, p.x), y: Math.min(drag.y, p.y), w: Math.abs(p.x - drag.x), h: Math.abs(p.y - drag.y) };
      rubber.hidden = false;
      rubber.style.left = box.x + "px";
      rubber.style.top = box.y + "px";
      rubber.style.width = box.w + "px";
      rubber.style.height = box.h + "px";
      if (img.clientWidth > 0) {
        state.region = regionFromPreview(display, box, img);
        updateIdleSummary();
      }
    };
    stage.onpointerup = () => { drag = null; };
    pane.append(stage);
    return;
  }
  const items = state.mode === "game" ? state.games : state.windows;
  const list = cardList(items, "window", (item) => { state.windowId = item.id; state.windowTitle = item.title; updateIdleSummary(); });
  pane.append(list);
  clipCardRows(list, 2);
}

function clipCardRows(wrap, rows) {
  if (!wrap) return;
  const apply = () => {
    wrap.style.maxHeight = "";
    const cards = [...wrap.children];
    if (cards.length === 0 || wrap.getBoundingClientRect().width < 8) return;
    const tops = cards.map((card) => Math.round(card.getBoundingClientRect().top));
    const rowTops = [...new Set(tops)].sort((a, b) => a - b).slice(0, rows);
    const visible = cards.filter((_, i) => rowTops.includes(tops[i]));
    const wrapBox = wrap.getBoundingClientRect();
    const bottom = Math.max(...visible.map((card) => card.getBoundingClientRect().bottom));
    wrap.style.maxHeight = Math.ceil(bottom - wrapBox.top + wrap.scrollTop) + "px";
  };
  requestAnimationFrame(apply);
  if (typeof ResizeObserver === "undefined") return;
  if (wrap._clipRo) wrap._clipRo.disconnect();
  const pane = wrap.parentElement;
  if (!pane) return;
  wrap._clipRo = new ResizeObserver(apply);
  wrap._clipRo.observe(pane);
}

function cardList(items, kind, onPick) {
  const wrap = document.createElement("div");
  wrap.className = kind === "window" ? "cards clip-2" : "cards";
  (items || []).forEach((item) => {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "card";
    btn.dataset.id = item.id;
    if ((kind === "display" && item.id === state.displayId) || (kind === "window" && item.id === state.windowId)) {
      btn.classList.add("on");
    }
    btn.innerHTML = (item.preview ? `<img src="${item.preview}" alt="">` : "<img alt=\"\">")
      + `<span>${escapeHtml(item.title || item.id)}</span>`
      + `<small>${escapeHtml(item.detail || "")}</small>`;
    btn.onclick = () => {
      wrap.querySelectorAll(".card").forEach((c) => c.classList.remove("on"));
      btn.classList.add("on");
      onPick(item);
    };
    wrap.append(btn);
  });
  return wrap;
}

function escapeHtml(value) {
  return String(value || "").replace(/[&<>"']/g, (ch) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[ch]));
}

function readyTarget() {
  if (state.mode === "audio" || state.mode === "fullscreen") return true;
  if (state.mode === "region") return state.region && state.region.width > 1 && state.region.height > 1;
  return !!state.windowId;
}

async function putTarget() {
  const body = { mode: state.mode, displayId: state.displayId };
  if (state.mode === "region") body.region = state.region;
  if (state.mode === "window" || state.mode === "game") {
    body.windowId = state.windowId;
    body.windowTitle = state.windowTitle;
  }
  await api("/target", { method: "PUT", body: JSON.stringify(body) });
}

function showLive(live) {
  $("startBtn").hidden = live;
  $("liveBtns").hidden = !live;
  $("elapsed").hidden = !live;
  $("recBadge").hidden = !live;
}

function renderSession(data) {
  const session = data?.session || {};
  state.session = session.state || "idle";
  const live = state.session === "recording" || state.session === "paused";
  showLive(live);
  $("elapsed").textContent = session.elapsed || "00:00:00";
  $("status").hidden = live;
  if (!live) $("status").textContent = idleSummary();
  $("pauseBtn").textContent = state.session === "paused" ? t("home.resume") : t("home.pause");
}

async function refreshSession() {
  try {
    const data = await api("/session");
    renderSession(data);
    return data;
  } catch (err) {
    $("status").textContent = err.message;
    return null;
  }
}

function hideSessionOverlay() {
  $("sessionOverlay").hidden = true;
  $("processingPanel").hidden = true;
  $("savedPanel").hidden = true;
}

function showProcessingOverlay() {
  $("sessionOverlay").hidden = false;
  $("processingPanel").hidden = false;
  $("savedPanel").hidden = true;
}

function showSavedOverlay(saved) {
  if (!saved?.name) {
    hideSessionOverlay();
    return;
  }
  state.lastSaved = saved;
  $("savedFile").textContent = saved.name;
  const warn = String(saved.warning || "").trim();
  $("savedWarning").textContent = warn;
  $("savedWarning").hidden = !warn;
  $("sessionOverlay").hidden = false;
  $("processingPanel").hidden = true;
  $("savedPanel").hidden = false;
}

async function stopRecording() {
  showProcessingOverlay();
  try {
    await api("/session/stop", { method: "POST" });
    const data = await refreshSession();
    showSavedOverlay(data?.session?.lastSaved);
  } catch (err) {
    hideSessionOverlay();
    toast(err.message);
  }
}

function cancelCountdown() {
  clearInterval(state.countTimer);
  state.countTimer = 0;
  $("countdown").hidden = true;
}

function startCountdown() {
  if (!readyTarget()) {
    $("recordError").hidden = false;
    $("recordError").textContent = state.mode === "region" ? t("rec.need.region") : t("rec.need.target");
    return;
  }
  $("recordError").hidden = true;
  let n = 3;
  $("countNum").textContent = String(n);
  $("countdown").hidden = false;
  state.countTimer = setInterval(async () => {
    n -= 1;
    if (n >= 1) {
      $("countNum").textContent = String(n);
      return;
    }
    cancelCountdown();
    hideSessionOverlay();
    try {
      await putTarget();
      await api("/session/start", { method: "POST" });
      await refreshSession();
    } catch (err) {
      $("recordError").hidden = false;
      $("recordError").textContent = err.message;
    }
  }, 1000);
}

async function loadTargets() {
  const [displays, windows, games, cameras, microphones] = await Promise.all([
    api("/targets/displays"),
    api("/targets/windows"),
    api("/targets/games"),
    api("/targets/cameras"),
    api("/targets/microphones")
  ]);
  state.displays = displays || [];
  state.windows = windows || [];
  state.games = games || [];
  state.cameras = cameras || [];
  state.microphones = microphones || [];
  if (!state.displayId && state.displays[0]) state.displayId = state.displays[0].id;
  renderTargets();
  syncQualityBoxes();
  updateIdleSummary();
}

function fillSelect(select, items, selected, placeholder, label) {
  if (!select) return;
  const list = items || [];
  const opts = [];
  if (placeholder != null) opts.push(`<option value="">${escapeHtml(placeholder)}</option>`);
  list.forEach((item) => {
    opts.push(`<option value="${escapeHtml(item.id)}">${escapeHtml(label ? label(item) : (item.title || item.id))}</option>`);
  });
  if (selected && !list.some((item) => String(item.id) === String(selected))) {
    opts.push(`<option value="${escapeHtml(selected)}">${escapeHtml(selected)}</option>`);
  }
  select.innerHTML = opts.join("");
  select.value = selected != null && selected !== "" ? String(selected) : "";
  if (select._syncCombo) select._syncCombo();
}

function monitorLabel(item) {
  const size = String(item.title || "").replace("×", "x");
  const n = Number(item.id) + 1;
  return tf("sum.display", `${Number.isFinite(n) && n > 0 ? n : 1}  ${size}`);
}

function syncDeviceEnabled() {
  if ($("sMicId")) {
    $("sMicId").disabled = !$("sMic")?.checked;
    if ($("sMicId")._syncCombo) $("sMicId")._syncCombo();
  }
  if ($("sCamId")) {
    $("sCamId").disabled = !$("sCam")?.checked;
    if ($("sCamId")._syncCombo) $("sCamId")._syncCombo();
  }
}

function parseDurationSeconds(value) {
  if (value == null || value === "") return null;
  if (typeof value === "number" && Number.isFinite(value)) return value;
  const text = String(value).trim();
  if (/^\d+(\.\d+)?$/.test(text)) return Number(text);
  const parts = text.split(":");
  if (parts.length < 2) return null;
  const sec = Number(parts.at(-1));
  const min = Number(parts.at(-2));
  const hour = parts.length > 2 ? Number(parts.at(-3)) : 0;
  if (![hour, min, sec].every(Number.isFinite)) return null;
  return hour * 3600 + min * 60 + sec;
}

function formatDuration(value) {
  const seconds = parseDurationSeconds(value);
  if (seconds == null || seconds < 0.5 || !Number.isFinite(seconds)) return "";
  const total = Math.max(1, Math.round(seconds));
  const mm = Math.floor(total / 60);
  const ss = total % 60;
  return String(mm).padStart(2, "0") + ":" + String(ss).padStart(2, "0");
}

function probeMediaDuration(id, isAudio) {
  return new Promise((resolve) => {
    const el = document.createElement(isAudio ? "audio" : "video");
    let settled = false;
    const done = (value) => {
      if (settled) return;
      settled = true;
      el.removeAttribute("src");
      el.load();
      resolve(value);
    };
    el.preload = "metadata";
    el.onloadedmetadata = () => done(Number.isFinite(el.duration) && el.duration >= 0.5 ? el.duration : null);
    el.onerror = () => done(null);
    window.setTimeout(() => done(null), 4000);
    el.src = "/media/" + encodeURIComponent(id);
  });
}

async function fillActualDurations() {
  const gen = (state.durationProbe = (state.durationProbe || 0) + 1);
  const rows = [...$("libraryRows").querySelectorAll(".row")];
  const workers = Math.min(3, rows.length);
  let index = 0;
  await Promise.all(Array.from({ length: workers }, async () => {
    while (index < rows.length && gen === state.durationProbe) {
      const row = rows[index++];
      const item = state.items.find((entry) => entry.id === row.dataset.id);
      const seconds = await probeMediaDuration(row.dataset.id, item?.isAudio);
      if (gen !== state.durationProbe || seconds == null) continue;
      const cell = row.querySelector("[data-duration]");
      if (cell) cell.textContent = formatDuration(seconds);
    }
  }));
}

function formatDate(value) {
  if (!value) return "";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return String(value);
  const p = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

function selectedItems() {
  const ids = new Set(state.selectedIds || []);
  return (state.items || []).filter((item) => ids.has(item.id));
}

function selectedItem() {
  const id = state.selectedId || (state.selectedIds || [])[(state.selectedIds || []).length - 1];
  return (state.items || []).find((item) => item.id === id) || selectedItems()[0] || null;
}

function paintLibrarySelection() {
  const ids = new Set(state.selectedIds || []);
  $("libraryRows").querySelectorAll(".row").forEach((row) => {
    const on = ids.has(row.dataset.id);
    row.classList.toggle("on", on);
    row.setAttribute("aria-selected", on ? "true" : "false");
  });
  updateLibraryCommands();
}

function setLibrarySelection(ids, anchor) {
  const known = new Set((state.items || []).map((item) => item.id));
  state.selectedIds = [...new Set(ids)].filter((id) => known.has(id));
  state.selectedId = state.selectedIds.includes(anchor)
    ? anchor
    : (state.selectedIds[state.selectedIds.length - 1] || null);
  state.selectAnchor = state.selectedId;
  paintLibrarySelection();
}

function toggleLibraryId(id) {
  const next = state.selectedIds.includes(id)
    ? state.selectedIds.filter((item) => item !== id)
    : state.selectedIds.concat(id);
  setLibrarySelection(next, id);
}

function rangeLibraryIds(toId) {
  const ids = (state.items || []).map((item) => item.id);
  const from = state.selectAnchor && ids.includes(state.selectAnchor) ? state.selectAnchor : toId;
  const a = ids.indexOf(from);
  const b = ids.indexOf(toId);
  if (a < 0 || b < 0) return [toId];
  const [start, end] = a < b ? [a, b] : [b, a];
  return ids.slice(start, end + 1);
}

function pickLibraryItem(id, ev) {
  if (ev.shiftKey) {
    setLibrarySelection(rangeLibraryIds(id), id);
    return;
  }
  toggleLibraryId(id);
}

function fileStem(name) {
  const text = String(name || "");
  const dot = text.lastIndexOf(".");
  return dot > 0 ? text.slice(0, dot) : text;
}

function updateLibraryCommands() {
  const count = (state.selectedIds || []).length;
  const hasOne = count >= 1;
  const hasMany = count >= 2;
  $("libPreview").disabled = !hasOne;
  $("libRename").disabled = count !== 1;
  $("libDelete").disabled = !hasOne;
  $("libRepair").disabled = !hasOne;
  $("libMerge").disabled = !hasMany;
  $("libSubtitle").disabled = true;
  $("libMusic").disabled = true;
  $("libRefresh").disabled = state.items.length === 0;
}

async function loadLibrary() {
  state.items = (await api("/library")) || [];
  const known = new Set(state.items.map((item) => item.id));
  state.selectedIds = (state.selectedIds || []).filter((id) => known.has(id));
  if (!state.selectedIds.includes(state.selectedId)) {
    state.selectedId = state.selectedIds[state.selectedIds.length - 1] || null;
  }
  if (!state.selectedIds.includes(state.selectAnchor)) {
    state.selectAnchor = state.selectedId;
  }
  const empty = state.items.length === 0;
  $("libraryEmpty").hidden = !empty;
  $("libraryTable").hidden = empty;
  const rows = $("libraryRows");
  rows.innerHTML = "";
  state.items.forEach((item) => {
    const on = state.selectedIds.includes(item.id);
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "row" + (on ? " on" : "");
    btn.dataset.id = item.id;
    btn.setAttribute("role", "option");
    btn.setAttribute("aria-selected", on ? "true" : "false");
    btn.innerHTML = `<span class="check" aria-hidden="true"></span>
      <div class="poster"><span class="icon poster-fall" aria-hidden="true">&#xE8B2;</span>${item.isAudio || !item.id ? "" : `<img src="/poster/${encodeURIComponent(item.id)}" alt="">`}</div>
      <strong class="name">${escapeHtml(item.name || item.id)}</strong>
      <span class="muted meta">${escapeHtml(formatSize(item.length))}</span>
      <span class="muted meta" data-duration>${formatDuration(item.duration)}</span>
      <span class="muted meta">${formatDate(item.date)}</span>`;
    btn.onclick = (ev) => {
      ev.preventDefault();
      if (ev.target.closest(".check")) {
        toggleLibraryId(item.id);
        return;
      }
      pickLibraryItem(item.id, ev);
    };
    btn.ondblclick = () => setPage("library", item.id);
    rows.append(btn);
  });
  updateLibraryCommands();
  fillActualDurations();
  if (state.previewId) openPreview(state.previewId);
  else {
    $("libraryViewer").hidden = true;
    $("libraryListPane").hidden = false;
    if ($("shell")) $("shell").classList.remove("preview");
  }
}

function openPreview(id) {
  const item = state.items.find((x) => x.id === id) || { id, name: id, isAudio: /\.(m4a|mp3|wav|aac)$/i.test(id) };
  setLibrarySelection(state.selectedIds.includes(id) ? state.selectedIds : [id], id);
  $("libraryListPane").hidden = true;
  $("libraryViewer").hidden = false;
  if ($("shell")) $("shell").classList.add("preview");
  $("previewTitle").textContent = item.name || id;
  const src = "/media/" + encodeURIComponent(id);
  $("previewPlayer").innerHTML = item.isAudio
    ? `<audio id="previewMedia" controls src="${src}"></audio>`
    : `<video id="previewMedia" controls src="${src}" playsinline></video>`;
}

function showDialog({ title, bodyHtml, primary, secondary, close, focus }) {
  if (close == null) close = t("common.cancel");
  return new Promise((resolve) => {
    const root = $("dialog");
    $("dialogTitle").textContent = title;
    $("dialogBody").innerHTML = bodyHtml || "";
    $("dialogPrimary").textContent = primary || "";
    $("dialogPrimary").hidden = !primary;
    $("dialogSecondary").textContent = secondary || "";
    $("dialogSecondary").hidden = !secondary;
    $("dialogClose").textContent = close;
    root.hidden = false;
    const finish = (result) => {
      const input = root.querySelector("input,textarea");
      const fields = {};
      root.querySelectorAll("[data-field]").forEach((el) => { fields[el.dataset.field] = el.value; });
      root.hidden = true;
      $("dialogPrimary").onclick = null;
      $("dialogSecondary").onclick = null;
      $("dialogClose").onclick = null;
      resolve({ result, value: input ? input.value : "", fields });
    };
    $("dialogPrimary").onclick = () => finish("primary");
    $("dialogSecondary").onclick = () => finish("secondary");
    $("dialogClose").onclick = () => finish("close");
    const el = focus && root.querySelector(focus);
    if (el) {
      el.focus();
      if (el.select) el.select();
    }
  });
}

function setMoreOpen(open) {
  $("libMoreMenu").hidden = !open;
  $("libMore").setAttribute("aria-expanded", open ? "true" : "false");
}

async function openFolderDialog() {
  if (!state.settings) await loadSettings();
  await showDialog({
    title: t("lib.folder"),
    bodyHtml: `<p class="path">${escapeHtml(state.settings?.saveFolder || "")}</p>`,
    close: t("common.ok")
  });
}

async function renameSelected() {
  const item = selectedItem();
  if (!item) return;
  const choice = await showDialog({
    title: t("lib.rename"),
    bodyHtml: `<input id="renameBox" type="text" value="${escapeHtml(fileStem(item.name))}">`,
    primary: t("common.save"),
    close: t("common.cancel"),
    focus: "#renameBox"
  });
  if (choice.result !== "primary") return;
  const name = String(choice.value || "").trim();
  if (!name) {
    toast(t("lib.rename.invalid"));
    return;
  }
  try {
    const updated = await api("/library/" + encodeURIComponent(item.id), { method: "PATCH", body: JSON.stringify({ name }) });
    setLibrarySelection([updated?.id || item.id], updated?.id || item.id);
    await loadLibrary();
  } catch (err) {
    toast(err.message || t("lib.rename.fail"));
  }
}

async function deleteSelected() {
  const items = selectedItems();
  if (items.length === 0) return;
  const content = items.length === 1 ? tf("lib.delete.one", items[0].name) : tf("lib.delete.many", items.length);
  const choice = await showDialog({
    title: t("lib.delete.title"),
    bodyHtml: `<p>${escapeHtml(content)}</p>`,
    primary: t("lib.delete"),
    close: t("common.cancel")
  });
  if (choice.result !== "primary") return;
  for (const item of items) {
    await api("/library/" + encodeURIComponent(item.id) + "?confirm=true", { method: "DELETE" });
  }
  setLibrarySelection([], null);
  await loadLibrary();
}

async function waitJob(id) {
  for (let i = 0; i < 80; i += 1) {
    const job = await api("/jobs/" + id);
    if (job.status === "done") return job;
    if (job.status === "error") throw new Error(job.error || t("job.fail"));
    await new Promise((r) => setTimeout(r, 250));
  }
  throw new Error(t("job.timeout"));
}

async function runJob(kind) {
  if (!state.selectedId) return;
  let body = null;
  if (kind === "compress") {
    const choice = await showDialog({
      title: t("lib.compress"),
      bodyHtml: `<p>${t("lib.compress.body")}</p>`,
      primary: t("lib.compress.export"),
      close: t("common.cancel")
    });
    if (choice.result !== "primary") return;
  }
  if (kind === "trim") {
    const media = $("previewMedia");
    const pos = media && Number.isFinite(media.currentTime) ? media.currentTime : 0;
    const choice = await showDialog({
      title: t("lib.trim"),
      bodyHtml: `<div class="dialog-fields">
        <label>${t("lib.trim.start")}<input data-field="start" type="number" min="0" step="0.1" value="${Math.max(0, pos)}"></label>
        <label>${t("lib.trim.end")}<input data-field="end" type="number" min="0" step="0.1" value="${Math.max(5, pos + 5)}"></label>
      </div>`,
      primary: t("lib.export"),
      close: t("common.cancel")
    });
    if (choice.result !== "primary") return;
    body = JSON.stringify({ startSeconds: Number(choice.fields.start || 0), endSeconds: Number(choice.fields.end || 0) });
  }
  try {
    toast(kind === "repair" ? t("lib.repairing") : kind === "compress" ? t("lib.compressing") : t("lib.trimming"));
    const data = await api("/library/" + encodeURIComponent(state.selectedId) + "/" + kind, { method: "POST", body });
    await waitJob(data.jobId);
    toast(kind === "repair" ? t("lib.repaired") : kind === "compress" ? t("lib.compress.exported") : t("lib.trim.exported"));
    await loadLibrary();
  } catch (err) {
    toast(err.message);
  }
}

function card(title, control, hint) {
  return `<div class="card-row"><div class="copy"><strong>${title}</strong>${hint ? `<span>${hint}</span>` : ""}</div><div class="action">${control}</div></div>`;
}

function wm(kind) {
  return (state.settings.overlay?.watermarks || []).find((w) => w.kind === kind || w.kind === String(kind));
}

function renderSettings() {
  const s = state.settings;
  if (!s) return;
  const ov = s.overlay || {};
  const au = s.audio || {};
  const q = s.quality || {};
  const hk = s.hotkeys || {};
  const auto = s.automation || {};
  const textWm = wm(0) || {};
  const imageWm = wm(1) || {};
  const timeOn = !!(wm(2));
  $("settingsHost").innerHTML = `
    <div class="h">${t("sec.appearance")}</div>
    ${card(t("settings.theme"), `<select id="sTheme"><option value="Light">${t("settings.theme.light")}</option><option value="Dark">${t("settings.theme.dark")}</option></select>`, t("settings.theme.desc"))}
    ${card(t("settings.language"), `<select id="sLang"><option value="system">${t("settings.lang.system")}</option><option value="en">${t("settings.lang.en")}</option><option value="zh-Hans">${t("settings.lang.zhHans")}</option><option value="zh-Hant">${t("settings.lang.zhHant")}</option><option value="ja">${t("settings.lang.ja")}</option><option value="ko">${t("settings.lang.ko")}</option></select>`, t("settings.language.desc"))}
    <div class="h">${t("sec.files")}</div>
    ${card(t("settings.save"), `<input id="sFolder" type="text">`)}
    <div class="h">${t("sec.audio")}</div>
    ${card(t("settings.audio.system"), toggleControl("sSys"))}
    ${card(t("settings.audio.mic"), toggleControl("sMic"))}
    ${card(t("settings.audio.micdev"), `<select id="sMicId"></select>`)}
    ${card(t("settings.audio.only"), toggleControl("sAudioOnly"), t("settings.audio.only.desc"))}
    <div class="h">${t("sec.quality")}</div>
    ${card(t("settings.quality"), `<select id="sQ"><option value="0">${t("quality.sd")}</option><option value="1">${t("quality.hd")}</option><option value="2">${t("quality.qhd")}</option><option value="3">${t("quality.uhd")}</option></select>`, t("settings.quality.desc"))}
    ${card(t("settings.fps"), `<select id="sFps"><option value="10">10 fps</option><option value="15">15 fps</option><option value="30">30 fps</option><option value="60">60 fps</option></select>`)}
    ${card(t("settings.hw"), toggleControl("sHw"))}
    ${card(t("settings.monitor"), `<select id="sMon"></select>`)}
    <div class="h">${t("sec.camera")}</div>
    ${card(t("settings.camera"), toggleControl("sCam"))}
    ${card(t("settings.camera.dev"), `<select id="sCamId"></select>`)}
    <p class="pip-title">${t("settings.pip")}</p>
    <p class="pip-hint">${t("settings.pip.hint")}</p>
    <div id="overlayStage" class="pip">
      <div id="wmBox" class="pip-box wm">${t("settings.overlay.mark")}</div>
      <div id="camBox" class="pip-box cam">${t("settings.overlay.camera")}</div>
    </div>
    ${card(t("settings.pos.x"), sliderControl("sCamX", 0, 100))}
    ${card(t("settings.pos.y"), sliderControl("sCamY", 0, 100))}
    ${card(t("settings.size.w"), sliderControl("sCamW", 10, 50))}
    ${card(t("settings.size.h"), sliderControl("sCamH", 10, 50))}
    <div class="h">${t("sec.watermark")}</div>
    ${card(t("settings.stamp"), toggleControl("sTime"))}
    ${card(t("settings.wm.text"), toggleControl("sTextOn"))}
    ${card(t("settings.wm.text.value"), `<input id="sText" type="text">`)}
    ${card(t("settings.wm.image.file"), `<input id="sImg" type="text">`)}
    ${card(t("settings.pos.x"), sliderControl("sMarkX", 0, 100))}
    ${card(t("settings.pos.y"), sliderControl("sMarkY", 0, 100))}
    ${card(t("settings.size.w"), sliderControl("sMarkW", 10, 50))}
    ${card(t("settings.size.h"), sliderControl("sMarkH", 10, 50))}
    <div class="h">${t("sec.auto")}</div>
    ${card(t("settings.seg"), toggleControl("sSeg"))}
    ${card(t("settings.seg.min"), `<input id="sSegMin" type="number" min="1">`)}
    <div class="h">${t("sec.system")}</div>
    ${card(t("settings.launchTray"), toggleControl("sLaunch"), t("settings.launchTray.desc"))}
    ${card(t("settings.tray"), toggleControl("sTray"))}
    ${card(t("settings.hideTray"), toggleControl("sHideTray"), t("settings.hideTray.desc"))}
    ${card(t("settings.bar"), toggleControl("sBar"), t("settings.bar.desc"))}
    ${card(t("settings.silent"), toggleControl("sSilent"), t("settings.silent.desc"))}
    <div class="h">${t("sec.hotkeys")}</div>
    ${card(t("settings.hotkey"), toggleControl("sHk"))}
    ${card(t("settings.hotkey.start"), `<input id="sHkStart" type="text" readonly>`, t("settings.hotkey.press.desc"))}
    ${card(t("settings.hotkey.pause"), `<input id="sHkPause" type="text" readonly>`)}
    ${card(t("settings.hotkey.stop"), `<input id="sHkStop" type="text" readonly>`)}
    ${card(t("settings.hotkey.shot"), `<input id="sHkShot" type="text" readonly>`)}
    <div class="h">${t("sec.reset")}</div>
    ${card(t("settings.reset.header"), `<button id="sReset" type="button">${t("settings.reset")}</button>`, t("settings.reset.desc"))}
    <div class="h">${t("sec.about")}</div>
    <details class="expander">
      <summary class="expander-head">
        <span class="icon" aria-hidden="true">&#xE946;</span>
        <div class="copy">
          <strong>Luma</strong>
          <span>${s.appVersion || "1.0.0"} · ${t("about.tagline")} · ${s.publisher || "flydmonkey"}</span>
        </div>
      </summary>
      <a class="expander-item" href="/legal/privacy" target="_blank" rel="noopener">${t("about.privacy")}</a>
      <a class="expander-item" href="/legal/terms" target="_blank" rel="noopener">${t("about.terms")}</a>
      <a class="expander-item" href="${s.websiteUrl || "https://www.github.com/flydmonkey/luma"}" target="_blank" rel="noopener">${t("about.project")}</a>
      <a class="expander-item" href="/api/docs" target="_blank" rel="noopener">${t("about.docs")}</a>
      <a class="expander-item" href="/skill" target="_blank" rel="noopener">${t("about.skill")}</a>
    </details>
  `;
  $("sTheme").value = themeName(s.theme) === "light" ? "Light" : "Dark";
  $("sLang").value = s.uiLanguage || "system";
  $("sFolder").value = s.saveFolder || "";
  $("sSys").checked = !!au.captureSystem;
  $("sMic").checked = !!au.captureMicrophone;
  $("sAudioOnly").checked = !!au.audioOnly;
  fillQuality($("sQ"), q.level, true);
  $("sFps").value = [10, 15, 30, 60].includes(q.frameRate) ? String(q.frameRate) : "30";
  $("sHw").checked = q.hardwareEncoding !== false;
  fillSelect($("sMon"), state.displays, String(s.monitorIndex ?? 0), null, monitorLabel);
  $("sCam").checked = !!ov.cameraEnabled;
  fillSelect($("sCamId"), state.cameras, ov.cameraDeviceId || "", t("settings.camera.dev.ph"));
  fillSelect($("sMicId"), state.microphones, au.microphoneDeviceId || "", t("settings.audio.micdev.ph"));
  ["sTheme", "sLang", "sQ", "sFps", "sMon", "sCamId", "sMicId"].forEach((id) => enhanceCombo($(id)));
  $("sCamX").value = String(Math.round((ov.cameraX ?? 0.5) * 100));
  $("sCamY").value = String(Math.round((ov.cameraY ?? 0.5) * 100));
  $("sCamW").value = String(Math.round((ov.cameraWidth ?? 0.24) * 100));
  $("sCamH").value = String(Math.round((ov.cameraHeight ?? 0.24) * 100));
  const mark = wm(0) || wm(1);
  $("sMarkX").value = String(Math.round((mark?.x ?? 0.02) * 100));
  $("sMarkY").value = String(Math.round((mark?.y ?? 0.10) * 100));
  $("sMarkW").value = String(Math.min(50, Math.max(10, Math.round((mark?.width > 0 ? mark.width : 0.16) * 100))));
  $("sMarkH").value = String(Math.min(50, Math.max(10, Math.round((mark?.height > 0 ? mark.height : 0.11) * 100))));
  syncDeviceEnabled();
  $("sCam").onchange = () => { syncDeviceEnabled(); placeOverlayBoxes(); saveSettingsFromForm().catch((err) => toast(err.message)); };
  $("sMic").onchange = () => { syncDeviceEnabled(); saveSettingsFromForm().catch((err) => toast(err.message)); };
  ["sCamX", "sCamY", "sCamW", "sCamH", "sMarkX", "sMarkY", "sMarkW", "sMarkH"].forEach((id) => {
    $(id).oninput = () => { syncSliderFill($(id)); placeOverlayBoxes(); };
    syncSliderFill($(id));
  });
  $("sTime").checked = timeOn;
  $("sTextOn").checked = !!(textWm.content || textWm.kind === 0);
  $("sText").value = (!textWm.content || textWm.content === "Record") ? "Luma" : textWm.content;
  $("sImg").value = imageWm.content || "";
  $("sSeg").checked = !!auto.segmentEnabled;
  $("sSegMin").value = String(auto.segmentMinutes || 10);
  $("sLaunch").checked = !!s.launchToTray;
  $("sHk").checked = hk.enabled !== false;
  $("sHkStart").value = hk.start || "";
  $("sHkPause").value = hk.pause || "";
  $("sHkStop").value = hk.stop || "";
  $("sHkShot").value = hk.screenshot || "";
  $("sTray").checked = s.closeToTray !== false;
  $("sHideTray").checked = !!s.hideTrayIcon;
  $("sBar").checked = s.showRecordingBar !== false;
  $("sSilent").checked = !!s.silentMode;
  placeOverlayBoxes();
  requestAnimationFrame(placeOverlayBoxes);
  $("settingsHost").onchange = () => {
    clearTimeout(state.settingsTimer);
    saveSettingsFromForm().catch((err) => toast(err.message));
  };
  $("settingsHost").oninput = queueSettingsSave;
  $("sReset").onclick = resetSettings;
  bindHotkey($("sHkStart"), "start");
  bindHotkey($("sHkPause"), "pause");
  bindHotkey($("sHkStop"), "stop");
  bindHotkey($("sHkShot"), "screenshot");
  bindOverlayDrag();
}

function overlayStageSize() {
  const stage = $("overlayStage");
  if (!stage) return { width: 448, height: 252 };
  const box = stage.getBoundingClientRect();
  return { width: box.width > 1 ? box.width : 448, height: box.height > 1 ? box.height : 252 };
}

function placeOverlayBoxes() {
  const cam = $("camBox");
  const wmBox = $("wmBox");
  if (!cam || !wmBox) return;
  const { width, height } = overlayStageSize();
  const x = Math.min(100, Math.max(0, Number($("sCamX")?.value ?? 50)));
  const y = Math.min(100, Math.max(0, Number($("sCamY")?.value ?? 50)));
  const w = Math.min(50, Math.max(10, Number($("sCamW")?.value ?? 24)));
  const h = Math.min(50, Math.max(10, Number($("sCamH")?.value ?? 24)));
  const camW = Math.max(48, w / 100 * width);
  const camH = Math.max(28, h / 100 * height);
  cam.style.width = camW + "px";
  cam.style.height = camH + "px";
  cam.style.left = (x / 100 * Math.max(0, width - camW)) + "px";
  cam.style.top = (y / 100 * Math.max(0, height - camH)) + "px";
  cam.style.opacity = $("sCam")?.checked ? "0.9" : "0.65";
  const mx = Math.min(100, Math.max(0, Number($("sMarkX")?.value ?? 2)));
  const my = Math.min(100, Math.max(0, Number($("sMarkY")?.value ?? 10)));
  const mw = Math.min(50, Math.max(10, Number($("sMarkW")?.value ?? 16)));
  const mh = Math.min(50, Math.max(10, Number($("sMarkH")?.value ?? 11)));
  const markW = Math.max(48, mw / 100 * width);
  const markH = Math.max(28, mh / 100 * height);
  wmBox.style.width = markW + "px";
  wmBox.style.height = markH + "px";
  wmBox.style.left = (mx / 100 * Math.max(0, width - markW)) + "px";
  wmBox.style.top = (my / 100 * Math.max(0, height - markH)) + "px";
  const markOn = $("sTextOn")?.checked || !!$("sImg")?.value.trim();
  wmBox.style.opacity = markOn ? "0.95" : "0.55";
}

function bindOverlayDrag() {
  const stage = $("overlayStage");
  if (!stage) return;
  let moving = null;
  stage.onpointerdown = (ev) => {
    const box = ev.target.closest(".pip-box");
    if (!box) return;
    moving = box.id;
    stage.setPointerCapture(ev.pointerId);
    ev.preventDefault();
  };
  stage.onpointermove = (ev) => {
    if (!moving) return;
    const { width, height } = overlayStageSize();
    const target = $(moving);
    const tw = target.offsetWidth;
    const th = target.offsetHeight;
    const maxX = Math.max(0, width - tw);
    const maxY = Math.max(0, height - th);
    const left = Math.min(maxX, Math.max(0, ev.clientX - stage.getBoundingClientRect().left - tw / 2));
    const top = Math.min(maxY, Math.max(0, ev.clientY - stage.getBoundingClientRect().top - th / 2));
    target.style.left = left + "px";
    target.style.top = top + "px";
    if (moving === "camBox") {
      $("sCamX").value = String(Math.round((maxX > 0 ? left / maxX : 0) * 100));
      $("sCamY").value = String(Math.round((maxY > 0 ? top / maxY : 0) * 100));
      syncSliderFill($("sCamX"));
      syncSliderFill($("sCamY"));
    } else {
      $("sMarkX").value = String(Math.round((maxX > 0 ? left / maxX : 0) * 100));
      $("sMarkY").value = String(Math.round((maxY > 0 ? top / maxY : 0) * 100));
      syncSliderFill($("sMarkX"));
      syncSliderFill($("sMarkY"));
    }
  };
  stage.onpointerup = async () => {
    if (moving) await saveSettingsFromForm().catch((err) => toast(err.message));
    moving = null;
  };
}

function bindHotkey(input, which) {
  input.onkeydown = async (ev) => {
    ev.preventDefault();
    const parts = [];
    if (ev.ctrlKey) parts.push("Ctrl");
    if (ev.altKey) parts.push("Alt");
    if (ev.shiftKey) parts.push("Shift");
    if (ev.key && !["Control", "Alt", "Shift", "Meta"].includes(ev.key)) parts.push(ev.key.length === 1 ? ev.key.toUpperCase() : ev.key);
    if (parts.length < 2) return;
    input.value = parts.join("+");
    await saveSettingsFromForm();
  };
}

function buildWatermarks() {
  const list = [];
  const x = Number($("sMarkX")?.value ?? 2) / 100;
  const y = Number($("sMarkY")?.value ?? 10) / 100;
  const w = Number($("sMarkW")?.value ?? 16) / 100;
  const h = Number($("sMarkH")?.value ?? 11) / 100;
  if ($("sTime").checked) list.push({ kind: 2, content: "", x: 0.02, y: 0.02, width: 0.2, height: 0.08, opacity: 1, color: "#FFFFFFFF" });
  if ($("sTextOn").checked) list.push({ kind: 0, content: $("sText").value, x, y, width: w, height: h, opacity: 1, color: "#FFFFFFFF" });
  if ($("sImg").value.trim()) list.push({ kind: 1, content: $("sImg").value.trim(), x, y, width: w, height: h, opacity: 0.9, color: "#FFFFFFFF" });
  return list;
}

function queueSettingsSave() {
  clearTimeout(state.settingsTimer);
  state.settingsTimer = setTimeout(() => {
    saveSettingsFromForm().catch((err) => toast(err.message));
  }, 280);
}

async function saveSettingsFromForm() {
  await patch({
    theme: $("sTheme").value,
    uiLanguage: $("sLang")?.value || "system",
    saveFolder: $("sFolder").value,
    monitorIndex: Number($("sMon").value || 0),
    launchToTray: $("sLaunch").checked,
    closeToTray: $("sTray").checked,
    hideTrayIcon: $("sHideTray").checked,
    showRecordingBar: $("sBar").checked,
    silentMode: $("sSilent").checked,
    audio: {
      ...currentAudio(),
      captureSystem: $("sSys").checked,
      captureMicrophone: $("sMic").checked,
      microphoneDeviceId: $("sMicId").value || null,
      audioOnly: $("sAudioOnly").checked
    },
    quality: { ...currentQuality(), level: qualityLevel($("sQ").value), frameRate: Number($("sFps").value), hardwareEncoding: $("sHw").checked },
    overlay: {
      cameraEnabled: $("sCam").checked,
      cameraDeviceId: $("sCamId").value || null,
      cameraX: Number($("sCamX").value || 50) / 100,
      cameraY: Number($("sCamY").value || 50) / 100,
      cameraWidth: Number($("sCamW").value || 24) / 100,
      cameraHeight: Number($("sCamH").value || 24) / 100,
      watermarks: buildWatermarks()
    },
    hotkeys: {
      enabled: $("sHk").checked,
      start: $("sHkStart").value,
      pause: $("sHkPause").value,
      stop: $("sHkStop").value,
      screenshot: $("sHkShot").value
    },
    automation: {
      startAtLogon: false,
      segmentEnabled: $("sSeg").checked,
      segmentMinutes: Number($("sSegMin").value || 10),
      segmentMaxMegabytes: state.settings.automation?.segmentMaxMegabytes || 0,
      schedules: []
    }
  });
}

async function resetSettings() {
  if (!confirm(t("settings.reset.confirm"))) return;
  await api("/settings/reset", { method: "POST" });
  await loadSettings();
  renderSettings();
}

const shot = {
  tool: "region",
  pen: "",
  color: 0xFFE81123,
  image: null,
  desktop: null,
  picked: false,
  monitors: [],
  windows: [],
  selection: null,
  drag: null,
  marks: [],
  draft: null,
  stroke: [],
  inking: false,
  requests: 0,
  errorKey: ""
};

const SHOT_PALETTE = [0xFFE81123, 0xFFFF8C00, 0xFFFFC83D, 0xFF10893E, 0xFF0078D4, 0xFF8764B8, 0xFFFFFFFF, 0xFF1A1A1A];

function applyShotLabels() {
  if (!$("shotSave")) return;
  $("shotCancel").textContent = t("shot.cancel");
  $("shotRegion").textContent = t("shot.region");
  $("shotWindow").textContent = t("shot.window");
  $("shotDisplay").textContent = t("shot.display");
  $("shotText").placeholder = t("shot.text.hint");
  if (shot.errorKey && !$("shotError").hidden) {
    $("shotError").textContent = t(shot.errorKey);
  }
}

function setShotTool(tool) {
  shot.tool = tool;
  shot.selection = null;
  clearShotInk();
  hideShotSelection();
  ["shotRegion", "shotWindow", "shotDisplay"].forEach((id) => {
    const name = id === "shotRegion" ? "region" : id === "shotWindow" ? "window" : "display";
    $(id).classList.toggle("on", name === tool);
  });
  const list = $("shotWindows");
  if (list) list.hidden = tool !== "window";
  if (tool === "window") renderShotWindows();
  else showShotDesktop();
}

function showShotDesktop() {
  shot.picked = false;
  const list = $("shotWindows");
  if (list) list.hidden = true;
  if (!shot.desktop) return;
  shot.image = shot.desktop;
  const canvas = $("shotCanvas");
  canvas.width = shot.desktop.naturalWidth;
  canvas.height = shot.desktop.naturalHeight;
  canvas.getContext("2d").drawImage(shot.desktop, 0, 0);
}

async function renderShotWindows() {
  const list = $("shotWindows");
  if (!list) return;
  shot.picked = false;
  list.replaceChildren();
  let items = state.windows || [];
  try {
    const fresh = await api("/targets/windows");
    if (shot.tool !== "window") return;
    items = fresh || [];
    state.windows = items;
  } catch {
    if (shot.tool !== "window") return;
  }
  if (!items.length) {
    const note = document.createElement("p");
    note.className = "muted";
    note.textContent = t("shot.window.empty");
    list.append(note);
    return;
  }
  items.forEach((item, index) => {
    const btn = document.createElement("button");
    btn.type = "button";
    btn.className = "card";
    btn.disabled = !item.preview;
    const img = document.createElement("img");
    img.alt = "";
    if (item.preview) img.src = item.preview;
    const title = document.createElement("span");
    title.textContent = item.title || item.id || "";
    const detail = document.createElement("small");
    detail.textContent = item.detail || "";
    btn.append(img, title, detail);
    btn.onclick = () => pickShotWindow(index);
    list.append(btn);
  });
}

async function pickShotWindow(index) {
  const requests = shot.requests;
  try {
    const data = await api("/screenshot?windowIndex=" + index);
    if (shot.requests !== requests || shot.tool !== "window") return;
    const image = new Image();
    image.src = "data:image/png;base64," + data.png;
    await image.decode();
    if (shot.requests !== requests || shot.tool !== "window") return;
    shot.image = image;
    shot.picked = true;
    const canvas = $("shotCanvas");
    canvas.width = image.naturalWidth;
    canvas.height = image.naturalHeight;
    canvas.getContext("2d").drawImage(image, 0, 0);
    $("shotWindows").hidden = true;
    showShotSelection({ x: 0, y: 0, width: image.naturalWidth, height: image.naturalHeight });
  } catch {
    showShotError("shot.load.fail");
  }
}

function shotFileName(date) {
  const p = (n) => String(n).padStart(2, "0");
  return "Luma-Shot-" + date.getFullYear() + p(date.getMonth() + 1) + p(date.getDate())
    + "-" + p(date.getHours()) + p(date.getMinutes()) + p(date.getSeconds()) + ".png";
}

function pointOnStill(event) {
  const canvas = $("shotCanvas");
  const rect = canvas.getBoundingClientRect();
  const x = Math.round((event.clientX - rect.left) * (canvas.width / rect.width));
  const y = Math.round((event.clientY - rect.top) * (canvas.height / rect.height));
  return {
    x: Math.max(0, Math.min(canvas.width, x)),
    y: Math.max(0, Math.min(canvas.height, y))
  };
}

function hitShotRect(point, items) {
  return (items || []).find((item) =>
    point.x >= item.x && point.y >= item.y && point.x < item.x + item.width && point.y < item.y + item.height) || null;
}

function hideShotSelection() {
  shot.selection = null;
  $("shotBox").hidden = true;
  $("shotInk").hidden = true;
  $("shotActions").hidden = true;
  hideShotText();
  hideShotPalette();
  hideShotOcr();
}

function placeShotBox(rect) {
  const canvas = $("shotCanvas");
  const stage = canvas.parentElement;
  const canvasRect = canvas.getBoundingClientRect();
  const stageRect = stage.getBoundingClientRect();
  const scaleX = canvasRect.width / canvas.width;
  const scaleY = canvasRect.height / canvas.height;
  const left = canvasRect.left - stageRect.left + rect.x * scaleX;
  const top = canvasRect.top - stageRect.top + rect.y * scaleY;
  const width = rect.width * scaleX;
  const height = rect.height * scaleY;
  const box = $("shotBox");
  box.style.left = left + "px";
  box.style.top = top + "px";
  box.style.width = width + "px";
  box.style.height = height + "px";
  box.hidden = false;
  return { left, top, width, height };
}

function shotDragRect(point) {
  return {
    x: Math.min(shot.drag.x, point.x),
    y: Math.min(shot.drag.y, point.y),
    width: Math.abs(point.x - shot.drag.x),
    height: Math.abs(point.y - shot.drag.y)
  };
}

function showShotSelection(rect) {
  clearShotInk();
  shot.selection = { x: rect.x, y: rect.y, width: rect.width, height: rect.height };
  const { left, top, width, height } = placeShotBox(rect);
  const ink = $("shotInk");
  ink.width = rect.width;
  ink.height = rect.height;
  ink.style.left = left + "px";
  ink.style.top = top + "px";
  ink.style.width = width + "px";
  ink.style.height = height + "px";
  ink.hidden = false;
  ink.style.pointerEvents = shot.pen ? "auto" : "none";
  const actions = $("shotActions");
  actions.style.left = left + "px";
  actions.style.top = (top + height + 8) + "px";
  actions.hidden = false;
}

function showShotError(key) {
  shot.errorKey = key;
  const error = $("shotError");
  error.textContent = t(key);
  error.hidden = false;
}

function closeShot() {
  shot.image = null;
  shot.desktop = null;
  shot.picked = false;
  shot.selection = null;
  shot.drag = null;
  shot.errorKey = "";
  clearShotInk();
  $("shotOverlay").hidden = true;
  $("shotError").hidden = true;
  hideShotSelection();
}

function cropBlob(source, sel) {
  flushShotText();
  const canvas = document.createElement("canvas");
  canvas.width = sel.width;
  canvas.height = sel.height;
  const ctx = canvas.getContext("2d");
  ctx.drawImage(source, sel.x, sel.y, sel.width, sel.height, 0, 0, sel.width, sel.height);
  drawShotMarks(ctx, shot.marks);
  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => (blob && blob.size ? resolve(blob) : reject(new Error("blob"))), "image/png");
  });
}

function downloadBlob(blob, name) {
  if (!blob || !blob.size) throw new Error("empty");
  const link = document.createElement("a");
  if (!("download" in link)) throw new Error("download");
  const url = URL.createObjectURL(blob);
  link.href = url;
  link.download = name;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function shotColorCss(argb) {
  return "#" + (argb >>> 0).toString(16).padStart(8, "0").slice(2);
}

function clearShotInk() {
  flushShotText();
  shot.pen = "";
  shot.marks = [];
  shot.draft = null;
  shot.stroke = [];
  shot.inking = false;
  hideShotPalette();
  hideShotOcr();
  updateShotPens();
  const ink = $("shotInk");
  if (ink) {
    ink.getContext("2d").clearRect(0, 0, ink.width, ink.height);
    ink.style.pointerEvents = "none";
  }
}

function updateShotPens() {
  const map = { shotPen: "pen", shotRect: "rect", shotEllipse: "ellipse", shotArrow: "arrow", shotTextPen: "text" };
  Object.entries(map).forEach(([id, name]) => {
    const button = $(id);
    if (button) button.classList.toggle("on", shot.pen === name);
  });
  const swatch = $("shotSwatch");
  if (swatch) swatch.style.background = shotColorCss(shot.color);
}

function setShotPen(pen) {
  flushShotText();
  shot.pen = shot.pen === pen ? "" : pen;
  updateShotPens();
  const ink = $("shotInk");
  if (ink) ink.style.pointerEvents = shot.pen ? "auto" : "none";
}

function hideShotPalette() {
  const palette = $("shotPalette");
  if (palette) palette.hidden = true;
}

function hideShotOcr() {
  const pop = $("shotOcrPop");
  if (pop) pop.hidden = true;
}

function hideShotText() {
  const input = $("shotText");
  if (!input) return;
  input.hidden = true;
  input.value = "";
}

function flushShotText() {
  const input = $("shotText");
  if (!input || input.hidden) return;
  const text = input.value.trim();
  if (text && shot.selection) {
    shot.marks.push({ kind: "text", x: Number(input.dataset.x), y: Number(input.dataset.y), text, color: shot.color });
    renderShotInk();
  }
  hideShotText();
}

function drawShotMarks(ctx, marks) {
  ctx.lineCap = "round";
  ctx.lineJoin = "round";
  marks.forEach((mark) => {
    ctx.strokeStyle = shotColorCss(mark.color);
    ctx.fillStyle = shotColorCss(mark.color);
    ctx.lineWidth = mark.kind === "pen" ? 5 : 3;
    if (mark.kind === "rect" || mark.kind === "ellipse") {
      const x = Math.min(mark.x1, mark.x2);
      const y = Math.min(mark.y1, mark.y2);
      const w = Math.abs(mark.x2 - mark.x1);
      const h = Math.abs(mark.y2 - mark.y1);
      ctx.beginPath();
      if (mark.kind === "ellipse") ctx.ellipse(x + w / 2, y + h / 2, Math.max(w / 2, 0.5), Math.max(h / 2, 0.5), 0, 0, Math.PI * 2);
      else ctx.rect(x, y, w, h);
      ctx.stroke();
    } else if (mark.kind === "arrow") {
      drawShotArrow(ctx, mark.x1, mark.y1, mark.x2, mark.y2);
    } else if (mark.kind === "pen" && mark.points) {
      ctx.beginPath();
      ctx.moveTo(mark.points[0], mark.points[1]);
      for (let i = 2; i < mark.points.length; i += 2) ctx.lineTo(mark.points[i], mark.points[i + 1]);
      if (mark.points.length < 4) {
        ctx.fillRect(mark.points[0] - 2, mark.points[1] - 2, 5, 5);
      } else {
        ctx.stroke();
      }
    } else if (mark.kind === "text" && mark.text) {
      ctx.font = "18px Segoe UI";
      ctx.fillText(mark.text, mark.x, mark.y + 18);
    }
  });
}

function drawShotArrow(ctx, x1, y1, x2, y2) {
  const angle = Math.atan2(y2 - y1, x2 - x1);
  const head = 12;
  ctx.beginPath();
  ctx.moveTo(x1, y1);
  ctx.lineTo(x2, y2);
  ctx.stroke();
  ctx.beginPath();
  ctx.moveTo(x2, y2);
  ctx.lineTo(x2 - head * Math.cos(angle - Math.PI / 7), y2 - head * Math.sin(angle - Math.PI / 7));
  ctx.lineTo(x2 - head * Math.cos(angle + Math.PI / 7), y2 - head * Math.sin(angle + Math.PI / 7));
  ctx.closePath();
  ctx.fill();
}

function renderShotInk() {
  const ink = $("shotInk");
  if (!ink || !shot.selection) return;
  const ctx = ink.getContext("2d");
  ctx.clearRect(0, 0, ink.width, ink.height);
  const marks = shot.draft ? shot.marks.concat([shot.draft]) : shot.marks;
  drawShotMarks(ctx, marks);
}

function pointOnInk(event) {
  const ink = $("shotInk");
  const rect = ink.getBoundingClientRect();
  const x = Math.round((event.clientX - rect.left) * (ink.width / rect.width));
  const y = Math.round((event.clientY - rect.top) * (ink.height / rect.height));
  return {
    x: Math.max(0, Math.min(ink.width - 1, x)),
    y: Math.max(0, Math.min(ink.height - 1, y))
  };
}

function onInkDown(event) {
  if (!shot.pen || !shot.selection) return;
  event.stopPropagation();
  const point = pointOnInk(event);
  if (shot.pen === "text") {
    flushShotText();
    const input = $("shotText");
    const stage = $("shotCanvas").parentElement.getBoundingClientRect();
    input.style.left = (event.clientX - stage.left) + "px";
    input.style.top = (event.clientY - stage.top) + "px";
    input.style.color = shotColorCss(shot.color);
    input.dataset.x = String(point.x);
    input.dataset.y = String(point.y);
    input.hidden = false;
    input.focus();
    return;
  }
  shot.inking = true;
  try { event.currentTarget.setPointerCapture(event.pointerId); } catch { /* drag still ends on pointerup */ }
  if (shot.pen === "pen") {
    shot.stroke = [point.x, point.y];
    shot.draft = { kind: "pen", color: shot.color, points: shot.stroke.slice() };
  } else {
    shot.anchor = point;
    shot.draft = { kind: shot.pen, color: shot.color, x1: point.x, y1: point.y, x2: point.x, y2: point.y };
  }
  renderShotInk();
}

function onInkMove(event) {
  if (!shot.inking) return;
  const point = pointOnInk(event);
  if (shot.pen === "pen") {
    const dx = point.x - shot.stroke[shot.stroke.length - 2];
    const dy = point.y - shot.stroke[shot.stroke.length - 1];
    if (dx * dx + dy * dy < 4) return;
    shot.stroke.push(point.x, point.y);
    shot.draft = { kind: "pen", color: shot.color, points: shot.stroke.slice() };
  } else if (shot.draft) {
    shot.draft = { kind: shot.pen, color: shot.color, x1: shot.anchor.x, y1: shot.anchor.y, x2: point.x, y2: point.y };
  }
  renderShotInk();
}

function onInkUp() {
  if (!shot.inking) return;
  shot.inking = false;
  if (shot.pen === "pen" && shot.stroke.length >= 2) {
    shot.marks.push({ kind: "pen", color: shot.color, points: shot.stroke.slice() });
  } else if (shot.draft && (Math.abs(shot.draft.x2 - shot.draft.x1) >= 4 || Math.abs(shot.draft.y2 - shot.draft.y1) >= 4)) {
    shot.marks.push(shot.draft);
  }
  shot.draft = null;
  shot.stroke = [];
  renderShotInk();
}

function buildShotPalette() {
  const palette = $("shotPalette");
  palette.replaceChildren();
  SHOT_PALETTE.forEach((argb) => {
    const button = document.createElement("button");
    button.type = "button";
    const dot = document.createElement("i");
    dot.style.background = shotColorCss(argb);
    button.appendChild(dot);
    button.onclick = (event) => {
      event.stopPropagation();
      shot.color = argb;
      updateShotPens();
      hideShotPalette();
    };
    palette.appendChild(button);
  });
}

async function recognizeShot() {
  if (!shot.selection || !shot.image) return;
  hideShotPalette();
  $("shotOcr").disabled = true;
  try {
    const blob = await cropBlob(shot.image, shot.selection);
    const png = await blobToBase64(blob);
    const data = await api("/screenshot/ocr", { method: "POST", body: JSON.stringify({ png }) });
    const pop = $("shotOcrPop");
    pop.replaceChildren();
    const note = document.createElement("p");
    if (data.text == null) note.textContent = t("shot.ocr.unavailable");
    else if (!String(data.text).trim()) note.textContent = t("shot.ocr.empty");
    else {
      note.textContent = t("shot.ocr.copied");
      const box = document.createElement("textarea");
      box.readOnly = true;
      box.value = data.text;
      pop.appendChild(note);
      pop.appendChild(box);
      pop.hidden = false;
      try { await navigator.clipboard.writeText(data.text); } catch { /* the text stays in the panel */ }
      return;
    }
    pop.appendChild(note);
    pop.hidden = false;
  } catch {
    showShotError("shot.ocr.fail");
  } finally {
    $("shotOcr").disabled = false;
  }
}

function blobToBase64(blob) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result).split(",")[1] || "");
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(blob);
  });
}

async function openShot() {
  const mode = state.mode;
  $("shotError").hidden = true;
  try {
    shot.requests += 1;
    const data = await api("/screenshot");
    if (state.mode !== mode) selectMode(mode);
    const image = new Image();
    image.src = "data:image/png;base64," + data.png;
    await image.decode();
    shot.image = image;
    shot.desktop = image;
    shot.monitors = data.monitors || [];
    shot.windows = data.windows || [];
    const canvas = $("shotCanvas");
    canvas.width = image.naturalWidth;
    canvas.height = image.naturalHeight;
    canvas.getContext("2d").drawImage(image, 0, 0);
    setShotTool("region");
    $("shotOverlay").hidden = false;
    applyShotLabels();
    const ink = $("shotInk");
    ink.onpointerdown = onInkDown;
    ink.onpointermove = onInkMove;
    ink.onpointerup = onInkUp;
    canvas.onpointerdown = onShotPointerDown;
    canvas.onpointermove = onShotPointerMove;
    canvas.onpointerup = onShotPointerUp;
  } catch (error) {
    toast(error.message || t("shot.load.fail"));
  }
}

function onShotPointerDown(event) {
  if ($("shotOverlay").hidden) return;
  const point = pointOnStill(event);
  if (shot.tool === "window") {
    if (shot.picked) return;
    const hit = hitShotRect(point, shot.windows);
    if (!hit) hideShotSelection();
    else showShotSelection(hit);
    return;
  }
  if (shot.tool === "display") {
    const hit = hitShotRect(point, shot.monitors);
    if (!hit) hideShotSelection();
    else showShotSelection(hit);
    return;
  }
  clearShotInk();
  hideShotSelection();
  shot.drag = point;
  placeShotBox({ x: point.x, y: point.y, width: 0, height: 0 });
  try {
    event.currentTarget.setPointerCapture(event.pointerId);
  } catch {
    // pointer capture is optional; the drag still ends on pointerup
  }
}

function onShotPointerMove(event) {
  if (shot.tool !== "region" || !shot.drag) return;
  placeShotBox(shotDragRect(pointOnStill(event)));
  $("shotActions").hidden = true;
  $("shotInk").hidden = true;
}

function onShotPointerUp(event) {
  if (shot.tool !== "region" || !shot.drag) return;
  const rect = shotDragRect(pointOnStill(event));
  shot.drag = null;
  if (rect.width < 8 || rect.height < 8) {
    closeShot();
    return;
  }
  showShotSelection(rect);
}

async function saveShot() {
  if (!shot.selection || !shot.image) return;
  const requests = shot.requests;
  try {
    const blob = await cropBlob(shot.image, shot.selection);
    downloadBlob(blob, shotFileName(new Date()));
    if (shot.requests !== requests) throw new Error("request");
    closeShot();
  } catch {
    showShotError("shot.save.fail");
  }
}

async function copyShot() {
  if (!shot.selection || !shot.image) return;
  const requests = shot.requests;
  try {
    const blob = await cropBlob(shot.image, shot.selection);
    if (!navigator.clipboard || typeof ClipboardItem === "undefined") throw new Error("clipboard");
    await navigator.clipboard.write([new ClipboardItem({ "image/png": Promise.resolve(blob) })]);
    if (shot.requests !== requests) throw new Error("request");
    closeShot();
  } catch {
    showShotError("shot.copy.fail");
  }
}

async function boot() {
  document.querySelectorAll("[data-go]").forEach((btn) => btn.onclick = () => setPage(btn.dataset.go));
  document.querySelectorAll(".mode[data-mode]").forEach((btn) => btn.onclick = () => selectMode(btn.dataset.mode));
  $("shotBtn").onclick = () => openShot();
  $("shotRegion").onclick = () => setShotTool("region");
  $("shotWindow").onclick = () => setShotTool("window");
  $("shotDisplay").onclick = () => setShotTool("display");
  $("shotCancel").onclick = () => closeShot();
  $("shotPen").onclick = () => setShotPen("pen");
  $("shotRect").onclick = () => setShotPen("rect");
  $("shotEllipse").onclick = () => setShotPen("ellipse");
  $("shotArrow").onclick = () => setShotPen("arrow");
  $("shotTextPen").onclick = () => setShotPen("text");
  $("shotColor").onclick = (event) => {
    event.stopPropagation();
    $("shotPalette").hidden = !$("shotPalette").hidden;
  };
  $("shotOcr").onclick = () => recognizeShot();
  $("shotSave").onclick = () => saveShot();
  $("shotCopy").onclick = () => copyShot();
  $("shotText").addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      event.preventDefault();
      flushShotText();
    }
  });
  buildShotPalette();
  document.addEventListener("keydown", (event) => {
    if (event.key !== "Escape" || !$("shotOverlay") || $("shotOverlay").hidden) return;
    event.preventDefault();
    if (!$("shotText").hidden) {
      hideShotText();
      return;
    }
    if (!$("shotOcrPop").hidden) {
      hideShotOcr();
      return;
    }
    closeShot();
  });
  $("homeSystem").onchange = saveHomeQuick;
  $("homeMic").onchange = saveHomeQuick;
  $("homeQuality").onchange = saveHomeQuick;
  $("startBtn").onclick = startCountdown;
  $("countCancel").onclick = cancelCountdown;
  $("pauseBtn").onclick = () => api("/session/pause", { method: "POST" }).then(refreshSession).catch((e) => toast(e.message));
  $("stopBtn").onclick = stopRecording;
  $("savedClose").onclick = hideSessionOverlay;
  $("savedPreview").onclick = () => {
    const name = state.lastSaved?.name;
    hideSessionOverlay();
    if (name) setPage("library", name);
  };
  $("backBtn").onclick = () => state.previewId ? setPage("library") : setPage("record");
  $("emptyRecord").onclick = () => setPage("record");
  $("emptyFolder").onclick = openFolderDialog;
  $("libFolder").onclick = openFolderDialog;
  $("libPreview").onclick = () => selectedItem() && setPage("library", selectedItem().id);
  $("libRename").onclick = renameSelected;
  $("libDelete").onclick = deleteSelected;
  $("libRepair").onclick = () => { setMoreOpen(false); runJob("repair"); };
  $("libMerge").onclick = () => {
    setMoreOpen(false);
    if ((state.selectedIds || []).length < 2) {
      toast(t("lib.merge.need"));
      return;
    }
    toast(t("lib.merge.web"));
  };
  $("libRefresh").onclick = () => { setMoreOpen(false); loadLibrary(); };
  $("libMore").onclick = (ev) => {
    ev.stopPropagation();
    setMoreOpen($("libMoreMenu").hidden);
  };
  document.addEventListener("click", () => setMoreOpen(false));
  $("previewFull").onclick = () => {
    const media = $("previewMedia");
    if (media && media.requestFullscreen) media.requestFullscreen();
  };
  $("previewTrim").onclick = () => runJob("trim");
  $("previewCompress").onclick = () => runJob("compress");
  document.addEventListener("keydown", (ev) => {
    if (state.page !== "library" || state.previewId) return;
    if (ev.key === "F2") {
      ev.preventDefault();
      renameSelected();
      return;
    }
    if ((ev.ctrlKey || ev.metaKey) && ev.key.toLowerCase() === "a") {
      ev.preventDefault();
      const ids = (state.items || []).map((item) => item.id);
      setLibrarySelection(ids, ids[ids.length - 1] || null);
    }
  });
  window.addEventListener("hashchange", showPage);
  window.addEventListener("pagehide", () => {
    if (state.page === "settings") saveSettingsFromForm();
  });
  await loadSettings();
  await loadTargets();
  const audioOnly = !!state.settings?.audio?.audioOnly;
  const last = Number(state.settings?.lastMode);
  selectMode(audioOnly ? "audio" : last === 1 ? "region" : last === 2 ? "window" : "fullscreen");
  showPage();
  state.poll = setInterval(() => { if (state.page === "record") refreshSession(); }, 1000);
}

boot().catch((err) => { $("status").textContent = err.message; });
