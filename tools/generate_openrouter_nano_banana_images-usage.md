# OpenRouter 图片生成脚本使用说明

本文档说明如何使用 `tools/generate_openrouter_nano_banana_images.py` 批量生成图片。

## 1. 环境准备

在项目根目录执行：

```powershell
.\.venv\Scripts\python.exe -m pip install -r tools/requirements-openrouter-image.txt
```

确保根目录 `.env` 至少包含：

```dotenv
OPENROUTER_API_KEY=你的_openrouter_key
```

可选代理配置：

```dotenv
HTTP_PROXY=http://127.0.0.1:7897
HTTPS_PROXY=http://127.0.0.1:7897
# 或仅给本脚本使用
# OPENROUTER_HTTP_PROXY=http://127.0.0.1:7897
# OPENROUTER_HTTPS_PROXY=http://127.0.0.1:7897
```

## 2. 常用命令

### 2.1 从 Markdown 提示词生成 3 张图

```powershell
.\.venv\Scripts\python.exe tools/generate_openrouter_nano_banana_images.py `
  --source "docs/promotion/windows-store-publish-success-process-image-prompts.md" `
  --limit 3 `
  --out "docs/promotion/image" `
  --prefix "windows-store-publish-success-process-ai" `
  --model "google/gemini-3-pro-image-preview"
```

### 2.2 强制不使用代理

即使 `.env` 或系统里有代理变量，也可以强制直连：

```powershell
.\.venv\Scripts\python.exe tools/generate_openrouter_nano_banana_images.py `
  --source "docs/promotion/windows-store-publish-success-process-image-prompts.md" `
  --limit 1 `
  --no-proxy
```

### 2.3 命令行显式指定代理

```powershell
.\.venv\Scripts\python.exe tools/generate_openrouter_nano_banana_images.py `
  --source "docs/promotion/windows-store-publish-success-process-image-prompts.md" `
  --limit 1 `
  --http-proxy "http://127.0.0.1:7897" `
  --https-proxy "http://127.0.0.1:7897"
```

### 2.4 仅检查提示词解析（不调用接口）

```powershell
.\.venv\Scripts\python.exe tools/generate_openrouter_nano_banana_images.py --dry-run --limit 3
```

## 3. 参数说明

- `--source`：提示词来源文件（支持 `.md` / `.json` / `.txt`）。
- `--format`：输入格式（`auto|markdown|json|text`）。
- `--prompt`：直接传入提示词（可重复多次）。
- `--env`：`.env` 路径。
- `--out`：输出目录。
- `--model`：OpenRouter 模型 ID。
- `--limit`：生成数量上限。
- `--prefix`：输出文件名前缀。
- `--http-proxy` / `--https-proxy`：命令行代理设置。
- `--no-proxy`：强制禁用代理（忽略命令行与环境变量代理）。
- `--dry-run`：仅解析，不请求 API。
- `--verbose`：输出调试日志。

## 4. 代理优先级

当未使用 `--no-proxy` 时，代理优先级为：

1. `--http-proxy` / `--https-proxy`
2. `OPENROUTER_HTTP_PROXY` / `OPENROUTER_HTTPS_PROXY`
3. `HTTP_PROXY` / `HTTPS_PROXY`

## 5. 输出文件

每个 prompt 会输出：

- 图片文件：`{prefix}-001.jpg/png`
- 响应备份：`{prefix}-001.response.json`

同时输出：

- `manifest.json`：生成结果汇总。

## 6. 常见问题

- `403 This model is not available in your region`：模型地域受限，换模型或走代理。
- `Response ended prematurely`：网络链路中断，建议启用代理或重试。
- `No endpoints found for <model>`：模型 ID 写错或该模型下线，先用 OpenRouter 模型列表确认。
