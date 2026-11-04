/**
 * 购物车接口冒烟（Jsd.Api /api/cart/*）
 * 用法：node smoke_cart.js  （需先启动 API，默认 http://localhost:5299）
 * 环境变量：BASE_URL / MEMBER_ID / SKU_ID / JWT_SECRET
 *
 * 覆盖用户要求的三条核心用例：
 *   1. 同一镜片「需加工」+「只需发片」→ 2 条记录
 *   2. 同一镜片「需加工」+ 相同的「需加工」→ 1 条记录，数量 +1
 *   3. （跨设备）同一会员任意设备 GetList 数据一致 —— 由服务端存储天然保证，
 *      冒烟中以「同 token 重复读」间接验证。
 */
const crypto = require('crypto')

const BASE = process.env.BASE_URL || 'http://localhost:5299/api'
const SECRET =
  process.env.JWT_SECRET || 'JingSuDingOrderSystem2026JwtSecretKey_MustBeLongEnough32+'
const MEMBER = Number(process.env.MEMBER_ID || 1001)
const SKU_ID = Number(process.env.SKU_ID || 17)

function b64url(buf) {
  return Buffer.from(buf)
    .toString('base64')
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
}

/** 伪造会员 JWT（不携带 ver，避免触发 TokenVersionMiddleware 的密码版本校验） */
function makeMemberToken() {
  const now = Math.floor(Date.now() / 1000)
  const payload = {
    nameid: String(MEMBER),
    'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier': String(MEMBER),
    member_id: String(MEMBER),
    user_type: 'member',
    name: 'buyer1001',
    role: 'member',
    iss: 'jingsuding',
    aud: 'jingsuding-admin',
    iat: now,
    nbf: now,
    exp: now + 43200
  }
  const head = b64url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const body = b64url(JSON.stringify(payload))
  const seg = `${head}.${body}`
  const sig = crypto.createHmac('sha256', SECRET).update(seg).digest()
  return `${seg}.${b64url(sig)}`
}

const TOKEN = makeMemberToken()

async function api(path, { method = 'GET', body, token = TOKEN } = {}) {
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`
    },
    body: body === undefined ? undefined : JSON.stringify(body)
  })
  const text = await res.text()
  let json
  try {
    json = text ? JSON.parse(text) : null
  } catch {
    throw new Error(`${method} ${path} 返回非 JSON (HTTP ${res.status}): ${text.slice(0, 200)}`)
  }
  if (res.status === 401) throw new Error(`${method} ${path} 401 未认证: ${text.slice(0, 200)}`)
  return json
}

let failed = 0
function assert(cond, msg) {
  if (cond) {
    console.log(`PASS  ${msg}`)
  } else {
    failed += 1
    console.error(`FAIL  ${msg}`)
  }
}

const RX = {
  left: { sphere: '-1.75', cylinder: '-0.50', axis: '180', pd: '32', height: '' },
  right: { sphere: '-2.00', cylinder: '-0.75', axis: '175', pd: '32', height: '' }
}

;(async () => {
  // 0. 清空，保证初始态干净
  await api('/cart/clear', { method: 'POST', body: {} })
  let list = await api('/cart/list')
  assert(list.code === 200 && list.data.items.length === 0, '初始购物车为空')

  // 1. 需加工 + 完整验光
  const a1 = await api('/cart/add', {
    method: 'POST',
    body: { skuId: SKU_ID, quantity: 1, processingType: 1, prescription: RX }
  })
  assert(a1.code === 200 && a1.data > 0, `需加工加购成功（id=${a1.data}）`)

  // 2. 再次「相同的需加工 + 相同验光」→ 必须合并为同一行
  const a2 = await api('/cart/add', {
    method: 'POST',
    body: { skuId: SKU_ID, quantity: 1, processingType: 1, prescription: RX }
  })
  assert(a2.data === a1.data, '相同加工+相同验光 → 同一行（合并）')

  // 3. 同一镜片「不需加工（只发片）」→ 必须是全新一行
  const b1 = await api('/cart/add', {
    method: 'POST',
    body: { skuId: SKU_ID, quantity: 1, processingType: 0, prescription: null }
  })
  assert(b1.code === 200 && b1.data !== a1.data, '需加工 vs 只发片 → 两条独立记录')

  // 4. 列表校验
  list = await api('/cart/list')
  const items = list.data.items
  assert(items.length === 2, `列表共 2 条（实际 ${items.length}）`)
  const need = items.find((i) => i.processingType === 1)
  const none = items.find((i) => i.processingType === 0)
  assert(!!need && need.qty === 2, `需加工行数量=2（实际 ${need && need.qty}）`)
  assert(!!none && none.qty === 1, `只发行数量=1（实际 ${none && none.qty}）`)
  assert(need && need.processingStatus === 'unprocessed', '需加工行标签=unprocessed（待加工）')
  assert(none && none.processingStatus === 'none', '只发行标签=none（无需加工）')
  assert(
    need && need.prescription && need.prescription.left.sphere === '-1.75',
    '验光数据原样回传（球镜 -1.75）'
  )
  assert(none && !none.prescription, '只发行不携带验光数据')
  assert(!!need && need.price > 0, `需加工行价格已计算（${need && need.price}）`)

  // 5. 轴位不同 → 新行
  const rx2 = JSON.parse(JSON.stringify(RX))
  rx2.left.axis = '90'
  const c1 = await api('/cart/add', {
    method: 'POST',
    body: { skuId: SKU_ID, quantity: 1, processingType: 1, prescription: rx2 }
  })
  assert(c1.data !== a1.data, '轴位不同 → 新行（验光参与唯一键）')

  // 6. 勾选切换
  await api('/cart/toggle-select', { method: 'POST', body: { id: none.id, selected: false } })
  list = await api('/cart/list')
  assert(list.data.items.find((i) => i.id === none.id).checked === false, '取消勾选生效')

  // 7. 修改数量
  await api('/cart/update-qty', { method: 'POST', body: { id: none.id, quantity: 5 } })
  list = await api('/cart/list')
  assert(list.data.items.find((i) => i.id === none.id).qty === 5, '数量更新生效')

  // 8. 全选
  await api('/cart/toggle-all', { method: 'POST', body: { selected: true } })
  list = await api('/cart/list')
  assert(list.data.allChecked === true, '全选生效')

  // 9. 删除单行
  await api('/cart/delete', { method: 'POST', body: { id: c1.data } })
  list = await api('/cart/list')
  assert(!list.data.items.some((i) => i.id === c1.data), '删除单行生效')

  // 10. 清空
  await api('/cart/clear', { method: 'POST', body: {} })
  list = await api('/cart/list')
  assert(list.data.items.length === 0, '清空购物车生效')

  console.log(failed === 0 ? 'ALL_OK' : `FAILED(${failed})`)
  process.exit(failed === 0 ? 0 : 1)
})().catch((e) => {
  console.error('SMOKE ERROR:', e.message)
  process.exit(1)
})
