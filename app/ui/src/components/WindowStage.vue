<script setup lang="ts">
import { computed } from 'vue'
import { useAccounts } from '../composables/useAccounts'
import type { Account } from '../composables/useAccounts'

const { accounts, currentId, displayName, stopAccount, switchTo } = useAccounts()

const running = computed(() => accounts.filter(a => a.run))

const ACCENTS = [
  ['#e8482c', '#f07a1f'], ['#8b5cf6', '#6d28d9'], ['#2a9d5f', '#35c17a'],
  ['#eab308', '#f97316'], ['#0ea5e9', '#2563eb'], ['#ec4899', '#8b5cf6'],
]
const BANDS = ['#5b8dd6', '#f43f5e', '#10b981', '#8b5cf6', '#f59e0b', '#06b6d4']
function colors(a: Account) { return ACCENTS[a.seed % ACCENTS.length] }
function band(a: Account) { return BANDS[a.seed % BANDS.length] }
</script>

<template>
  <main class="stage">
    <!-- 窗口标签栏 -->
    <div class="tabs">
      <span v-if="!running.length" class="tabs-empty">还没有运行中的窗口 · 从左侧账号进入</span>
      <template v-else>
        <div
          v-for="a in running"
          :key="a.id"
          class="wtab"
          :class="{ active: currentId === a.id }"
          @click="switchTo(a.id)"
        >
          <span class="dot"></span>{{ displayName(a) }}
          <button class="x" @click.stop="stopAccount(a.id)">✕</button>
        </div>
      </template>
    </div>

    <!-- 舞台 -->
    <div class="stage-body">
      <!-- 空状态 -->
      <div v-if="!running.length" class="empty-stage">
        <div class="big">🪟</div>
        <h3>多窗口战场</h3>
        <p>每个运行中的账号对应一个独立 Flash 游戏窗口</p>
        <p class="tip">点击左侧账号卡片的「进入」开始游戏 · 顶部分页可快速切换</p>
      </div>

      <!-- 场景 -->
      <template v-for="a in running" :key="a.id">
        <div v-if="currentId === a.id" class="scene live">
          <div class="scene-bar">
            <span class="gname"><span class="d"></span>{{ displayName(a) }}</span>
            <span class="gmeta">{{ a.server || '区服未知' }}</span>
            <span class="gmeta">Lv.{{ a.loggedIn ? a.lv : '-' }}</span>
            <div class="scene-ctl">
              <button class="stop" @click="stopAccount(a.id)">✕ 关闭窗口</button>
            </div>
          </div>
          <!-- 模拟游戏画面（实装为 Flash 窗口嵌入） -->
          <div class="gscene" :style="{ '--gjc': colors(a)[0], '--gband': band(a) }">
            <div class="gsun"></div>
            <div class="gcloud c1"></div><div class="gcloud c2"></div>
            <div class="gmtn m1"></div><div class="gmtn m2"></div>
            <div class="gground"></div>
            <div class="gchar">
              <div class="gc-head"><i class="gc-band"></i></div>
              <div class="gc-body"></div>
              <div class="gc-leg l"></div><div class="gc-leg r"></div>
              <div class="gc-aura"></div>
            </div>
            <div class="gstatus">
              <div class="gs-hp"><i :style="{ width: (80 + (a.lv % 15)) + '%' }"></i></div>
              <div class="gs-mp"><i :style="{ width: (55 + (a.lv % 25)) + '%' }"></i></div>
              <span class="gs-lv">Lv.{{ a.lv }}</span>
            </div>
            <div class="gskills">
              <button class="sk" style="--sk:#ff8a2a">忍</button>
              <button class="sk" style="--sk:#8b5cf6">术</button>
              <button class="sk" style="--sk:#22d3ee">通</button>
              <button class="sk" style="--sk:#f43f5e">灵</button>
            </div>
            <div class="gchat">「影分身之术！」</div>
            <div class="gdamage">-1240</div>
          </div>
        </div>
      </template>
    </div>
  </main>
</template>

