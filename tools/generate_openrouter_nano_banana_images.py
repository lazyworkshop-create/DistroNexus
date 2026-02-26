from __future__ import annotations

import argparse
import base64
import json
import logging
import mimetypes
import os
import re
from pathlib import Path
from typing import Any

import requests
from dotenv import dotenv_values, load_dotenv
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

OPENROUTER_API_URL = "https://openrouter.ai/api/v1/chat/completions"
DEFAULT_MODEL = "google/gemini-3-pro-image-preview"
DEFAULT_SOURCE_PATH = Path("20260224-AI-Agent-ERP影响研究/20260224-AI-Agent-ERP-Article-1-Image-Prompts.md")
DEFAULT_OUTPUT_DIR = Path("generated-images")
LOGGER = logging.getLogger("openrouter-image-generator")


def load_openrouter_key(env_path: Path) -> str:
    load_dotenv(dotenv_path=env_path, override=False)
    for env_name in ("OPENROUTER_API_KEY", "OPENROUTER_KEY", "OPENROUTER"):
        value = os.getenv(env_name, "").strip().strip('"').strip("'")
        if value:
            return value

    if not env_path.exists():
        raise FileNotFoundError(f".env not found: {env_path}")

    parsed = dotenv_values(env_path)
    for key in ("OPENROUTER_API_KEY", "OPENROUTER_KEY", "OPENROUTER"):
        value = str(parsed.get(key, "")).strip().strip('"').strip("'")
        if value and value != "None":
            return value

    for raw_line in env_path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue

        if "=" in line:
            key, value = line.split("=", 1)
            key = key.strip().upper()
            value = value.strip().strip('"').strip("'")
            if key in {"OPENROUTER_API_KEY", "OPENROUTER_KEY", "OPENROUTER"} and value:
                return value

        if ":" in line:
            key, value = line.split(":", 1)
            key = key.strip().upper()
            value = value.strip().strip('"').strip("'")
            if key in {"OPENROUTER", "OPENROUTER_API_KEY", "OPENROUTER_KEY"} and value:
                return value

    raise ValueError("OpenRouter API key not found in .env")


def build_http_session() -> requests.Session:
    retry = Retry(
        total=3,
        backoff_factor=1,
        status_forcelist=(429, 500, 502, 503, 504),
        allowed_methods=frozenset(["GET", "POST"]),
        raise_on_status=False,
    )
    adapter = HTTPAdapter(max_retries=retry)
    session = requests.Session()
    session.mount("https://", adapter)
    session.mount("http://", adapter)
    return session


def resolve_proxy_config(http_proxy: str | None, https_proxy: str | None) -> dict[str, str]:
    resolved_http = (
        (http_proxy or "").strip()
        or os.getenv("OPENROUTER_HTTP_PROXY", "").strip()
        or os.getenv("HTTP_PROXY", "").strip()
    )
    resolved_https = (
        (https_proxy or "").strip()
        or os.getenv("OPENROUTER_HTTPS_PROXY", "").strip()
        or os.getenv("HTTPS_PROXY", "").strip()
    )

    proxies: dict[str, str] = {}
    if resolved_http:
        proxies["http"] = resolved_http
    if resolved_https:
        proxies["https"] = resolved_https
    return proxies


def extract_prompts_from_markdown(markdown_text: str) -> list[str]:
    blocks = re.findall(r"\*\*Prompt:\*\*\s*\n((?:>.*\n?)+)", markdown_text, flags=re.MULTILINE)
    prompts: list[str] = []

    for block in blocks:
        lines = [line.lstrip(">").strip() for line in block.splitlines() if line.strip().startswith(">")]
        prompt = " ".join(lines).strip()
        if prompt:
            prompts.append(prompt)

    if prompts:
        return prompts

    heading_blocks = re.findall(
        r"^#{1,6}.*?(?:Prompt|提示词).*?$\n([^#]+)",
        markdown_text,
        flags=re.MULTILINE | re.IGNORECASE,
    )
    for block in heading_blocks:
        text = block.strip()
        if text:
            prompts.append(" ".join(line.strip() for line in text.splitlines() if line.strip()))

    if prompts:
        return prompts

    quote_lines = [line.lstrip(">").strip() for line in markdown_text.splitlines() if line.strip().startswith(">")]
    merged_quote = " ".join(line for line in quote_lines if line)
    if merged_quote:
        return [merged_quote]

    raise ValueError("No prompts found in markdown. Add '**Prompt:**' blocks, prompt sections, or quoted prompt lines.")


def extract_prompts_from_text(text: str) -> list[str]:
    chunks = re.split(r"\n\s*(?:---+|===+)\s*\n", text)
    prompts = [chunk.strip() for chunk in chunks if chunk.strip()]
    if prompts:
        return prompts
    raise ValueError("No prompts found in text source.")


