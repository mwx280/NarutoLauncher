<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { useAccounts, type Account } from '../composables/useAccounts'

const { addAccount, updateAccount, qrImageUrl } = useAccounts()

const props = defineProps<{ show: boolean }>()
const emit = defineEmits<{ (e: 'close'): void }>()

// 编辑状态：null=新增
const editingId = ref<number | null>(null)
const addMode = ref<'qq' | 'scan'>('qq')
const fName = ref('')
const fQQ = ref('')
const fPwd = ref('')
const fNameScan = ref('')
const qrSrc = ref('')
const scanStatus = ref('请使用手机 QQ 扫码 · 登录后同步区服/等级/战力')
let scanTimer: ReturnType<typeof setInterval> | null = null

function openAdd() {
  editingId.value = null
  addMode.value = 'qq'
  fName.value = ''; fQQ.value = ''; fPwd.value = ''; fNameScan.value = ''
}

function openEdit(a: Account) {
  editingId.value = a.id
  addMode.value = a.scanLogin ? 'scan' : 'qq'
  fName.value = a.name || ''
  fQQ.value = a.qq || ''
  fPwd.value = a.pwd || ''
  fNameScan.value = a.name || ''
  if (addMode.value === 'scan') loadQR()
}

function switchMode(mode: 'qq' | 'scan') {
  addMode.value = mode
  if (mode === 'scan') loadQR()
}

function loadQR() {
  qrSrc.value = qrImageUrl()
  scanStatus.value = '请使用手机 QQ 扫码 · 登录后同步区服/等级/战力'
  // 实装后由 CEF 宿主代理 ptqrlogin 轮询（跨域 + cookie）
  startPoll()
}

function startPoll() {
  stopPoll()
  scanTimer = setInterval(() => {
    // 预览：不实际轮询。实装时由宿主完成登录态检测。
  }, 4000)
}
function stopPoll() {
  if (scanTimer) { clearInterval(scanTimer); scanTimer = null }
}

function save() {
  if (editingId.value !== null) {
    const data: Partial<Account> = { scanLogin: addMode.value === 'scan' }
    if (addMode.value === 'qq') {
      const qq = fQQ.value.trim()
      if (!/^\d{5,}$/.test(qq)) { alert('请输入正确的 QQ 号'); return }
      data.qq = qq
      data.pwd = fPwd.value
      if (fName.value.trim()) data.name = fName.value.trim()
    } else {
      if (fNameScan.value.trim()) data.name = fNameScan.value.trim()
    }
    updateAccount(editingId.value, data)
  } else {
    if (addMode.value === 'qq') {
      const qq = fQQ.value.trim()
      if (!/^\d{5,}$/.test(qq)) { alert('请输入正确的 QQ 号'); return }
      addAccount({ qq, name: fName.value.trim() || qq, pwd: fPwd.value, scanLogin: false })
    } else {
      const name = fNameScan.value.trim()
      if (!name) { alert('请填写备注'); return }
      addAccount({ name, scanLogin: true })
    }
  }
  stopPoll()
  emit('close')
}

defineExpose({ openAdd, openEdit })

onBeforeUnmount(stopPoll)
</script>

