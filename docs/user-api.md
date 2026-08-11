# 用户系统 API 文档

## 基础信息

- **Base URL**: `http://192.168.1.214:1337/api`
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

### 错误响应

```json
{
  "success": false,
  "error": {
    "code": 400,
    "message": "请求参数错误",
    "details": "具体错误描述",
    "timestamp": "2026-08-10T12:00:00.000Z"
  }
}
```

常见错误码：

| code | 说明 |
|------|------|
| 400 | 请求参数错误 |
| 401 | 认证失败（token 无效或过期） |
| 404 | 资源不存在 |
| 409 | 数据已存在（如重复注册） |
| 500 | 服务器内部错误 |

---

## 认证方式

除公开接口外，需要认证的接口在请求头中携带 JWT：

```
Authorization: Bearer <jwt_token>
```

JWT token 由登录/验证码验证接口返回。

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
| username | string | 是 | 用户名，最少 3 个字符，全局唯一 |
| password | string | 是 | 密码，最少 6 个字符 |

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

| details | 触发条件 |
|---|---|
| `请填写完整的注册信息` | email / username / password 有缺失 |
| `该邮箱已被注册` | 邮箱已被已激活用户占用 |
| `该用户名已被占用` | 用户名被其他用户使用 |

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
| code | string | 是 | 6 位数字验证码 |

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

| details | 触发条件 |
|---|---|
| `请输入邮箱和验证码` | email 或 code 缺失 |
| `用户不存在` | 邮箱未注册 |
| `验证码错误` | 验证码不匹配 |
| `验证码已过期` | 超过 10 分钟有效期 |

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

| details | 触发条件 |
|---|---|
| `请输入邮箱` | email 缺失 |
| `用户不存在` | 邮箱未注册 |
| `用户已激活` | 已激活用户无需重发注册验证码 |
| `请稍后再试` | 距上次发送不足 60 秒 |

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

| details | 触发条件 |
|---|---|
| `请输入邮箱` | email 缺失 |
| `该用户尚未注册` | 邮箱未在系统中注册 |
| `账号尚未激活，请先完成注册` | 用户未完成激活流程 |

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
      "username": "myusername",
      "email": "user@example.com",
      "provider": "local",
      "confirmed": true,
      "blocked": false
    }
  },
  "message": "请求成功"
}
```

> **说明**：此接口要求用户已是 `confirmed` 状态才能登录成功。

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
    "code": 401,
    "message": "认证失败",
    "details": "token 无效或过期"
  }
}
```

> **说明**：只要用户在 30 天内打开过客户端并成功续期，就永远不需要重新登录。超过 30 天未打开则需重新走 OTP 验证码登录。

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
    "username": "myusername",
    "email": "user@example.com",
    "provider": "local",
    "confirmed": true,
    "blocked": false,
    "image": null,
    "role": { "id": 1, "name": "Authenticated", ... },
    "user_profile": { ... },
    "user_devices": [ ... ],
    "purchase_records": [ ... ],
    "presets": [ ... ]
  },
  "message": "请求成功"
}
```

> **说明**：返回用户完整信息，包含关联的个人资料、设备、购买记录等。

---

## 六、用户信息管理

### 6.1 修改用户名 / 头像

```
PUT /api/users/me
```

**请求头**：`Authorization: Bearer <jwt_token>`

**请求体**：

```json
{
  "username": "newusername",
  "image": 5
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| username | string | 否 | 新用户名（全局唯一） |
| image | number | 否 | 头像媒体 ID，需先通过 `POST /api/upload` 上传获取 |

> 至少传一个字段，可同时修改。`image` 为 Strapi 媒体库中的文件 ID，客户端需先调用上传接口。

**错误示例**：

| details | 触发条件 |
|---|---|
| `请先登录` | 未携带 token 或 token 已过期 |
| `请提供需要修改的字段` | username 和 image 都没传 |
| `该用户名已被占用` | 用户名被其他用户使用 |

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
      "url": "/uploads/avatar_abc123.png",
      ...
    }
  },
  "message": "请求成功"
}
```

**头像上传流程**：

```
POST /api/upload (multipart/form-data, files 字段) → 返回 fileId
  ↓
PUT /api/users/me  { "image": fileId }
```

---

## 七、其他标准接口

以下为 Strapi 原生提供的用户相关接口：

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
| password | string | 是 | 新密码 |
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

> 密码修改成功后旧 token 仍有效，建议客户端用返回的新 `jwt` 替换本地存储。

### 7.2 忘记密码（发送重置邮件）

```
POST /api/auth/forgot-password
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
    "ok": true
  },
  "message": "请求成功"
}
```

> 无论邮箱是否注册均返回成功，防止用户枚举攻击。

### 7.3 重置密码

```
POST /api/auth/reset-password
```

**请求体**：

```json
{
  "code": "重置码（来自邮件中的链接）",
  "password": "newpassword",
  "passwordConfirmation": "newpassword"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| code | string | 是 | 邮件链接中的重置码 |
| password | string | 是 | 新密码 |
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

---

## 八、云预设管理

所有预设接口均需认证，自动限定为当前用户自己的数据。

### 8.1 获取预设列表

```
GET /api/presets
```

可选查询参数：`?filters[device_category][$eq]=base` 按设备类型筛选（`base` / `wheel` / `pedal`）。

**成功响应**：

```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "name": "日常模式",
      "description": "通勤用",
      "parameters": { "force": 50, "angle": 270 },
      "is_default": true,
      "sort_order": 1,
      "is_enabled": true,
      "device_category": "base",
      "user": 1,
      "createdAt": "2026-08-10T12:00:00.000Z",
      "updatedAt": "2026-08-10T12:00:00.000Z"
    }
  ],
  "message": "请求成功"
}
```

### 8.2 获取单个预设

```
GET /api/presets/:id
```

> 非自己的预设返回 404。

### 8.3 创建预设

```
POST /api/presets
```

**请求体**：

```json
{
  "name": "赛道模式",
  "description": "高反馈力度",
  "parameters": { "force": 90, "angle": 360 },
  "is_default": false,
  "sort_order": 2,
  "is_enabled": true,
  "device_category": "base"
}
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|:---:|------|
| name | string | 否 | 预设名称 |
| description | string | 否 | 描述 |
| parameters | object | 否 | 参数配置（JSON） |
| is_default | boolean | 否 | 是否默认，默认 `true` |
| sort_order | number | 否 | 排序 |
| is_enabled | boolean | 否 | 是否启用，默认 `true` |
| device_category | enum | 否 | `base` / `wheel` / `pedal` |

> `user` 字段由服务端自动绑定为当前用户，**无需也禁止手动传**。

### 8.4 更新预设

```
PUT /api/presets/:id
```

> 非自己的预设返回 404。`user` 字段不可修改。

**请求体**：同创建，传需要修改的字段即可。

### 8.5 删除预设

```
DELETE /api/presets/:id
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

### 验证码接口双用途说明

`/api/auth/verify-otp` 同时支持：
- **注册激活**：用户首次注册后，验证通过即激活账号
- **验证码登录**：用户通过 `login-otp` 获取验证码后，调用此接口完成登录

客户端无需区分场景，统一调用即可。
