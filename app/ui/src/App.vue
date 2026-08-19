<script setup lang="ts">
import { ref, computed } from 'vue'

// ---------- 账号数据 ----------
interface Account {
  remark: string
  username: string
  password: string
  scanLogin: boolean
}

const accounts = ref<Account[]>([
  // 预览数据（后续由 CEF 桥从本地存储加载）
])

// 编辑状态：-1 = 未编辑，其他为正在编辑的账号索引
const editingIdx = ref(-1)

const addFormVisible = ref(false)
const addScan = ref(false)
const addRemark = ref('')
const addUser = ref('')
const addPass = ref('')

const addForm = {
  remark: addRemark,
  username: addUser,
  password: addPass,
  scan: addScan,
  visible: addFormVisible,
}

function displayName(acc: Account): string {
  if (acc.scanLogin) return acc.remark || '扫码登录'
  return acc.remark || acc.username
}

function submitAdd() {
  if (addForm.scan.value) {
    if (!addForm.remark.value.trim()) return
    accounts.value.push({
      remark: addForm.remark.value.trim(),
      username: '',
      password: '',
      scanLogin: true,
    })
  } else {
    if (!addForm.username.value.trim()) return
    accounts.value.push({
      remark: addForm.remark.value.trim(),
      username: addForm.username.value.trim(),
      password: addForm.password.value,
      scanLogin: false,
    })
  }
  addForm.remark.value = ''
  addForm.username.value = ''
  addForm.password.value = ''
  addForm.scan.value = false
  addForm.visible.value = false
}

function removeAccount(idx: number) {
  accounts.value.splice(idx, 1)
}

// 空状态
const emptyText = computed(() =>
  accounts.value.length === 0 ? '还没有账号，点击上方「＋ 添加账号」' : ''
)

// 登录方式切换 tab
const loginTab = ref<'qq' | 'scan'>('qq')
</script>

<template>
  <div class="window">
    <!-- 自绘标题栏 -->
    <div class="titlebar">
      <span class="titlebar-title">火影忍者Online</span>
      <div class="titlebar-spacer"></div>
      <div class="win-btns">
        <button class="win-btn" title="最小化">—</button>
        <button class="win-btn" title="最大化">□</button>
        <button class="win-btn" title="全屏">⤢</button>
        <button class="win-btn danger" title="关闭">✕</button>
      </div>
    </div>

    <div class="body">
      <!-- 左侧导航 -->
      <aside class="sidebar">
        <div class="sidebar-head">
          <span class="brand">火影忍者Online</span>
        </div>

        <div class="section-row">
          <span class="section-title">我的账号</span>
          <button class="btn-primary small" @click="addFormVisible = !addFormVisible">
            ＋ 添加账号
          </button>
        </div>

        <!-- 添加账号表单 -->
        <div v-if="addFormVisible" class="add-form">
          <label class="scan-opt">
            <input type="checkbox" v-model="addScan" />
            <span>扫码登录（无需 QQ 号/密码）</span>
          </label>
          <input
            class="input"
            v-model="addRemark"
            :placeholder="addScan ? '备注（必填）' : '备注（可选）'"
          />
          <template v-if="!addScan">
            <input class="input" v-model="addUser" placeholder="账号 / QQ号" />
            <input class="input" v-model="addPass" type="password" placeholder="密码" />
          </template>
          <div class="form-btns">
            <button class="btn-ghost" @click="addFormVisible = false">取消</button>
            <button class="btn-primary" @click="submitAdd">保存</button>
          </div>
        </div>

        <!-- 账号列表 -->
        <div class="account-list">
          <div v-if="emptyText" class="empty-hint">{{ emptyText }}</div>
          <div v-for="(acc, idx) in accounts" :key="idx" class="account-card">
            <div class="card-row">
              <span class="type-tag" :class="acc.scanLogin ? 'scan' : 'qq'">
                {{ acc.scanLogin ? '扫码' : 'QQ' }}
              </span>
              <span class="card-name">{{ displayName(acc) }}</span>
              <button class="card-del" @click="removeAccount(idx)">✕</button>
            </div>
            <div class="card-ops">
              <button class="btn-ghost small" @click="editingIdx = idx">
                {{ editingIdx === idx ? '保存' : '编辑' }}
              </button>
              <button class="btn-primary small">开始游戏</button>
            </div>
          </div>
        </div>
      </aside>

      <!-- 右侧游戏视图 -->
      <main class="stage">
        <div class="game-view">
          <div class="game-title">火影忍者OL</div>
          <div class="game-hint">游戏视图区 —— Flash 游戏将嵌入此处</div>
          <button class="btn-primary start-btn">开始游戏</button>
          <div class="game-tip">在左侧添加账号，登录后即可进入游戏</div>
        </div>
      </main>
    </div>
  </div>
</template>

