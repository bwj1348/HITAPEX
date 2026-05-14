
# HITAPEX 赛车模拟设备官网 API 完整文档

## 概述

- **基础URL**:  `http://192.168.1.214:1337/api` 
- **认证方式**: Bearer Token
- **数据格式**: JSON
- **版本**: 1.0

## 认证

所有API请求需要在Header中添加认证信息：

```
Authorization: Bearer {您的API令牌}
```

**API令牌示例**:
```
8802be9bbe4baff304f429277b3573c4147ef9e5bcb700abb27c44d88f288650f30540b2131a156fb2c83421ad87ea035b7d62f5fd63d8ed13d942c773529b4193abe95af46c664c6323b30efa62a591f2f5bb03e97472eb3d11fa25152c86110db2511c5b7cfa6a6c974ee6c88039b9f18d1c985092898f6ad0b6d0c97ad661
```



## API端点列表

### 海报 (Banner)

**获取海报列表**

- **URL**: `GET /api/banners`


### 游戏 (Game)

**获取游戏列表**

- **URL**: `GET /api/games`
- **描述**: 获取支持的赛车游戏列表


- **响应示例**:
```json
{
    "data": [
        {
            "id": 2,
            "name": "Assetto Corsa Competizione",
            "description": "GT赛车模拟游戏，官方授权",
            "cover_image": {
                "id": 1,
                "name": "acc_cover.jpg",
                "url": "/uploads/games/acc_cover.jpg"
            },
            "bg_image": {
                "id": 2,
                "name": "acc_bg.jpg",
                "url": "/uploads/games/acc_bg.jpg"
            },
            "steam_id": "114514"
        }
    ]
}
```

### 预设 (Preset)

**获取预设列表**

- **URL**: `GET /api/presets`


### 下载 (Download)

**获取下载列表**

- **URL**: `GET /api/downloads`


### 固件版本 (Firmware Version)

**获取固件版本信息**

- **URL**: `GET /api/firmware-versions`


### 产品 (Product)

**获取产品列表**

- **URL**: `GET /api/products`






```

## 注意事项

1. **认证**: 所有API请求都需要在Header中添加Bearer Token
2. **数据格式**: 所有API返回数据都包含在 `data` 字段中
3. **关联数据**: 使用 `populate` 参数获取关联数据
4. **筛选数据**: 使用 `filters` 参数进行数据筛选
5. **文件路径**: 图片和文件URL需要拼接完整路径：`http://192.168.1.214:1337{url}`
6. **生产环境**: 建议使用HTTPS在生产环境中
7. **令牌管理**: 定期检查API令牌的有效性

