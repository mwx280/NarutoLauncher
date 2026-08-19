import { ref, computed } from 'vue'

// 账号数据结构
export interface Account {
  id: number
  qq: string
  name: string
  pwd?: string
  server: string       // 所在区服（登录后从游戏获取）
  lv: number           // 等级（登录后获取）
  power: string        // 战力（登录后获取）
  loggedIn: boolean    // 是否已登录（未登录显示"未获取"）
  scanLogin: boolean   // 是否扫码登录
  run: boolean         // 窗口是否运行中
  seed: number         // 头像配色
}

const STORAGE_KEY = 'hy-multi-accounts'

function loadAccounts(): Account[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) return JSON.parse(raw)
  } catch { /* ignore */ }
  return []
}

const accounts = ref<Account[]>(loadAccounts())

// 默认示例数据（仅首次）
if (!accounts.value.length) {
  accounts.value = [
    { id: 1, qq: '3026661111', name: '大号·鸣人', server: '火影一区', lv: 168, power: '3.2亿', loggedIn: true, scanLogin: false, run: false, seed: 0 },
    { id: 2, qq: '3026662222', name: '佐助小号', server: '火影二区', lv: 152, power: '2.8亿', loggedIn: true, scanLogin: false, run: false, seed: 1 },
    { id: 3, qq: '3026663333', name: '未登录新号', server: '', lv: 0, power: '', loggedIn: false, scanLogin: false, run: false, seed: 2 },
  ]
  saveAccounts()
}

function saveAccounts() {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(accounts.value))
}

// 在线 / 离线账号数
const onlineCount = computed(() => accounts.value.filter(a => a.run).length)
const totalCount = computed(() => accounts.value.length)

// 当前聚焦的窗口 id
const currentId = ref<number | null>(null)

function maskQQ(q: string) {
  return String(q).replace(/^(\d{3})\d+(\d{4})$/, '$1****$2')
}
function displayName(a: Account) {
  return a.name || a.qq || '未命名'
}

// ---- 账号操作 ----
function addAccount(data: Partial<Account>) {
  accounts.value.push({
    id: Date.now(),
    qq: data.qq || '',
    name: data.name || '',
    pwd: data.pwd || '',
    server: '',
    lv: 0,
    power: '',
    loggedIn: false,
    scanLogin: !!data.scanLogin,
    run: false,
    seed: Math.floor(Math.random() * 6),
    ...data,
  })
  saveAccounts()
}

function updateAccount(id: number, data: Partial<Account>) {
  const a = accounts.value.find(x => x.id === id)
  if (!a) return
  Object.assign(a, data)
  saveAccounts()
}

function removeAccount(id: number) {
  accounts.value = accounts.value.filter(x => x.id !== id)
  if (currentId.value === id) currentId.value = null
  saveAccounts()
}

function startAccount(id: number) {
  const a = accounts.value.find(x => x.id === id)
  if (!a) return
  a.run = true
  saveAccounts()
}
function stopAccount(id: number) {
  const a = accounts.value.find(x => x.id === id)
  if (!a) return
  a.run = false
  if (currentId.value === id) currentId.value = null
  saveAccounts()
}
function startAll() {
  accounts.value.forEach(a => { if (!a.run) a.run = true })
  saveAccounts()
}
function switchTo(id: number) {
  currentId.value = id
}

// ---- 扫码二维码（官网选区页同款 appid） ----
const QR_APPID = 102045649
const QR_RETURN_URL = 'https://huoying.qq.com/server/website/'

function qrImageUrl() {
  return `https://ssl.ptlogin2.qq.com/ptqrshow?appid=${QR_APPID}&e=2&l=M&s=3&d=72&v=4&t=${Date.now()}&da=1&pt_3rd_aid=${QR_APPID}`
}

export function useAccounts() {
  return {
    accounts,
    currentId,
    onlineCount,
    totalCount,
    maskQQ,
    displayName,
    addAccount,
    updateAccount,
    removeAccount,
    startAccount,
    stopAccount,
    startAll,
    switchTo,
    qrImageUrl,
  }
}