<template>
  <Teleport to="body">
    <Transition name="fade">
      <div v-if="show" class="overlay" @click.self="emit('close')">
        <div class="modal">
          <h3>{{ editingId !== null ? '编辑账号' : '添加新账号' }}</h3>

          <!-- 添加方式切换 -->
          <div class="add-modes">
            <button class="add-mode" :class="{ active: addMode === 'qq' }" @click="switchMode('qq')">
              <span class="am-ico">💬</span>
              <span class="am-t">QQ 账号添加</span>
              <span class="am-d">输入 QQ 号与密码，记住登录态</span>
            </button>
            <button class="add-mode" :class="{ active: addMode === 'scan' }" @click="switchMode('scan')">
              <span class="am-ico">📱</span>
              <span class="am-t">扫码添加</span>
              <span class="am-d">使用 QQ 扫码，安全免密</span>
            </button>
          </div>

          <!-- QQ 表单 -->
          <div v-if="addMode === 'qq'" class="mode-form">
            <div class="field">
              <label>备注</label>
              <input v-model="fName" type="text" placeholder="如：大号 / 漩涡鸣人">
            </div>
            <div class="field">
              <label>QQ 号</label>
              <input v-model="fQQ" type="text" placeholder="请输入 QQ 号">
            </div>
            <div class="field">
              <label>密码</label>
              <input v-model="fPwd" type="password" placeholder="请输入密码">
            </div>
          </div>

          <!-- 扫码表单 -->
          <div v-else class="mode-form">
            <div class="field">
              <label>备注</label>
              <input v-model="fNameScan" type="text" placeholder="如：大号 / 漩涡鸣人">
            </div>
            <div class="scan-box">
              <div class="scan-img-wrap">
                <img :src="qrSrc" alt="二维码">
                <div class="scanline"></div>
                <div class="scan-refresh" @click="loadQR">🔄 刷新二维码</div>
              </div>
              <div class="scan-status">{{ scanStatus }}</div>
            </div>
          </div>

          <div class="modal-ft">
            <button class="top-btn outline" @click="emit('close')">取消</button>
            <button class="top-btn fire" @click="save">{{ editingId !== null ? '保存修改' : '保存账号' }}</button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.overlay {
  position: fixed; inset: 0; z-index: 60;
  background: rgba(60, 40, 20, 0.5); backdrop-filter: blur(6px);
  display: flex; align-items: center; justify-content: center;
}
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.modal {
  width: 440px; max-width: 92vw;
  background: var(--card, #fffdf7); border: 1px solid var(--line, #ecdcc3);
  border-radius: 24px; padding: 30px;
  box-shadow: 0 40px 80px rgba(60, 40, 20, 0.4);
}
.modal h3 { font-size: 18px; font-weight: 800; margin-bottom: 20px; }

.add-modes { display: flex; gap: 10px; margin-bottom: 18px; }
.add-mode {
  flex: 1; border: 1.5px solid var(--line, #ecdcc3); border-radius: 14px;
  padding: 14px 12px; background: transparent; cursor: pointer; text-align: left;
  font-family: inherit; display: flex; flex-direction: column; gap: 6px; transition: all 0.2s;
}
.add-mode:hover { border-color: rgba(232, 72, 44, 0.4); background: rgba(232, 72, 44, 0.04); }
.add-mode.active { border-color: #e8482c; background: rgba(232, 72, 44, 0.07); }
.add-mode .am-ico { font-size: 22px; }
.add-mode .am-t { font-size: 14px; font-weight: 800; color: #2b2118; }
.add-mode .am-d { font-size: 11px; color: #8a7a66; }

.field { margin-bottom: 15px; }
.field label { display: block; font-size: 12px; color: #8a7a66; margin-bottom: 6px; font-weight: 600; }
.field input {
  width: 100%; padding: 11px 13px; border-radius: 11px;
  border: 1.5px solid var(--line, #ecdcc3); background: #fff; color: #2b2118;
  font-size: 14px; font-family: inherit; outline: none; transition: all 0.25s;
}
.field input:focus { border-color: #e8482c; box-shadow: 0 0 0 3px rgba(232, 72, 44, 0.12); }

.scan-box { text-align: center; }
.scan-img-wrap { position: relative; width: 210px; height: 210px; margin: 6px auto 10px; }
.scan-img-wrap img {
  width: 100%; height: 100%; object-fit: contain;
  border: 1px solid var(--line, #ecdcc3); border-radius: 14px; background: #fff;
}
.scanline {
  position: absolute; left: 10px; right: 10px; height: 2px; top: 10px; border-radius: 2px;
  background: linear-gradient(90deg, transparent, #e8482c, transparent);
  box-shadow: 0 0 10px rgba(232, 72, 44, 0.6);
  animation: scanline 2.4s ease-in-out infinite;
}
@keyframes scanline { 0% { top: 10px; opacity: 0.2; } 50% { opacity: 1; } 100% { top: calc(100% - 12px); opacity: 0.2; } }
.scan-refresh {
  position: absolute; right: 6px; bottom: 6px; background: rgba(43, 33, 24, 0.75);
  color: #fff; font-size: 11px; padding: 4px 10px; border-radius: 7px; cursor: pointer;
}
.scan-refresh:hover { background: #e8482c; }
.scan-status { font-size: 12px; color: #8a7a66; }

.modal-ft { display: flex; gap: 10px; margin-top: 20px; }
.top-btn {
  flex: 1; border: none; cursor: pointer; font-family: inherit; font-weight: 700;
  border-radius: 999px; padding: 11px 0; font-size: 14px; transition: all 0.2s;
}
.top-btn.fire { background: linear-gradient(90deg, #e8482c, #f07a1f); color: #fff; }
.top-btn.outline { background: transparent; color: #2b2118; border: 1.5px solid #e8482c; }
</style>
