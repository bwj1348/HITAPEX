# 用户系统 API 文档

## 基础信息

- **Base URL**: `http://192.168.1.214:1337/api`

> Base URL 是当前开发机内网地址。客户端应把它做成**可配置项**（配置文件 / 启动参数），不要硬编码——上线后 IP / 域名会变。
- **请求格式**: `application/json`
- **字符编码**: UTF-8

---

## 通用响应格式

### 成功响应

所有成功的 API 请求均返回以下统一格式：

```json
{
  "success": true,
  "data": { ... },
  "message": "请求成功"
}
```

> **`message` 有两层，别混淆**：
> - 顶层 `message` 由服务端中间件统一填充为 `"请求成功"`，仅作成功标志，**不建议当业务提示用**。
> - 具体业务提示在 `data.message` 里（如 `"注册成功，验证码已发送"`、`"密码修改成功"`）。
> - 注意：**不是所有接口的 `data` 都带 `message`**（如 `refresh-token`、`update-me` 只有业务数据、无 `data.message`）。客户端成功 toast 建议：优先读 `data.message`，读不到就回退到顶层 `message` 或自己写死文案。

### 分页响应

列表接口（如 `GET /api/user-presets`）传分页参数 `?pagination[page]=1&pagination[pageSize]=25` 时，响应增加 `meta.pagination`：

