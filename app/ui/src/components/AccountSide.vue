<script setup lang="ts">
import { useAccounts } from '../composables/useAccounts'
import type { Account } from '../composables/useAccounts'

const { accounts, currentId, onlineCount, totalCount, maskQQ, displayName, startAccount, switchTo } = useAccounts()

const emit = defineEmits<{
  (e: 'add'): void
  (e: 'edit', id: number): void
}>()

// 头像配色
const ACCENTS = [
  ['#e8482c', '#f07a1f'], ['#8b5cf6', '#6d28d9'], ['#2a9d5f', '#35c17a'],
  ['#eab308', '#f97316'], ['#0ea5e9', '#2563eb'], ['#ec4899', '#8b5cf6'],
]
function colors(a: Account) {
  return ACCENTS[a.seed % ACCENTS.length]
}

function clickAccount(a: Account) {
  if (a.run) switchTo(a.id)
  else startAccount(a.id)
}
</script>

<template>
  <aside class="side">
    <div class="side-head">
      <h3><span class="ic">👤</span>账号列表</h3>
      <span class="count">{{ totalCount }} 个账号</span>
    </div>
    <div class="side-list">
      <div
        v-for="a in accounts"
        :key="a.id"
        class="acct"
        :class="{ active: currentId === a.id }"
        @click="clickAccount(a)"
      >
        <div class="av" :class="a.run ? 'on' : 'off'" :style="{ background: `linear-gradient(135deg,${colors(a)[0]},${colors(a)[1]})` }">
          {{ displayName(a)[0] }}
        </div>
        <div class="meta">
          <div class="nm">{{ displayName(a) }}</div>
          <div class="qq">
            {{ a.loggedIn ? `${a.server || '未知区服'} · Lv.${a.lv} · ${a.power || '-'}` : '未获取（登录后同步）' }}
          </div>
        </div>
        <span class="st-tag" :class="a.run ? 'run' : 'stop'">{{ a.run ? '在线' : '离线' }}</span>
        <button class="acct-edit" @click.stop="emit('edit', a.id)" title="编辑">✎</button>
      </div>
    </div>
    <div class="side-add" @click="emit('add')">＋ 添加新账号</div>
    <div class="side-foot">点击账号进入对应游戏窗口 · 可多开</div>
  </aside>
</template>

<style scoped>
.side {
  width: 250px; flex: none; display: flex; flex-direction: column; gap: 10px;
  background: var(--card, #fffdf7); border: 1px solid var(--line, #ecdcc3);
  border-radius: 20px; padding: 14px;
}
.side-head { display: flex; align-items: center; justify-content: space-between; padding: 2px 4px 8px; }
.side-head h3 { font-size: 14px; font-weight: 800; display: flex; align-items: center; gap: 8px; }
.side-head h3 .ic { width: 26px; height: 26px; border-radius: 8px; background: linear-gradient(135deg, #e8482c, #f07a1f); color: #fff; display: grid; place-items: center; font-size: 13px; }
.side-head .count { font-size: 11px; color: #8a7a66; }

.side-list { flex: 1; overflow-y: auto; display: flex; flex-direction: column; gap: 8px; }
.side-list::-webkit-scrollbar { width: 4px; }
.side-list::-webkit-scrollbar-thumb { background: var(--line, #ecdcc3); border-radius: 2px; }

.acct {
  display: flex; align-items: center; gap: 10px; padding: 10px 12px;
  border-radius: 13px; border: 1px solid var(--line, #ecdcc3); cursor: pointer;
  transition: all 0.18s; position: relative;
}
.acct:hover { background: rgba(232, 72, 44, 0.05); }
.acct.active { border-color: #e8482c; background: rgba(232, 72, 44, 0.07); }
.acct .av {
  width: 38px; height: 38px; flex: none; border-radius: 50%;
  display: grid; place-items: center; color: #fff; font-weight: 800; font-size: 16px; position: relative;
}
.acct .av::after {
  content: ""; position: absolute; right: 0; bottom: 0; width: 10px; height: 10px;
  border-radius: 50%; border: 2px solid var(--card, #fffdf7);
}
.acct .av.on::after { background: #2a9d5f; }
.acct .av.off::after { background: #ccc; }
.acct .meta { flex: 1; min-width: 0; }
.acct .meta .nm { font-size: 13px; font-weight: 800; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.acct .meta .qq { font-size: 10px; color: #8a7a66; margin-top: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.acct .st-tag { font-size: 10px; font-weight: 700; padding: 2px 8px; border-radius: 999px; flex: none; }
.acct .st-tag.run { background: rgba(42, 157, 95, 0.14); color: #1f7a4c; }
.acct .st-tag.stop { background: rgba(138, 122, 102, 0.12); color: #8a7a66; }
.acct-edit {
  flex: none; width: 24px; height: 24px; border: none; border-radius: 7px;
  background: transparent; color: #8a7a66; font-size: 12px; cursor: pointer; opacity: 0; transition: all 0.15s;
}
.acct:hover .acct-edit { opacity: 1; }
.acct-edit:hover { background: rgba(232, 72, 44, 0.15); color: #e8482c; }

.side-add {
  display: flex; align-items: center; justify-content: center; gap: 8px;
  padding: 11px; border: 2px dashed rgba(232, 72, 44, 0.35); border-radius: 13px;
  color: #e8482c; font-size: 13px; font-weight: 700; cursor: pointer; transition: all 0.2s;
}
.side-add:hover { background: rgba(232, 72, 44, 0.07); border-color: #e8482c; }
.side-foot { font-size: 10px; color: #8a7a66; text-align: center; padding-top: 6px; }
</style>