def extract_prompts_from_json(text: str) -> list[str]:
    try:
        payload = json.loads(text)
    except json.JSONDecodeError as error:
        raise ValueError(f"Invalid JSON prompt source: {error}") from error

    if isinstance(payload, list):
        prompts = [str(item).strip() for item in payload if str(item).strip()]
        if prompts:
            return prompts
        raise ValueError("JSON list contains no valid prompt strings.")

    if isinstance(payload, dict):
        values = payload.get("prompts")
        if isinstance(values, list):
            prompts = [str(item).strip() for item in values if str(item).strip()]
            if prompts:
                return prompts

    raise ValueError("JSON source must be a list of prompts or an object with a 'prompts' list.")


def detect_source_format(source_path: Path) -> str:
    suffix = source_path.suffix.lower()
    if suffix in {".md", ".markdown"}:
        return "markdown"
    if suffix == ".json":
        return "json"
    return "text"


def load_prompts(
    source_path: Path | None,
    input_format: str,
    inline_prompts: list[str],
) -> list[str]:
    prompts: list[str] = [item.strip() for item in inline_prompts if item.strip()]

    if source_path is None:
        if prompts:
            return prompts
        raise ValueError("No prompts provided. Use --source or --prompt.")

    if not source_path.exists():
        raise FileNotFoundError(f"Prompt source file not found: {source_path}")

    raw_text = source_path.read_text(encoding="utf-8")
    chosen_format = detect_source_format(source_path) if input_format == "auto" else input_format

    if chosen_format == "markdown":
        prompts.extend(extract_prompts_from_markdown(raw_text))
    elif chosen_format == "json":
        prompts.extend(extract_prompts_from_json(raw_text))
    elif chosen_format == "text":
        prompts.extend(extract_prompts_from_text(raw_text))
    else:
        raise ValueError(f"Unsupported input format: {chosen_format}")

    normalized = [item for item in (prompt.strip() for prompt in prompts) if item]
    if not normalized:
        raise ValueError("No prompts available after parsing input.")
    return normalized


def extract_data_url(value: str) -> tuple[bytes, str] | None:
    match = re.match(r"^data:(image/[\w.+-]+);base64,(.+)$", value, flags=re.DOTALL)
    if not match:
        return None

    mime = match.group(1).strip().lower()
    raw_b64 = "".join(match.group(2).split())
    image_bytes = base64.b64decode(raw_b64)
    ext = mimetypes.guess_extension(mime) or ".png"
    return image_bytes, ext


def try_decode_base64(value: str) -> bytes | None:
    compact = "".join(value.split())
    if len(compact) < 128:
        return None
    if not re.fullmatch(r"[A-Za-z0-9+/=]+", compact):
        return None

    try:
        return base64.b64decode(compact, validate=True)
    except Exception:
        return None


def pick_image_source(data: dict[str, Any]) -> str | None:
    if isinstance(data.get("data"), list) and data["data"]:
        first = data["data"][0]
        if isinstance(first, dict):
            if first.get("b64_json"):
                return str(first["b64_json"])
            if isinstance(first.get("url"), str):
                return first["url"]

    choices = data.get("choices")
    if not isinstance(choices, list) or not choices:
        return None

    message = choices[0].get("message") if isinstance(choices[0], dict) else None
    if not isinstance(message, dict):
        return None

    images = message.get("images")
    if isinstance(images, list) and images:
        image_obj = images[0]
        if isinstance(image_obj, dict):
            if isinstance(image_obj.get("image_url"), dict):
                url = image_obj["image_url"].get("url")
                if isinstance(url, str) and url:
                    return url
            if isinstance(image_obj.get("url"), str):
                return image_obj["url"]
            if isinstance(image_obj.get("b64_json"), str):
                return image_obj["b64_json"]

    content = message.get("content")
    if isinstance(content, list):
        for part in content:
            if not isinstance(part, dict):
                continue
            if part.get("type") == "image_url":
                image_url = part.get("image_url")
                if isinstance(image_url, dict) and isinstance(image_url.get("url"), str):
                    return image_url["url"]
                if isinstance(image_url, str):
                    return image_url
            if part.get("type") in {"output_image", "image"} and isinstance(part.get("b64_json"), str):
                return part["b64_json"]

    text = message.get("content")
    if isinstance(text, str):
        markdown_image = re.search(r"!\[[^\]]*\]\((https?://[^)]+)\)", text)
        if markdown_image:
            return markdown_image.group(1)

        url_match = re.search(r"https?://\S+", text)
        if url_match:
            return url_match.group(0)

    return None


def normalize_image_to_bytes(session: requests.Session, source: str, timeout: int) -> tuple[bytes, str]:
    data_url = extract_data_url(source)
    if data_url is not None:
        return data_url

    if source.startswith("http://") or source.startswith("https://"):
        try:
            response = session.get(source, timeout=timeout)
            response.raise_for_status()
        except requests.RequestException as error:
            raise RuntimeError(f"Failed to download generated image: {error}") from error

        body = response.content
        content_type = (response.headers.get("Content-Type") or "").split(";")[0].strip().lower()
        ext = mimetypes.guess_extension(content_type) or ".png"
        return body, ext

    decoded = try_decode_base64(source)
    if decoded is None:
        raise ValueError("Cannot parse image source returned by OpenRouter")
    return decoded, ".png"