```json
{
  "success": true,
  "data": [ ... ],
  "meta": {
    "pagination": {
      "page": 1,
      "pageSize": 25,
      "pageCount": 3,
      "total": 62
    }
  },
  "message": "请求成功"
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `meta.pagination.page` | number | 当前页（从 1 起） |
| `meta.pagination.pageSize` | number | 每页条数 |
| `meta.pagination.pageCount` | number | 总页数 |
| `meta.pagination.total` | number | 总记录数 |

> **默认行为**：不传分页参数时，`data` 为完整数组，`meta` 为空对象 `{}`（中间件恒输出 `meta` 字段，只是不分页时为空）。客户端如需分页，必须显式传 `pagination[page]` 与 `pagination[pageSize]`。

### 错误响应

```json
{
  "success": false,
  "error": {
    "status": 400,
    "code": "AUTH_EMAIL_TAKEN",
    "message": "该邮箱已被注册",
    "details": null,
    "timestamp": "2026-08-12T12:00:00.000Z"
  }
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| `status` | number | HTTP 状态码 |
| `code` | string | **业务错误码**（稳定契约，客户端按此分支，不会随文案改动而变化） |
| `message` | string | 人类可读中文，可直接 toast |
| `details` | any \| null | 可选补充（如缺失字段名、`retry_after` 秒数等），无则为 null |
| `timestamp` | string | ISO 8601 时间戳 |

> **客户端契约建议**：用 `error.code` 做 switch-case 决定 UI 行为（如 `AUTH_EMAIL_TAKEN` 显示"去登录"按钮、`OTP_INVALID` 清空验证码输入框），用 `error.message` 做 toast 文案。不要把 `message` 当成程序判断依据——文案可能调整，`code` 不会变。

常见 HTTP `status`：

| status | 说明 |
|------|------|
| 400 | 请求参数错误 / 业务校验未通过 |
| 401 | 认证失败（token 无效或过期） |
| 404 | 资源不存在 |
| 429 | 请求过于频繁（触发限流） |
| 500 | 服务器内部错误 |

> **限流（429）**：服务端已启用限流。当前策略（见 `config/middlewares.ts`）：
> - `/api/auth/local` 密码登录：按 `IP + identifier(账号)` 每分钟 **60** 次（防字典攻击：同一账号换 IP 无效，要绕过只能换目标账号）
> - `/api/auth/*` 其他公开接口（注册 / 发码 / 验码）：按 `IP + email` 每分钟 **5** 次
> - `/api/*` 已登录接口：按 `IP` 每分钟 **60** 次
>
> 触发 429 时读取响应体的 `error.details.retry_after`（秒，距下次可请求的倒计时）或 `Retry-After` 响应头，提示用户并禁用按钮倒计时。

---

## 错误码总览

所有业务错误码（按类别分组）。客户端按 `error.code` 分支处理。

### 字段校验

| code | message | 触发场景 |
|------|---------|---------|
| `VALIDATION_FIELD_MISSING` | 请填写完整的信息 | 注册 / 忘记密码 / 重置密码时必填字段缺失 |
| `VALIDATION_EMAIL_MISSING` | 请输入邮箱 | 请求未带 email |
| `VALIDATION_OTP_MISSING` | 请输入邮箱和验证码 | verify-otp 缺 email 或 code |
| `VALIDATION_EMAIL_FORMAT` | 邮箱格式不正确 | email 不符合格式 |
| `VALIDATION_USERNAME_LENGTH` | 用户名需 2-20 个字符 | 用户名长度越界 |
| `VALIDATION_USERNAME_FORMAT` | 用户名只能包含中文、字母、数字、下划线和连字符 | 用户名含非法字符 |
| `VALIDATION_PASSWORD_LENGTH` | 密码至少 8 位 | 注册 / 修改密码 / 重置密码时密码长度不足 8 位 |
| `VALIDATION_PASSWORD_MISMATCH` | 两次输入的密码不一致 | 修改密码时 password 与 passwordConfirmation 不一致 |
| `VALIDATION_PASSWORD_SAME` | 新密码不能与原密码相同 | 修改密码时新密码与原密码一致 |
| `VALIDATION_NO_FIELDS` | 请提供需要修改的字段 | update-me 未传任何字段 |
| `VALIDATION_PURPOSE_INVALID` | 无效的操作类型 | verify-stepup 传入不在白名单的 purpose |

### 认证 / 账号

| code | message | 触发场景 |
|------|---------|---------|
| `AUTH_REQUIRED` | 请先登录 | 服务端防御码，正常不对外返回；实际未登录 401 返回 `SYSTEM_UNAUTHORIZED` |
| `AUTH_EMAIL_TAKEN` | 该邮箱已被注册 | 邮箱已被已激活用户占用 |
| `AUTH_USERNAME_TAKEN` | 该用户名已被占用 | 用户名被其他用户使用 |
| `AUTH_USER_NOT_FOUND` | 用户不存在 | 邮箱未注册（verify-otp / resend-otp） |
| `AUTH_USER_NOT_REGISTERED` | 该用户尚未注册 | login-otp 邮箱未注册 |
| `AUTH_USER_BLOCKED` | 账号已被禁用 | 用户被管理员封禁 |
| `AUTH_USER_ALREADY_ACTIVATED` | 用户已激活 | resend-otp 对已激活用户调用 |
| `AUTH_USER_NOT_ACTIVATED` | 账号尚未激活，请先完成注册 | login-otp 对未激活用户调用 |
| `AUTH_WRONG_PASSWORD` | 原密码错误 | 修改密码时 currentPassword 不正确 |

### 验证码

| code | message | 触发场景 |
|------|---------|---------|
| `OTP_INVALID` | 验证码错误 | 验证码不匹配 |
| `OTP_MAX_ATTEMPTS` | 验证码错误次数过多，请重新获取 | 连续错码达 10 次，验证码作废 |
| `OTP_EXPIRED` | 验证码已过期 | 超过 10 分钟有效期 |
| `OTP_COOLDOWN` | 请稍后再试 | 距上次发送不足 60 秒 |
| `OTP_SEND_FAILED` | 验证码发送失败，请稍后重试 | 邮件服务异常 |

### 身份确认 / step-up

> step-up JWT 是验证码校验通过后签发的**短期凭证（5 分钟）**，用于证明"当前操作者已完成身份确认"。目前仅重置密码使用，未来可扩展到改邮箱、注销账号等敏感操作。token 通过 `Authorization: Bearer <stepup_jwt>` 头携带，由控制器手动校验（不依赖 Strapi 鉴权中间件）。

| code | message | 触发场景 |
|------|---------|---------|
| `STEPUP_TOKEN_MISSING` | 缺少身份验证凭证 | reset-password 未带 Authorization 头（status=401） |
| `STEPUP_TOKEN_INVALID` | 身份验证已失效，请重新操作 | step-up JWT 签名无效 / 格式错误（status=401） |
| `STEPUP_TOKEN_EXPIRED` | 验证已过期，请重新获取验证码 | step-up JWT 超过 5 分钟有效期（status=401） |
| `STEPUP_WRONG_PURPOSE` | 该凭证不支持此操作 | step-up JWT 的 purpose 与当前操作不匹配（status=403） |

### 头像

| code | message | 触发场景 |
|------|---------|---------|
| `AVATAR_TOO_LARGE` | 头像文件不能超过 5MB | 上传文件超 5MB |
| `AVATAR_TYPE_INVALID` | 头像仅支持 JPG、PNG、WebP、GIF 格式 | mimetype 不在允许列表 |

### 预设

| code | message | 触发场景 |
|------|---------|---------|
| `PRESET_NOT_FOUND` | 预设不存在 | 预设不存在或不属于当前用户（status=404，不泄漏存在性） |

### 系统 / 兜底

| code | message | 触发场景 |
|------|---------|---------|
| `SYSTEM_ROLE_MISSING` | 未找到默认角色配置 | 服务端未配置 authenticated 角色（status=500） |
| `SYSTEM_NOT_FOUND` | 资源未找到 | 路由未命中（status=404） |
| `SYSTEM_VALIDATION_ERROR` | 数据验证失败 | Strapi 原生校验错误 |
| `SYSTEM_UNAUTHORIZED` | 认证失败 | Strapi 原生 401（status=401） |
| `SYSTEM_RATE_LIMITED` | 请求过于频繁 | 限流兜底（status=429） |
| `SYSTEM_INTERNAL` | 服务器内部错误 | 未捕获异常（status=500） |

---

## 认证方式

除公开接口外，需要认证的接口在请求头中携带 JWT：

```
Authorization: Bearer <jwt_token>
```

JWT token 由登录/验证码验证接口返回。

> **401 统一处理约定**：任何请求若返回 `401`（`code` 通常为 `SYSTEM_UNAUTHORIZED`），说明 token 无效或已过期。客户端应在 HTTP 层按**状态码 401** 做**全局拦截**：清空本地 token → 跳转登录页，不要每个接口单独写这段逻辑，也不要依赖具体 `code` 判断。

> **退出登录**：后端不提供 logout 端点（JWT 无状态）。客户端「退出登录」即本地删除 token，无需请求服务端。

> **旧 JWT 作废机制（token_version）**：JWT 本身无状态，为了让「改密码 / 重置密码」后旧设备的凭证立即失效，服务端在 User 表维护 `token_version` 字段：
> - 每次签发 JWT 时把当前 `token_version` 写入 payload
> - 改密码 / 重置密码时 `token_version` + 1
> - 每次带 Authorization 头的请求，中间件 decode JWT 拿 payload 里的版本号，与 DB 当前值比对，不一致即 401（`SYSTEM_UNAUTHORIZED`）
>
> 这意味着用户改密码后，**其他设备的登录态会被立即挤出**（设备丢失场景下的安全保护）。客户端收到 401 后应清空本地 token 跳转登录页。

---

## 数据类型定义

以下为响应中嵌套对象的结构（JSON wire format）。所有类型都附带 Strapi 标准元字段：

- `id`: number — 数字主键
- `documentId`: string — Strapi v5 文档 ID（UUID）
- `createdAt` / `updatedAt`: string — ISO 8601 时间戳
- 可发布内容类型（draftAndPublish）额外有 `publishedAt`: string | null

> **JSON 命名约定**：所有字段为 **camelCase**。WPF 端用 `System.Text.Json` 配 `JsonNamingPolicy.CamelCase`，或 Newtonsoft 的 `CamelCasePropertyNamesContractResolver`，可直接反序列化为 PascalCase 属性。
>
> **可空性**：下表标 `?` 的字段在数据库可空或来自 populate（未 populate 时不出现该 key，而非 `null`）。客户端反序列化时应把这类字段当 nullable。

### UserProfile

用户个人资料（与 User 一对一，未创建时整个 key 不出现）。

| 字段 | 类型 | 说明 |
|------|------|------|
| `phone` | string \| null | 联系电话 |
| `nickname` | string \| null | 昵称 |

### UserDevice

用户绑定的设备。

| 字段 | 类型 | 说明 |
|------|------|------|
| `device_name` | string | 设备名称 |
| `serial_number` | string | 序列号，全局唯一 |
| `current_firmware` | string | 当前固件版本 |
| `purchase_date` | string | 购买日期 `YYYY-MM-DD` |
| `warranty_expiry` | string \| null | 保修截止 `YYYY-MM-DD` |
| `device_status` | enum | `active` \| `needs_update` \| `under_repair` \| `retired` |
| `last_connected` | string \| null | 最近连接时间 ISO 8601 |
| `config` | object \| null | 设备配置（自由 JSON，结构由客户端约定） |
| `product` | Product \| null | 关联产品（需 populate 才出现） |

### PurchaseRecord

购买记录。

| 字段 | 类型 | 说明 |
|------|------|------|
| `email` | string \| null | 购买邮箱 |
| `source` | enum \| null | `shopify` \| `taobao` |
| `externalorder_id` | string \| null | 外部订单号 |
| `state` | boolean \| null | 核销状态 |
| `product_model` | ProductModel \| null | 关联产品型号（需 populate 才出现） |

### ProductModel

产品型号（PurchaseRecord 的二级关联）。

| 字段 | 类型 | 说明 |
|------|------|------|
| `name` | string \| null | 型号名 |
| `model_code` | string \| null | 型号编码 |
| `category` | enum \| null | `base` \| `wheel` \| `pedal` |
| `image` | Media[] \| null | 多图 |
| `sku_mapping` | object \| null | SKU 映射（自由 JSON） |

### UserPreset

用户预设（设备参数配置）。详见 [八、用户预设管理](#八用户预设管理)。

| 字段 | 类型 | 说明 |
|------|------|------|
| `config_data` | object | 预设参数，**结构由客户端约定**（服务端不校验内部字段） |
| `user` | number | 所属用户 ID |

### Media

Strapi 媒体库文件（User.image、ProductModel.image 等字段用此结构）。

| 字段 | 类型 | 说明 |
|------|------|------|
| `id` | number | |
| `documentId` | string | |
| `url` | string | 相对路径，如 `/uploads/avatar_xxx.png`，客户端需自行拼接 backend host |
| `name` | string | 原始文件名 |
| `mime` | string | MIME 类型 |
| `width` | number \| undefined | |
| `height` | number \| undefined | |
| `size` | number | 字节数 |
| `formats` | object \| undefined | 缩略图变体（`thumbnail` / `small` / `medium` / `large`），每个变体含 `{ url, width, height }` |

> **自由 JSON 字段说明**：`UserPreset.config_data`、`UserDevice.config`、`ProductModel.sku_mapping` 在 schema 里是无约束 `json` 类型，服务端只存不校验。客户端应自己定义子结构并做缺字段兜底——不要假设服务端会保证某个 key 存在。

---

## 一、验证码注册流程

### 1.1 注册（发送验证码）

```
POST /api/auth/local/register-otp
```

**请求体**：

```json
{
  "email": "user@example.com",
  "username": "myusername",
  "password": "mypassword"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| email | string | 是 | 邮箱地址 |
| username | string | 是 | 用户名，2-20 个字符，全局唯一 |
| password | string | 是 | 密码，至少 8 位，字符不限 |

**成功响应**：

```json
{
  "success": true,
  "data": {
    "user": {
      "id": 1,
      "email": "user@example.com",
      "username": "myusername"
    },
    "message": "注册成功，验证码已发送"
  },
  "message": "请求成功"
}
```

**错误示例**：

| code | message | 触发条件 |
|------|---------|---------|
| `VALIDATION_FIELD_MISSING` | 请填写完整的注册信息 | email / username / password 有缺失 |
| `VALIDATION_EMAIL_FORMAT` | 邮箱格式不正确 | email 不符合格式 |
| `VALIDATION_USERNAME_LENGTH` | 用户名需 2-20 个字符 | 用户名长度越界 |
| `VALIDATION_USERNAME_FORMAT` | 用户名只能包含中文、字母、数字、下划线和连字符 | 用户名含非法字符 |
| `VALIDATION_PASSWORD_LENGTH` | 密码至少 8 位 | 密码长度不足 8 位 |
| `AUTH_EMAIL_TAKEN` | 该邮箱已被注册 | 邮箱已被已激活用户占用 |
| `AUTH_USERNAME_TAKEN` | 该用户名已被占用 | 用户名被其他用户使用 |
| `SYSTEM_ROLE_MISSING` | 未找到默认角色配置 | 服务端未配置 authenticated 角色（status=500） |
| `OTP_SEND_FAILED` | 验证码发送失败，请稍后重试 | 邮件服务异常 |

> **说明**：注册成功后用户状态为"未激活"，系统会向邮箱发送 6 位数字验证码，有效期 **10 分钟**。

---

### 1.2 验证邮箱（激活账号 / 验证码登录）

```
POST /api/auth/verify-otp
```

**请求体**：

```json
{
  "email": "user@example.com",
  "code": "123456"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| email | string | 是 | 邮箱地址 |
| code | string | 是 | 6 位数字验证码（**字符串类型，别传数字**，否则后端报 500） |

**成功响应**：

```json
{
  "success": true,
  "data": {
    "jwt": "eyJhbGciOiJIUzI1NiIs...",
    "user": {
      "id": 1,
      "email": "user@example.com",
      "username": "myusername"
    },
    "message": "激活成功"
  },
  "message": "请求成功"
}
```

> **说明**：
> - 首次注册后验证 → `message` 为 `"激活成功"`，账号状态变为"已激活"
> - 已激活用户用此接口验证 → `message` 为 `"登录成功"`（等同于验证码登录）
> - 返回的 `jwt` 即登录凭证，后续请求在 `Authorization` 头中携带

**错误示例**：

| code | message | 触发条件 |
|------|---------|---------|
| `VALIDATION_OTP_MISSING` | 请输入邮箱和验证码 | email 或 code 缺失 |
| `AUTH_USER_NOT_FOUND` | 用户不存在 | 邮箱未注册 |
| `AUTH_USER_BLOCKED` | 账号已被禁用 | 用户被管理员封禁 |
| `OTP_INVALID` | 验证码错误 | 验证码不匹配 |
| `OTP_MAX_ATTEMPTS` | 验证码错误次数过多，请重新获取 | 连续错码达 10 次，验证码已作废（status=400） |
| `OTP_EXPIRED` | 验证码已过期 | 超过 10 分钟有效期 |

> **安全限制**：同一验证码连续输错 **10 次**即作废，需调用 [1.3 重发](#13-重发注册验证码) 重新获取。成功验证或重新发码都会重置计数。

---

### 1.3 重发注册验证码

```
POST /api/auth/resend-otp
```

**请求体**：

```json
{
  "email": "user@example.com"
}
```

**成功响应**：

```json
{
  "success": true,
  "data": {
    "message": "验证码已重发"
  },
  "message": "请求成功"
}
```

**错误示例**：

| code | message | 触发条件 |
|------|---------|---------|
| `VALIDATION_EMAIL_MISSING` | 请输入邮箱 | email 缺失 |
| `AUTH_USER_NOT_FOUND` | 用户不存在 | 邮箱未注册 |
| `AUTH_USER_ALREADY_ACTIVATED` | 用户已激活 | 已激活用户无需重发注册验证码 |
| `AUTH_USER_BLOCKED` | 账号已被禁用 | 用户被管理员封禁 |
| `OTP_COOLDOWN` | 请稍后再试 | 距上次发送不足 60 秒 |
| `OTP_SEND_FAILED` | 验证码发送失败，请稍后重试 | 邮件服务异常 |

> 新验证码有效期 **10 分钟**，旧验证码立即失效。

---

## 二、验证码登录流程

### 2.1 登录（发送验证码）

```
POST /api/auth/login-otp
```

**请求体**：

```json
{
  "email": "user@example.com"
}
```

**成功响应**：

```json
{
  "success": true,
  "data": {
    "message": "验证码已发送至您的邮箱"
  },
  "message": "请求成功"
}
```

**错误示例**：

| code | message | 触发条件 |
|------|---------|---------|
| `VALIDATION_EMAIL_MISSING` | 请输入邮箱 | email 缺失 |
| `AUTH_USER_NOT_REGISTERED` | 该用户尚未注册 | 邮箱未在系统中注册 |
| `AUTH_USER_NOT_ACTIVATED` | 账号尚未激活，请先完成注册 | 用户未完成激活流程 |
| `AUTH_USER_BLOCKED` | 账号已被禁用 | 用户被管理员封禁 |
| `OTP_COOLDOWN` | 请稍后再试 | 距上次发送不足 60 秒 |
| `OTP_SEND_FAILED` | 验证码发送失败，请稍后重试 | 邮件服务异常 |

> **说明**：发送登录验证码后，调用 [1.2 验证邮箱](#12-验证邮箱激活账号--验证码登录) 接口完成验证并获取 JWT。

---

## 三、密码登录（Strapi 原生）

```
POST /api/auth/local
```

**请求体**：

```json
{
  "identifier": "user@example.com",
  "password": "mypassword"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| identifier | string | 是 | 邮箱（也可用用户名） |
| password | string | 是 | 密码 |

**成功响应**：

```json
{
  "success": true,
  "data": {
    "jwt": "eyJhbGciOiJIUzI1NiIs...",
    "user": {
      "id": 1,
      "documentId": "a1b2c3d4-...",
      "username": "myusername",
      "email": "user@example.com",
      "createdAt": "2026-08-10T12:00:00.000Z",
      "updatedAt": "2026-08-10T12:00:00.000Z"
    }
  },
  "message": "请求成功"
}
```

> **说明**：此接口要求用户已是 `confirmed` 状态才能登录成功。
>
> **限流**：按 `IP + identifier(账号)` 每分钟 **60** 次。同一账号 1 分钟内尝试超过 60 次会触发 `SYSTEM_RATE_LIMITED`(429),换 IP 无效,只能等窗口过期或换账号。这是为了防字典攻击——攻击者即便用大量 IP,对单一账号的尝试频率也被卡死。

---

## 四、JWT 凭证续期

客户端每次启动时调用此接口，用当前有效的 JWT 换取一个全新的 30 天凭证，实现"永不过期"的登录态。

```
POST /api/auth/refresh-token
```

**请求头**：`Authorization: Bearer <jwt_token>`

**请求体**：无

**成功响应**：

```json
{
  "success": true,
  "data": {
    "jwt": "eyJhbGciOiJIUzI1NiIs...",
    "user": {
      "id": 1,
      "email": "user@example.com",
      "username": "myusername"
    }
  },
  "message": "请求成功"
}
```

**错误示例**：

```json
{
  "success": false,
  "error": {
    "status": 401,
    "code": "SYSTEM_UNAUTHORIZED",
    "message": "认证失败",
    "details": null,
    "timestamp": "2026-08-12T12:00:00.000Z"
  }
}
```

| code | message | 触发条件 |
|------|---------|---------|
| `SYSTEM_UNAUTHORIZED` | 认证失败 | 未携带 token 或 token 已过期（status=401） |
| `AUTH_USER_BLOCKED` | 账号已被禁用 | 用户被管理员封禁（每次续期都会复查） |

> **说明**：只要用户在 30 天内打开过客户端并成功续期，就永远不需要重新登录。超过 30 天未打开则需重新走 OTP 验证码登录。被封禁用户的 token 即使还在有效期内，续期时也会被拒绝。

---

## 五、获取当前用户信息

```
GET /api/users/me
```

**请求头**：

```
Authorization: Bearer <jwt_token>
```

**成功响应**：

```json
{
  "success": true,
  "data": {
    "id": 1,
    "documentId": "a1b2c3d4-...",
    "username": "myusername",
    "email": "user@example.com",
    "image": null,
    "role": { "id": 1, "name": "Authenticated", ... },
    "user_profile": { ... },
    "user_devices": [ ... ],
    "purchase_records": [ ... ],
    "user_presets": [ ... ]
  },
  "message": "请求成功"
}
```

> **说明**：
> - 顶层字段总是返回；关联字段（`user_profile` / `user_devices` / `purchase_records` / `user_presets`）**默认不返回**，需用 populate 查询参数显式拉取。
> - 拉全部关联：`GET /api/users/me?populate=*`
> - 拉指定关联：`GET /api/users/me?populate[0]=user_profile&populate[1]=user_devices`
> - 二级关联（如 device.product、purchase_record.product_model）需深层 populate 语法：`?populate[deep]=2`（若服务端未开 deep populate 则用具体路径）。
> - 各关联对象结构见 [数据类型定义](#数据类型定义)。注意关联字段名是 **`user_presets`**（不是 `presets`）。

---

## 六、用户信息管理

### 6.1 修改用户名 / 头像

```
PUT /api/auth/update-me
```

> **路径说明**：早期版本使用 `PUT /api/users/me`，但该路径会被 Strapi users-permissions 原生路由 `PUT /users/:id` 抢先匹配（koa-router 按声明顺序匹配），导致权限策略 `user.update` 拦截返回 401。改用 `/auth/update-me` 绕开原生路由前缀。

**请求头**：`Authorization: Bearer <jwt_token>`

接口同时支持两种 `Content-Type`：

#### 方式一：multipart/form-data（推荐，一次请求搞定）

直接把头像文件和可选字段塞到同一个表单里：

| 字段 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| username | string | 否 | 新用户名（全局唯一） |
| image | File | 否 | 头像文件（二进制） |

**前端示例**：

```js
const form = new FormData();
if (newUsername) form.append('username', newUsername);
if (avatarFile)  form.append('image', avatarFile);   // File 对象

await fetch('/api/auth/update-me', {
  method: 'PUT',
  headers: { Authorization: `Bearer ${jwt}` },       // 别手动设 Content-Type
  body: form,
});
```

#### 方式二：application/json（仅改用户名）

改头像必须走方式一（multipart）。JSON 模式只接受 `username`：

```json
{
  "username": "newusername"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| username | string | 是 | 新用户名（全局唯一） |

> 至少传一个字段：multipart 可传 `username` 或 `image`（或同时），JSON 仅 `username`。

**错误示例**：

| code | message | 触发条件 |
|------|---------|---------|
| `SYSTEM_UNAUTHORIZED` | 认证失败 | 未携带 token 或 token 已过期（status=401） |
| `AUTH_USER_BLOCKED` | 账号已被禁用 | 用户被管理员封禁 |
| `VALIDATION_NO_FIELDS` | 请提供需要修改的字段 | username 和 image 都没传 |
| `VALIDATION_USERNAME_LENGTH` | 用户名需 2-20 个字符 | 用户名长度越界 |
| `VALIDATION_USERNAME_FORMAT` | 用户名只能包含中文、字母、数字、下划线和连字符 | 用户名含非法字符 |
| `AUTH_USERNAME_TAKEN` | 该用户名已被占用 | 用户名被其他用户使用 |
| `AVATAR_TOO_LARGE` | 头像文件不能超过 5MB | 上传文件超 5MB |
| `AVATAR_TYPE_INVALID` | 头像仅支持 JPG、PNG、WebP、GIF 格式 | mimetype 不在允许列表 |

**成功响应**：

```json
{
  "success": true,
  "data": {
    "id": 1,
    "username": "newusername",
    "email": "user@example.com",
    "image": {
      "id": 5,
      "documentId": "xxxxxxxxxxxxxxxxxxxxxxxx",
      "name": "avatar.png",
      "alternativeText": null,
      "caption": null,
      "width": 800,
      "height": 800,
      "formats": {
        "thumbnail": {
          "url": "/uploads/thumbnail_avatar_xxxxxxxx.png",
          "mime": "image/png",
          "width": 156,
          "height": 156
        }
      },
      "mime": "image/png",
      "size": 64.78,
      "url": "/uploads/avatar_xxxxxxxx.png",
      "provider": "local",
      "createdAt": "2026-08-13T08:59:51.568Z",
      "updatedAt": "2026-08-13T08:59:51.568Z"
    }
  },
  "message": "请求成功"
}
```

---

## 七、密码管理

### 7.1 修改密码

```
POST /api/auth/change-password
```

**请求头**：`Authorization: Bearer <jwt_token>`

**请求体**：

```json
{
  "currentPassword": "oldpassword",
  "password": "newpassword",
  "passwordConfirmation": "newpassword"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| currentPassword | string | 是 | 当前密码 |
| password | string | 是 | 新密码，至少 8 位，字符不限（schema 强制） |
| passwordConfirmation | string | 是 | 确认新密码，必须与 password 一致 |

**成功响应**：

```json
{
  "success": true,
  "data": {
    "jwt": "eyJhbGciOiJIUzI1NiIs...",
    "user": {
      "id": 1,
      "username": "myusername",
      "email": "user@example.com"
    }
  },
  "message": "请求成功"
}
```

> **重要**：密码修改成功后，**其他设备持有的旧 JWT 会立即失效**（服务端通过 `token_version` 比对作废）。客户端必须用本次响应返回的新 `jwt` 替换本地存储，否则下次请求会被 401 拦截（`SYSTEM_UNAUTHORIZED`）。

**错误示例**：

| code | message | 触发条件 |
|------|---------|---------|
| `SYSTEM_UNAUTHORIZED` | 认证失败 | 未携带 token 或 token 已过期（status=401） |
| `VALIDATION_FIELD_MISSING` | 请填写完整的信息 | currentPassword / password / passwordConfirmation 缺失 |
| `VALIDATION_PASSWORD_LENGTH` | 密码至少 8 位 | 新密码长度不足 8 位 |
| `VALIDATION_PASSWORD_MISMATCH` | 两次输入的密码不一致 | password 与 passwordConfirmation 不一致 |
| `AUTH_WRONG_PASSWORD` | 原密码错误 | currentPassword 与数据库密码不匹配 |
| `VALIDATION_PASSWORD_SAME` | 新密码不能与原密码相同 | 新密码与原密码一致 |

### 7.2 发送验证码（忘记密码）

> 重置密码采用 **step-up 身份确认**两步式流程：先发码验证身份（7.2 → 7.3），拿到 5 分钟有效的 step-up JWT 后再设置新密码（7.4）。这是为客户端 UI 的两步式交互设计的，`verify-stepup` 作为通用"身份确认"原语未来可复用。

```
POST /api/auth/forgot-password
```

**请求体**：

```json
{
  "email": "user@example.com"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| email | string | 是 | 已注册的邮箱地址 |

**成功响应**：

```json
{
  "success": true,
  "data": {
    "message": "验证码已发送至您的邮箱"
  },
  "message": "请求成功"
}
```

> 发送成功后，使用 [7.3 身份确认](#73-身份确认verify-stepup) 校验验证码。验证码有效期 **10 分钟**，60 秒内不可重复请求。

**错误示例**：

| code | message | 触发条件 |
|------|---------|---------|
| `VALIDATION_EMAIL_MISSING` | 请输入邮箱 | email 缺失 |
| `AUTH_USER_NOT_REGISTERED` | 该用户尚未注册 | 邮箱未在系统中注册 |
| `AUTH_USER_NOT_ACTIVATED` | 账号尚未激活，请先完成注册 | 用户未完成激活流程 |
| `AUTH_USER_BLOCKED` | 账号已被禁用 | 用户被管理员封禁 |
| `OTP_COOLDOWN` | 请稍后再试 | 距上次发送不足 60 秒 |
| `OTP_SEND_FAILED` | 验证码发送失败，请稍后重试 | 邮件服务异常 |

### 7.3 身份确认（verify-stepup）

> 通用身份确认端点：校验 [7.2](#72-发送验证码忘记密码) 发出的验证码，通过后签发一个 **5 分钟有效**的 step-up JWT（携带 `purpose` 声明）。该 JWT 是后续敏感操作（如重置密码）的凭证，**不是登录凭证**。

```
POST /api/auth/verify-stepup
```

**请求体**：

```json
{
  "email": "user@example.com",
  "code": "123456",
  "purpose": "reset_password"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| email | string | 是 | 邮箱地址 |
| code | string | 是 | 6 位数字验证码（**字符串类型，别传数字**） |
| purpose | string | 是 | 操作类型，目前仅 `reset_password`（未来可扩展） |

**成功响应**：

```json
{
  "success": true,
  "data": {
    "stepup_jwt": "eyJhbGciOiJIUzI1NiIs..."
  },
  "message": "请求成功"
}
```

> 拿到 `stepup_jwt` 后，进入 [7.4 重置密码](#74-重置密码)。step-up JWT **5 分钟后过期**，过期需重新从 7.2 走完整流程。验证通过后验证码立即失效，不可重复使用。

**错误示例**：

| code | message | 触发条件 |
|------|---------|---------|
| `VALIDATION_OTP_MISSING` | 请输入邮箱和验证码 | email 或 code 缺失 |
| `VALIDATION_FIELD_MISSING` | 请填写完整的信息 | purpose 缺失 |
| `VALIDATION_PURPOSE_INVALID` | 无效的操作类型 | purpose 不在白名单（非 `reset_password`） |
| `AUTH_USER_NOT_REGISTERED` | 该用户尚未注册 | 邮箱未注册 |
| `AUTH_USER_NOT_ACTIVATED` | 账号尚未激活，请先完成注册 | 用户未完成激活流程 |
| `AUTH_USER_BLOCKED` | 账号已被禁用 | 用户被管理员封禁 |
| `OTP_INVALID` | 验证码错误 | 验证码不匹配 |
| `OTP_MAX_ATTEMPTS` | 验证码错误次数过多，请重新获取 | 连续错码达 10 次，验证码已作废（status=400） |
| `OTP_EXPIRED` | 验证码已过期 | 超过 10 分钟有效期 |

> **安全限制**：同一验证码连续输错 **10 次即作废**，需调用 [7.2](#72-发送验证码忘记密码) 重新获取。成功验证或重新发码都会重置计数。

### 7.4 重置密码

> 凭 [7.3](#73-身份确认verify-stepup) 签发的 step-up JWT 设置新密码。**不签发登录 JWT**：成功后客户端应展示成功页并跳回登录页，用新密码重新登录。

```
POST /api/auth/reset-password
```

**请求头**：

```
Authorization: Bearer <stepup_jwt>
```

**请求体**：

```json
{
  "password": "newpassword"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| password | string | 是 | 新密码，至少 8 位，字符不限 |

**成功响应**：

```json
{
  "success": true,
  "data": {
    "message": "密码重置成功"
  },
  "message": "请求成功"
}
```

**错误示例**：

| code | message | 触发条件 |
|------|---------|---------|
| `STEPUP_TOKEN_MISSING` | 缺少身份验证凭证 | 未携带 Authorization 头（status=401） |
| `STEPUP_TOKEN_INVALID` | 身份验证已失效，请重新操作 | step-up JWT 无效（status=401） |
| `STEPUP_TOKEN_EXPIRED` | 验证已过期，请重新获取验证码 | step-up JWT 超过 5 分钟（status=401） |
| `STEPUP_WRONG_PURPOSE` | 该凭证不支持此操作 | JWT 的 purpose 非 `reset_password`（status=403） |
| `VALIDATION_FIELD_MISSING` | 请填写完整的信息 | password 缺失 |
| `VALIDATION_PASSWORD_LENGTH` | 密码至少 8 位 | 新密码长度不足 8 位 |

> **客户端提示**：收到 `STEPUP_TOKEN_MISSING` / `STEPUP_TOKEN_INVALID` / `STEPUP_TOKEN_EXPIRED` 任一（均为 401）时，step-up 凭证已不可用，应引导用户回到忘记密码第一步重新发码。
>
> **重要**：密码重置成功后，**该账号在所有设备上的旧 JWT 会立即失效**（服务端通过 `token_version` 比对作废）。本接口不签发新登录 JWT，用户需用新密码重新登录。

---

## 八、用户预设管理

所有预设接口均需认证，自动限定为当前用户自己的数据。非自己的预设一律返回 404，不泄漏存在性。

> **`:id` 路径参数说明（重要）**：这里的 `:id` 是 Strapi v5 core router 的命名残留——参数名虽然叫 `:id`，但**实际接收的值是 `documentId`（字符串）**，不是数字主键 `id`。客户端做单条查 / 改 / 删时，必须传列表 / 创建响应里的 **`documentId`** 字段值；传数字 `id` 会匹配不到（返回 `PRESET_NOT_FOUND` 404）。

**通用错误示例**（所有预设接口共用）：

| code | message | 触发条件 |
|------|---------|---------|
| `SYSTEM_UNAUTHORIZED` | 认证失败 | 未携带 token 或 token 已过期（status=401） |
| `PRESET_NOT_FOUND` | 预设不存在 | 预设不存在或不属于当前用户（status=404） |

### 8.1 获取预设列表

```
GET /api/user-presets
```

> 默认返回当前用户的全部预设（无分页）。传 `?pagination[page]=1&pagination[pageSize]=25` 启用分页，响应结构见 [分页响应](#分页响应)。预设对象结构见 [UserPreset](#userpreset)。

**成功响应**：

```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "documentId": "nq8k75ksphn8u227sucij3tw",
      "config_data": { "force": 50, "angle": 270 },
      "user": 1,
      "createdAt": "2026-08-10T12:00:00.000Z",
      "updatedAt": "2026-08-10T12:00:00.000Z"
    }
  ],
  "meta": {},
  "message": "请求成功"
}
```

### 8.2 获取单个预设

```
GET /api/user-presets/:id
```

> 非自己的预设返回 404。

### 8.3 创建预设

```
POST /api/user-presets
```

**请求体**：

```json
{
  "config_data": { "force": 90, "angle": 360 }
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| config_data | object | 是 | 预设参数配置（JSON） |

> `user` 字段由服务端自动绑定为当前用户，**无需也禁止手动传**（传了会被忽略）。

**成功响应**：

```json
{
  "success": true,
  "data": {
    "id": 2,
    "documentId": "oc8ax8bwr154l3xlepgk8i2j",
    "config_data": { "force": 90, "angle": 360 },
    "user": 1,
    "createdAt": "2026-08-10T12:00:00.000Z",
    "updatedAt": "2026-08-10T12:00:00.000Z"
  },
  "message": "请求成功"
}
```

> 创建成功后，**保存返回的 `documentId`**，它是后续 8.2 / 8.4 / 8.5 单条操作要传的路径参数值。

### 8.4 更新预设

```
PUT /api/user-presets/:id
```

> 非自己的预设返回 404。`user` 字段不可修改（传了会被忽略）。

**请求体**：同创建，传需要修改的字段即可。

### 8.5 删除预设

```
DELETE /api/user-presets/:id
```

> 非自己的预设返回 404。

---

## 附录：完整注册 / 登录流程图

### 新用户注册

```
注册(register-otp) → 收到验证码邮件 → 验证(verify-otp) → 获得 JWT → 进入应用
                       ↓ 验证码过期
                  重发(resend-otp) → 验证(verify-otp)
```

### 已有账号登录

```
方式一（验证码）：登录(login-otp) → 收到验证码邮件 → 验证(verify-otp) → 获得 JWT
方式二（密码）  ：直接调用 /api/auth/local
```

### 客户端启动完整流程

```
客户端启动
  ↓
refresh-token
  ├── 成功（新 JWT）→ 进入应用
  └── 401（已过期）
        ↓
      登录页
        ├── 验证码登录：login-otp → verify-otp → 进入应用
        └── 密码登录：  /api/auth/local → 进入应用
```

### 凭证续期

```
客户端启动 → refresh-token（携带当前 JWT）→ 获得新 30 天 JWT → 继续使用
超过 30 天未启动 → refresh-token 返回 401 → 回退到登录流程
```

### 忘记密码（step-up 两步式重置）

```
第一步（身份确认）：
  forgot-password(email) → 收到重置验证码邮件
  verify-stepup(email, code, purpose:'reset_password') → 获得 stepup_jwt（5 分钟有效）

第二步（设置新密码）：
  reset-password(password)  [Authorization: Bearer <stepup_jwt>]
    ├── 成功 → 密码重置成功 → 回登录页用新密码登录（不签发 JWT）
    └── 401（STEPUP_TOKEN_*）→ step-up 凭证失效 → 回第一步重新发码
```

### 验证码接口双用途说明

`/api/auth/verify-otp` 同时支持：
- **注册激活**：用户首次注册后，验证通过即激活账号
- **验证码登录**：用户通过 `login-otp` 获取验证码后，调用此接口完成登录

客户端无需区分场景，统一调用即可。
