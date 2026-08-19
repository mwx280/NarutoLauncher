<script setup lang="ts">
import { ref, watch } from 'vue'
import TitleBar from './components/TitleBar.vue'
import AccountSide from './components/AccountSide.vue'
import WindowStage from './components/WindowStage.vue'
import AccountModal from './components/AccountModal.vue'
import { useAccounts, type Account } from './composables/useAccounts'

const { startAll, onlineCount, totalCount } = useAccounts()

const modalVisible = ref(false)
const modalRef = ref<InstanceType<typeof AccountModal> | null>(null)

function openAdd() {
  modalRef.value?.openAdd()
  modalVisible.value = true
}
function openEdit(id: number) {
  modalRef.value?.openEdit(id)
  modalVisible.value = true
}
function closeModal() {
  modalVisible.value = false
}

// toast 提示
const toasts = ref<{ id: number; msg: string }[]>([])
let toastId = 0
function toast(msg: string) {
  const id = ++toastId
  toasts.value.push({ id, msg })
  setTimeout(() => {
    toasts.value = toasts.value.filter(t => t.id !== id)
  }, 2400)
}

watch(() => onlineCount.value, (v) => {
  if (v > 0) toast(`当前 ${v} 个窗口在线`)
})
</script>

<template>
  <div class="app-root">
    <!-- 背景纸纹 -->
    <div class="bg-paper"></div>

    <!-- 无边框窗口控制条 -->
    <TitleBar />

    <div class="app">
      <!-- 顶栏 -->
      <div class="topbar">
        <div class="brand">
          <div class="brand-seal">🍥</div>
          <div>
            <h1>火影忍者<span>OL</span> · 卷轴台</h1>
            <p>NARUTO · 多账号多开 · 一键切换</p>
          </div>
        </div>
        <span class="spacer"></span>
        <div class="stat">
          <span>在线 <b>{{ onlineCount }}</b></span>
          <span>账号 <b>{{ totalCount }}</b></span>
        </div>
        <button class="top-btn outline" @click="openAdd">＋ 添加账号</button>
        <button class="top-btn fire" @click="startAll(); toast('全部账号已上线')">⚔ 全部开战</button>
      </div>

      <div class="body">
        <!-- 左侧账号栏 -->
        <AccountSide @add="openAdd" @edit="openEdit" />

        <!-- 右侧多窗口舞台 -->
        <WindowStage />
      </div>
    </div>

    <!-- 添加/编辑账号弹窗 -->
    <AccountModal ref="modalRef" :show="modalVisible" @close="closeModal" />

    <!-- toast -->
    <div class="toasts">
      <TransitionGroup name="toast">
        <div v-for="t in toasts" :key="t.id" class="toast">{{ t.msg }}</div>
      </TransitionGroup>
    </div>
  </div>
</template>

<style>
/* 全局主题变量（和风纸纹） */
:root {
  --ink: #2b2118;
  --sub: #8a7a66;
  --paper: #fdf6ea;
  --card: #fffdf7;
  --line: #ecdcc3;
  --fire: #e8482c;
  --fire2: #f07a1f;
  --green: #2a9d5f;
}

* { margin: 0; padding: 0; box-sizing: border-box; }
html, body, #app { height: 100%; }
body {
  font-family: "Segoe UI", "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", sans-serif;
  background: var(--paper); color: var(--ink);
  overflow: hidden;
  -webkit-user-select: none; user-select: none;
}

.app-root { position: relative; height: 100%; display: flex; flex-direction: column; }
.bg-paper {
  position: fixed; inset: 0; z-index: 0; pointer-events: none;
  background-image: radial-gradient(rgba(232, 72, 44, 0.05) 1px, transparent 1px);
  background-size: 22px 22px;
}
.bg-paper::before {
  content: ""; position: absolute; inset: 0;
  background: linear-gradient(180deg, rgba(240, 122, 31, 0.06), transparent 30%),
              radial-gradient(900px 400px at 50% 0%, rgba(232, 72, 44, 0.1), transparent 60%);
}

.app { position: relative; z-index: 2; flex: 1; display: flex; flex-direction: column; min-height: 0; }

/* 顶栏 */
.topbar { display: flex; align-items: center; gap: 14px; padding: 14px 24px 12px; }
.brand { display: flex; align-items: center; gap: 12px; }
.brand-seal {
  width: 42px; height: 42px; border-radius: 50%;
  background: radial-gradient(circle at 35% 30%, var(--fire2), var(--fire));
  display: grid; place-items: center; color: #fff; font-size: 20px;
  box-shadow: 0 8px 20px rgba(232, 72, 44, 0.35), inset 0 0 0 2px rgba(255, 255, 255, 0.25);
}
.brand h1 { font-size: 18px; font-weight: 800; letter-spacing: 0.5px; }
.brand h1 span { color: var(--fire); }
.brand p { font-size: 10px; color: var(--sub); letter-spacing: 2px; }
.spacer { flex: 1; }
.stat { font-size: 12px; color: var(--sub); display: flex; align-items: center; gap: 14px; }
.stat b { color: var(--fire); font-size: 14px; }
.top-btn {
  border: none; cursor: pointer; font-family: inherit; font-weight: 700;
  border-radius: 999px; padding: 9px 18px; font-size: 13px; transition: all 0.2s;
}
.top-btn.fire { background: linear-gradient(90deg, var(--fire), var(--fire2)); color: #fff; box-shadow: 0 8px 20px rgba(232, 72, 44, 0.32); }
.top-btn.fire:hover { transform: translateY(-2px); box-shadow: 0 12px 26px rgba(232, 72, 44, 0.42); }
.top-btn.outline { background: transparent; color: var(--ink); border: 1.5px solid var(--fire); }
.top-btn.outline:hover { background: rgba(232, 72, 44, 0.08); }

.body { flex: 1; display: flex; min-height: 0; padding: 0 20px 16px; gap: 16px; }

/* toast */
.toasts {
  position: fixed; bottom: 26px; left: 50%; transform: translateX(-50%);
  z-index: 100; display: flex; flex-direction: column; gap: 8px; align-items: center;
}
.toast {
  padding: 12px 22px; border-radius: 999px; background: var(--ink);
  color: #fdf6ea; font-size: 13px; box-shadow: 0 14px 34px rgba(0, 0, 0, 0.3);
}
.toast-enter-active, .toast-leave-active { transition: all 0.35s; }
.toast-enter-from, .toast-leave-to { opacity: 0; transform: translateY(16px); }
</style>