def call_openrouter_image(
    session: requests.Session,
    prompt: str,
    api_key: str,
    model: str,
    timeout: int,
) -> dict[str, Any]:
    payload = {
        "model": model,
        "messages": [
            {
                "role": "user",
                "content": [
                    {
                        "type": "text",
                        "text": prompt,
                    }
                ],
            }
        ],
        "modalities": ["text", "image"],
    }

    headers = {
        "Authorization": f"Bearer {api_key}",
        "Content-Type": "application/json",
        "HTTP-Referer": "https://localhost",
        "X-Title": "nano-banana-image-batch",
    }

    try:
        response = session.post(OPENROUTER_API_URL, headers=headers, json=payload, timeout=timeout)
        response.raise_for_status()
    except requests.HTTPError as error:
        body = error.response.text if error.response is not None else str(error)
        status_code = error.response.status_code if error.response is not None else "unknown"
        raise RuntimeError(f"OpenRouter request failed ({status_code}): {body}") from error
    except requests.RequestException as error:
        raise RuntimeError(f"OpenRouter request failed: {error}") from error

    return response.json()


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate images from prompt sources using OpenRouter image models")
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE_PATH, help="Prompt source file: .md/.json/.txt")
    parser.add_argument("--format", choices=["auto", "markdown", "json", "text"], default="auto", help="Prompt source format")
    parser.add_argument("--prompt", action="append", default=[], help="Inline prompt, can be repeated")
    parser.add_argument("--env", type=Path, default=Path(".env"), help="Path to .env file")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUTPUT_DIR, help="Output directory")
    parser.add_argument("--model", type=str, default=DEFAULT_MODEL, help="OpenRouter model ID")
    parser.add_argument("--timeout", type=int, default=180, help="HTTP timeout seconds")
    parser.add_argument("--limit", type=int, default=3, help="How many prompts to generate")
    parser.add_argument("--start-index", type=int, default=1, help="Start index for output file naming")
    parser.add_argument("--prefix", type=str, default="image", help="Output filename prefix")
    parser.add_argument("--http-proxy", type=str, default="", help="HTTP proxy URL, e.g. http://127.0.0.1:7897")
    parser.add_argument("--https-proxy", type=str, default="", help="HTTPS proxy URL, e.g. http://127.0.0.1:7897")
    parser.add_argument("--no-proxy", action="store_true", help="Force disable all proxies (ignore env and args)")
    parser.add_argument("--dry-run", action="store_true", help="Validate and print prompts without calling OpenRouter")
    parser.add_argument("--verbose", action="store_true", help="Enable verbose logs")
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.DEBUG if args.verbose else logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )

    prompts = load_prompts(source_path=args.source, input_format=args.format, inline_prompts=args.prompt)
    prompts = prompts[: max(1, args.limit)]

    if args.dry_run:
        LOGGER.info("Dry-run mode: %s prompt(s) parsed.", len(prompts))
        for idx, prompt in enumerate(prompts, start=args.start_index):
            LOGGER.info("Prompt %03d: %.120s%s", idx, prompt, "..." if len(prompt) > 120 else "")
        return

    api_key = load_openrouter_key(args.env)

    args.out.mkdir(parents=True, exist_ok=True)
    session = build_http_session()
    if args.no_proxy:
        session.trust_env = False
        session.proxies.clear()
        LOGGER.info("Proxy disabled by --no-proxy")
    else:
        proxies = resolve_proxy_config(args.http_proxy, args.https_proxy)
        if proxies:
            session.proxies.update(proxies)
            LOGGER.info("Proxy enabled. http=%s https=%s", proxies.get("http", ""), proxies.get("https", ""))

    manifest: list[dict[str, Any]] = []

    for index, prompt in enumerate(prompts, start=args.start_index):
        LOGGER.info("[%s/%s] Generating image with model %s", index - args.start_index + 1, len(prompts), args.model)
        response_json = call_openrouter_image(
            session=session,
            prompt=prompt,
            api_key=api_key,
            model=args.model,
            timeout=args.timeout,
        )
        source = pick_image_source(response_json)
        if source is None:
            raise RuntimeError(
                f"No image found in OpenRouter response for prompt {index}: "
                f"{json.dumps(response_json, ensure_ascii=False)[:1000]}"
            )

        image_bytes, ext = normalize_image_to_bytes(session=session, source=source, timeout=args.timeout)
        file_name = f"{args.prefix}-{index:03d}{ext}"
        file_path = args.out / file_name
        file_path.write_bytes(image_bytes)

        meta_path = args.out / f"{args.prefix}-{index:03d}.response.json"
        meta_path.write_text(json.dumps(response_json, ensure_ascii=False, indent=2), encoding="utf-8")

        manifest.append(
            {
                "index": index,
                "prompt": prompt,
                "model": args.model,
                "image": str(file_path),
                "response": str(meta_path),
            }
        )

        LOGGER.info("Saved: %s", file_path)

    manifest_path = args.out / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    LOGGER.info("Done. Manifest: %s", manifest_path)


if __name__ == "__main__":
    main()
