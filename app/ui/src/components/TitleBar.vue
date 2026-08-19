<script setup lang="ts">
// 无边框窗口控制条（CEF 宿主拖拽区）
// 实装后按钮通过 CEF 桥调用窗口控制；预览阶段 toast 提示。
function onWin(action: string) {
  // TODO: 接 CEF 桥后调用 window.naruto.win(action)
  console.log('[win]', action)
}
</script>

<template>
  <div class="frameless-bar">
    <div class="fb-title">
      <img src="/assets/favicon.png" alt="logo">
      火影忍者OL · 多窗口卷轴台
    </div>
    <div class="fb-btns">
      <button class="fb-btn" @click="onWin('min')">—</button>
      <button class="fb-btn" @click="onWin('max')">□</button>
      <button class="fb-btn" @click="onWin('full')">⤢</button>
      <button class="fb-btn close" @click="onWin('close')">✕</button>
    </div>
  </div>
</template>

<style scoped>
.frameless-bar {
  position: relative; z-index: 20; height: 36px;
  display: flex; align-items: center; justify-content: flex-end;
  -webkit-app-region: drag;
  border-bottom: 1px solid rgba(232, 72, 44, 0.08);
  background: rgba(253, 246, 234, 0.65);
  backdrop-filter: blur(8px);
}
.fb-title {
  position: absolute; left: 16px; font-size: 11px; color: var(--sub, #8a7a66);
  letter-spacing: 2px; display: flex; align-items: center; gap: 8px;
}
.fb-title img { width: 16px; height: 16px; opacity: 0.7; }
.fb-btns { display: flex; -webkit-app-region: no-drag; gap: 2px; padding-right: 6px; }
.fb-btn {
  width: 40px; height: 28px; border: none; background: transparent;
  color: #8a7a66; font-size: 13px; border-radius: 8px; cursor: pointer;
  font-family: inherit; transition: all 0.15s;
}
.fb-btn:hover { background: rgba(232, 72, 44, 0.12); color: #2b2118; }
.fb-btn.close:hover { background: #e8482c; color: #fff; }
</style>