<style>
.window {
  position: absolute;
  inset: 8px;
  border-radius: 12px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  background: linear-gradient(160deg, #1c2230 0%, #171c27 45%, #12161f 100%);
  border: 1px solid rgba(255, 255, 255, 0.08);
  box-shadow: 0 12px 48px rgba(0, 0, 0, 0.5);
}

/* 标题栏 */
.titlebar {
  height: 40px;
  flex: none;
  display: flex;
  align-items: center;
  padding: 0 4px 0 16px;
  background: rgba(255, 255, 255, 0.02);
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
  -webkit-app-region: drag;
}
.titlebar-title {
  font-size: 13px;
  font-weight: bold;
  letter-spacing: 1px;
  color: #dbe2ed;
}
.titlebar-spacer { flex: 1; }
.win-btns { display: flex; -webkit-app-region: no-drag; }
.win-btn {
  width: 44px; height: 30px; line-height: 30px;
  background: transparent; border: none;
  color: #8b95a7; font-size: 13px; border-radius: 7px;
}
.win-btn:hover { background: rgba(255, 255, 255, 0.07); color: #e6eaf0; }
.win-btn.danger:hover { background: #e5484d; color: #fff; }

/* 主体 */
.body { flex: 1; display: flex; min-height: 0; }

/* 侧边栏 */
.sidebar {
  width: 270px; flex: none;
  padding: 14px;
  background: rgba(0, 0, 0, 0.22);
  border-right: 1px solid rgba(255, 255, 255, 0.06);
  display: flex; flex-direction: column;
  gap: 12px;
  overflow-y: auto;
}
.sidebar-head .brand { color: #ffa13d; font-weight: bold; font-size: 15px; }
.section-row { display: flex; align-items: center; justify-content: space-between; }
.section-title { color: #6b7588; font-size: 11px; font-weight: bold; letter-spacing: 1.5px; }

/* 表单 */
.add-form {
  background: rgba(255, 140, 26, 0.06);
  border: 1px dashed rgba(255, 161, 61, 0.45);
  border-radius: 10px;
  padding: 12px;
  display: flex; flex-direction: column; gap: 8px;
}
.scan-opt { display: flex; align-items: center; gap: 6px; color: #aab3c5; font-size: 12px; }
.scan-opt input { accent-color: #ff8c1a; }
.input {
  background: rgba(0, 0, 0, 0.3);
  border: 1px solid rgba(255, 255, 255, 0.08);
  border-radius: 8px; padding: 7px 10px;
  color: #e6eaf0; font-size: 13px; outline: none;
}
.input:focus { border-color: #ffa13d; }
.input::placeholder { color: #4d5768; }
.form-btns { display: flex; justify-content: flex-end; gap: 8px; }

/* 按钮 */
.btn-primary {
  background: linear-gradient(180deg, #ffb347, #ff7a00);
  color: #1a0f00; border: none; border-radius: 8px;
  font-size: 13px; font-weight: bold; padding: 7px 14px;
}
.btn-primary:hover { background: linear-gradient(180deg, #ffc061, #ff8c1a); }
.btn-primary.small { padding: 4px 10px; font-size: 12px; border-radius: 6px; }
.btn-ghost {
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px; color: #aab3c5; font-size: 13px; padding: 7px 14px;
}
.btn-ghost:hover { background: rgba(255, 255, 255, 0.09); }
.btn-ghost.small { padding: 4px 10px; font-size: 12px; border-radius: 6px; }

/* 账号列表 */
.account-list { flex: 1; display: flex; flex-direction: column; gap: 8px; }
.empty-hint {
  color: #5d677a; font-size: 12px; text-align: center;
  padding: 30px 10px; line-height: 1.8;
}
.account-card {
  background: rgba(255, 255, 255, 0.045);
  border: 1px solid rgba(255, 255, 255, 0.07);
  border-radius: 10px; padding: 10px;
}
.account-card:hover { background: rgba(255, 255, 255, 0.07); }
.card-row { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; }
.type-tag {
  font-size: 11px; font-weight: bold; padding: 1px 5px; border-radius: 4px;
  border: 1px solid;
}
.type-tag.scan { color: #ffa13d; border-color: rgba(255, 161, 61, 0.4); background: rgba(255, 161, 61, 0.08); }
.type-tag.qq { color: #7ab3ff; border-color: rgba(122, 179, 255, 0.4); background: rgba(122, 179, 255, 0.08); }
.card-name { flex: 1; font-weight: bold; font-size: 13px; }
.card-del { background: transparent; border: none; color: #5d677a; font-size: 12px; }
.card-del:hover { color: #ff6b70; }
.card-ops { display: flex; justify-content: flex-end; gap: 8px; }

/* 游戏视图 */
.stage { flex: 1; display: flex; flex-direction: column; min-width: 0; }
.game-view {
  flex: 1;
  display: flex; flex-direction: column;
  align-items: center; justify-content: center; gap: 14px;
  background: radial-gradient(ellipse at 50% 30%, rgba(255, 161, 61, 0.08), transparent 60%);
}
.game-title {
  font-size: 46px; font-weight: bold; letter-spacing: 8px; color: #ffa13d;
  text-shadow: 0 6px 40px rgba(255, 138, 26, 0.35);
}
.game-hint { color: #8b95a7; font-size: 13px; }
.start-btn { font-size: 15px; padding: 10px 44px; border-radius: 10px; }
.game-tip { color: #4d5768; font-size: 12px; }
</style>