<style scoped>
.stage {
  flex: 1; display: flex; flex-direction: column; min-width: 0;
  background: var(--card, #fffdf7); border: 1px solid var(--line, #ecdcc3);
  border-radius: 20px; overflow: hidden;
}

/* 标签栏 */
.tabs {
  display: flex; align-items: center; gap: 4px; padding: 8px 10px;
  background: linear-gradient(180deg, #fff3dd, #fdecd0); border-bottom: 1px solid var(--line, #ecdcc3);
  overflow-x: auto; flex: none;
}
.tabs-empty { font-size: 12px; color: #8a7a66; padding: 8px 14px; }
.wtab {
  display: flex; align-items: center; gap: 8px; padding: 7px 12px;
  border-radius: 10px 10px 0 0; cursor: pointer; font-size: 12px; font-weight: 700;
  color: #8a7a66; white-space: nowrap; transition: all 0.15s;
}
.wtab:hover { color: #2b2118; background: rgba(255, 255, 255, 0.5); }
.wtab.active { color: #2b2118; background: #fffdf7; border: 1px solid var(--line, #ecdcc3); border-bottom: none; }
.wtab .dot { width: 7px; height: 7px; border-radius: 50%; background: #2a9d5f; }
.wtab .x { width: 16px; height: 16px; border: none; border-radius: 5px; background: transparent; color: #8a7a66; font-size: 10px; cursor: pointer; }
.wtab .x:hover { background: rgba(232, 72, 44, 0.15); color: #e8482c; }

/* 舞台 */
.stage-body { flex: 1; position: relative; min-height: 0; }
.empty-stage {
  position: absolute; inset: 0; display: flex; flex-direction: column;
  align-items: center; justify-content: center; gap: 14px; color: #8a7a66;
}
.empty-stage .big { font-size: 46px; opacity: 0.5; }
.empty-stage h3 { font-size: 17px; font-weight: 800; color: #2b2118; }
.empty-stage p { font-size: 13px; }
.empty-stage .tip { font-size: 12px; }

.scene { position: absolute; inset: 0; display: flex; flex-direction: column; }
.scene-bar {
  display: flex; align-items: center; gap: 10px; padding: 8px 14px;
  border-bottom: 1px dashed var(--line, #ecdcc3); flex: none;
}
.scene-bar .gname { font-size: 13px; font-weight: 800; display: flex; align-items: center; gap: 8px; }
.scene-bar .gname .d { width: 8px; height: 8px; border-radius: 50%; background: #2a9d5f; box-shadow: 0 0 8px #2a9d5f; }
.scene-bar .gmeta { font-size: 11px; color: #8a7a66; }
.scene-ctl { margin-left: auto; display: flex; gap: 6px; }
.scene-ctl button {
  padding: 5px 12px; border: none; border-radius: 8px;
  background: rgba(229, 72, 77, 0.12); color: #d64545; font-size: 11px; font-weight: 700;
  cursor: pointer; transition: all 0.15s; font-family: inherit;
}
.scene-ctl button:hover { background: #e8482c; color: #fff; }

/* 游戏画面模拟 */
.gscene {
  flex: 1; position: relative; overflow: hidden; min-height: 0;
  background: linear-gradient(180deg, #7ec8f2 0%, #d7f0ff 70%, #a8dcc4 70%, #8fcba8 100%);
}
.gsun { position: absolute; top: 14px; right: 24px; width: 34px; height: 34px; border-radius: 50%; background: radial-gradient(#fff, #ffd76a); box-shadow: 0 0 30px rgba(255, 215, 106, 0.8); }
.gcloud { position: absolute; background: #fff; border-radius: 999px; opacity: 0.9; }
.gcloud::before, .gcloud::after { content: ""; position: absolute; background: #fff; border-radius: 50%; }
.gcloud.c1 { top: 26px; left: 8%; width: 52px; height: 16px; animation: cloudDrift 16s linear infinite; }
.gcloud.c2 { top: 54px; left: 55%; width: 40px; height: 13px; animation: cloudDrift 22s linear infinite; }
.gcloud.c1::before { width: 24px; height: 24px; top: -10px; left: 10px; }
.gcloud.c1::after { width: 18px; height: 18px; top: -6px; left: 28px; }
.gcloud.c2::before { width: 18px; height: 18px; top: -8px; left: 8px; }
.gcloud.c2::after { width: 13px; height: 13px; top: -4px; left: 22px; }
@keyframes cloudDrift { from { transform: translateX(-70px); } to { transform: translateX(340px); } }
.gmtn { position: absolute; bottom: 60px; background: #7fb069; clip-path: polygon(0 100%, 50% 0, 100% 100%); }
.gmtn.m1 { left: -20px; width: 130px; height: 70px; opacity: 0.9; }
.gmtn.m2 { right: -30px; width: 160px; height: 80px; opacity: 0.7; }
.gground { position: absolute; left: 0; right: 0; bottom: 0; height: 60px; background: linear-gradient(180deg, #8fcba8, #6fae7f); }
.gground::before { content: ""; position: absolute; inset: 0; background: radial-gradient(rgba(255,255,255,0.25) 1px, transparent 1px); background-size: 14px 14px; }
.gchar { position: absolute; left: 50%; bottom: 54px; transform: translateX(-50%); width: 52px; height: 74px; animation: gBob 2.4s ease-in-out infinite; }
@keyframes gBob { 0%,100% { transform: translateX(-50%) translateY(0); } 50% { transform: translateX(-50%) translateY(-7px); } }
.gc-head { position: absolute; left: 50%; top: 0; transform: translateX(-50%); width: 26px; height: 26px; border-radius: 50%; background: #f6c9a0; }
.gc-band { position: absolute; top: 8px; left: -4px; width: 34px; height: 6px; background: var(--gband, #5b8dd6); border-radius: 3px; }
.gc-band::after { content: ""; position: absolute; right: 0; top: -3px; border: 3px solid var(--gband, #5b8dd6); border-radius: 50%; }
.gc-body { position: absolute; left: 50%; top: 24px; transform: translateX(-50%); width: 28px; height: 30px; border-radius: 7px; background: var(--gjc, #ff8a2a); }
.gc-leg { position: absolute; top: 52px; width: 10px; height: 20px; border-radius: 4px; background: #3a2f2f; }
.gc-leg.l { left: 13px; }
.gc-leg.r { right: 13px; }
.gc-aura { position: absolute; inset: -8px; border: 2px solid rgba(34, 211, 238, 0.8); border-radius: 50%; opacity: 0; }
.scene.live .gc-aura { opacity: 1; animation: auraPulse 1.6s ease-out infinite; }
@keyframes auraPulse { 0% { transform: scale(0.9); opacity: 0.8; } 100% { transform: scale(1.5); opacity: 0; } }
.gstatus { position: absolute; left: 12px; top: 10px; right: 12px; display: flex; flex-direction: column; gap: 3px; }
.gs-hp, .gs-mp { height: 8px; background: rgba(0,0,0,0.35); border-radius: 4px; overflow: hidden; }
.gs-hp i { display: block; height: 100%; background: linear-gradient(90deg, #f43f5e, #fb923c); }
.gs-mp i { display: block; height: 100%; background: linear-gradient(90deg, #22d3ee, #3b82f6); }
.gs-lv { position: absolute; right: 0; top: -2px; font-size: 11px; font-weight: 800; color: #fff; text-shadow: 0 1px 2px rgba(0,0,0,0.6); }
.gskills { position: absolute; left: 12px; bottom: 10px; display: flex; gap: 7px; }
.gskills .sk {
  width: 32px; height: 32px; border-radius: 9px; border: 1.5px solid rgba(255,255,255,0.7);
  background: linear-gradient(135deg, var(--sk), rgba(0,0,0,0.4)); color: #fff;
  font-size: 12px; font-weight: 800; cursor: pointer; transition: transform 0.15s; font-family: inherit;
}
.gskills .sk:hover { transform: scale(1.15) translateY(-2px); }
.gchat { position: absolute; right: 12px; bottom: 14px; background: rgba(0,0,0,0.45); color: #fff; font-size: 11px; padding: 5px 9px; border-radius: 7px; }
.gdamage { position: absolute; left: 18%; top: 42%; font-size: 16px; font-weight: 900; color: #ff3d5a; text-shadow: 0 0 6px #000; animation: dmg 2s ease-out infinite; }
@keyframes dmg { 0% { transform: translateY(0); opacity: 0; } 15% { opacity: 1; } 100% { transform: translateY(-32px); opacity: 0; } }
</style>
